using System.Net;
using System.Text.Json;
using FluentValidation;
using LeccionesAprendidas.Models;
using LeccionesAprendidas.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace LeccionesAprendidas.Functions;

public class LessonLearnedFunctions
{
    private readonly IOpenAIService _openAIService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ISearchService _searchService;
    private readonly IValidator<CreateLessonRequest> _createValidator;
    private readonly IValidator<SearchLessonRequest> _searchValidator;

    public LessonLearnedFunctions(
        IOpenAIService openAIService,
        ICosmosDbService cosmosDbService,
        ISearchService searchService,
        IValidator<CreateLessonRequest> createValidator,
        IValidator<SearchLessonRequest> searchValidator)
    {
        _openAIService = openAIService;
        _cosmosDbService = cosmosDbService;
        _searchService = searchService;
        _createValidator = createValidator;
        _searchValidator = searchValidator;
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };


    [Function("RegisterLesson")]
    public async Task<HttpResponseData> RegisterLessonAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<CreateLessonRequest>(body, JsonOptions);

        if (request is null)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");

        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage)));
        var lesson = new LessonLearned
        {
            Code = request.Code,
            Description = request.Description,
            SituationType = request.SituationType,
            Location = request.Location,
            RelatedPosition = request.RelatedPosition,
            Analysis = request.Analysis,
            Consequences = request.Consequences,
            Lesson = request.LessonLearned,
            DateTime = request.DateTime
        };

        var embeddingTask = _openAIService.GenerateEmbeddingAsync(lesson.SearchContent);
        var enrichmentTask = _openAIService.GenerateLessonEnrichmentAsync(lesson.SearchContent);
        await Task.WhenAll(embeddingTask, enrichmentTask);

        var embeddingResult = await embeddingTask;
        if (embeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, embeddingResult.Error ?? "Unknown error generating embedding.");

        var enrichmentResult = await enrichmentTask;
        if (enrichmentResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, enrichmentResult.Error ?? "Unknown error generating lesson enrichment.");

        lesson.SearchContentEmbedding = embeddingResult.Value!;
        lesson.SuggestDisplay = enrichmentResult.Value!.SuggestDisplay;

        var saveResult = await _cosmosDbService.CreateLessonAsync(lesson);
        if (saveResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, saveResult.Error ?? "Unknown error saving lesson.");

        var indexResult = await _searchService.IndexLessonAsync(lesson);
        if (indexResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, indexResult.Error ?? "Unknown error indexing lesson.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(saveResult.Value, HttpStatusCode.Created);
        return response;
    }

    [Function("SearchLessons")]
    public async Task<HttpResponseData> SearchLessonsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<SearchLessonRequest>(body, JsonOptions);

        if (request is null)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body.");

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage)));

        var embeddingResult = await _openAIService.GenerateEmbeddingAsync(request.Query);
        if (embeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, embeddingResult.Error ?? "Unknown error generating embedding.");

        var searchResult = await _searchService.SearchLessonsAsync(
            request.Query,
            embeddingResult.Value!,
            request.DateFrom,
            request.DateTo,
            request.MinScore,
            request.PageNumber,
            request.PageSize);

        if (searchResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, searchResult.Error ?? "Unknown error searching lessons.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(searchResult.Value);
        return response;
    }

    [Function("ReindexFromCosmosDb")]
    public async Task<HttpResponseData> ReindexFromCosmosDbAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var lessonsResult = await _cosmosDbService.GetLessonsAsync();
        if (lessonsResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, lessonsResult.Error ?? "Error fetching lessons from Cosmos DB.");

        var lessons = lessonsResult.Value!;

        if (lessons.Count == 0)
        {
            var emptyResponse = req.CreateResponse(HttpStatusCode.OK);
            await emptyResponse.WriteAsJsonAsync(new { Total = 0, Reindexed = 0, Failed = 0 });
            return emptyResponse;
        }

        var indexResult = await _searchService.IndexLessonsAsync(lessons);
        if (indexResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, indexResult.Error ?? "Error reindexing lessons.");

        var (reindexed, failed) = indexResult.Value;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Total = lessons.Count, Reindexed = reindexed, Failed = failed });
        return response;
    }

    [Function("RecreateIndex")]
    public async Task<HttpResponseData> RecreateIndexAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var result = await _searchService.RecreateIndexAsync();

        if (result.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error ?? "Unknown error recreating index.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Index recreated successfully.");
        return response;
    }

    [Function("SuggestLessons")]
    public async Task<HttpResponseData> SuggestLessonsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<SuggestLessonRequest>(body, JsonOptions);

        if (request is null || string.IsNullOrWhiteSpace(request.Query))
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Query is required.");

        var suggestResult = await _searchService.SuggestLessonsAsync(request.Query, request.Size);
        if (suggestResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, suggestResult.Error ?? "Unknown error fetching suggestions.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(suggestResult.Value);
        return response;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode code, string message)
    {
        var response = req.CreateResponse(code);
        await response.WriteStringAsync(message);
        return response;
    }
}
