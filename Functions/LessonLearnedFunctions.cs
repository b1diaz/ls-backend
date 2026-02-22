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
    private readonly OpenAIService _openAIService;
    private readonly CosmosDbService _cosmosDbService;
    private readonly SearchService _searchService;
    private readonly IValidator<CreateLessonRequest> _createValidator;
    private readonly IValidator<SearchLessonRequest> _searchValidator;

    public LessonLearnedFunctions(
        OpenAIService openAIService,
        CosmosDbService cosmosDbService,
        SearchService searchService,
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
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Cuerpo de solicitud inválido.");

        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage)));

        // Generar embeddings para cada campo específico
        var descriptionEmbeddingResult = await _openAIService.GenerateEmbeddingAsync(request.Description);
        if (descriptionEmbeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, descriptionEmbeddingResult.Error ?? "Error desconocido al generar el embedding de descripción.");

        var analysisEmbeddingResult = await _openAIService.GenerateEmbeddingAsync(request.Analysis);
        if (analysisEmbeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, analysisEmbeddingResult.Error ?? "Error desconocido al generar el embedding de análisis.");

        var consequencesEmbeddingResult = await _openAIService.GenerateEmbeddingAsync(request.Consequences);
        if (consequencesEmbeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, consequencesEmbeddingResult.Error ?? "Error desconocido al generar el embedding de consecuencias.");

        var lessonEmbeddingResult = await _openAIService.GenerateEmbeddingAsync(request.LessonLearned);
        if (lessonEmbeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, lessonEmbeddingResult.Error ?? "Error desconocido al generar el embedding de aprendizaje.");

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
            DateTime = request.DateTime,
            DescriptionEmbedding = descriptionEmbeddingResult.Value!,
            AnalysisEmbedding = analysisEmbeddingResult.Value!,
            ConsequencesEmbedding = consequencesEmbeddingResult.Value!,
            LessonEmbedding = lessonEmbeddingResult.Value!
        };

        var saveResult = await _cosmosDbService.CreateLessonAsync(lesson);
        if (saveResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, saveResult.Error ?? "Error desconocido al crear leccion.");

        var indexResult = await _searchService.IndexLessonAsync(lesson);
        if (indexResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, indexResult.Error ?? "Error desconocido al crear crear indice.");

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(saveResult.Value);
        return response;
    }

    [Function("SearchLessons")]
    public async Task<HttpResponseData> SearchLessonsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<SearchLessonRequest>(body, JsonOptions);

        if (request is null)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Cuerpo de solicitud inválido.");

        var validation = await _searchValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return await CreateErrorResponse(req, HttpStatusCode.BadRequest, string.Join(" | ", validation.Errors.Select(e => e.ErrorMessage)));

        var embeddingResult = await _openAIService.GenerateEmbeddingAsync(request.Query);
        if (embeddingResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, embeddingResult.Error ?? "Error desconocido al generar el embedding.");

        var searchResult = await _searchService.SearchLessonsAsync(
            request.Query, 
            embeddingResult.Value!,
            request.SearchField,
            request.DateFrom,
            request.DateTo,
            request.MinScore,
            request.PageNumber,
            request.PageSize);
        
        if (searchResult.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, searchResult.Error ?? "Error desconocido al buscar leccion.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(searchResult.Value);
        return response;
    }

    [Function("RecreateIndex")]
    public async Task<HttpResponseData> RecreateIndexAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var result = await _searchService.RecreateIndexAsync();
        
        if (result.IsError)
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, result.Error ?? "Error desconocido al recrear el índice.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Índice recreado exitosamente.");
        return response;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode code, string message)
    {
        var response = req.CreateResponse(code);
        await response.WriteStringAsync(message);
        return response;
    }
}
