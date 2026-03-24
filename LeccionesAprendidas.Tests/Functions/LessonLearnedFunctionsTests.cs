using System.Net;
using FluentValidation;
using FluentValidation.Results;
using LeccionesAprendidas.Functions;
using LeccionesAprendidas.Models;
using LeccionesAprendidas.Services;
using LeccionesAprendidas.Tests.Helpers;
using Moq;

namespace LeccionesAprendidas.Tests.Functions;

public class LessonLearnedFunctionsTests
{
    private readonly Mock<IOpenAIService> _openAI = new();
    private readonly Mock<ICosmosDbService> _cosmos = new();
    private readonly Mock<ISearchService> _search = new();
    private readonly Mock<IValidator<CreateLessonRequest>> _createValidator = new();
    private readonly Mock<IValidator<SearchLessonRequest>> _searchValidator = new();

    private LessonLearnedFunctions CreateSut() =>
        new(_openAI.Object, _cosmos.Object, _search.Object,
            _createValidator.Object, _searchValidator.Object);

    private static readonly float[] FakeEmbedding = [0.1f, 0.2f, 0.3f];

    private static readonly CreateLessonRequest ValidCreateRequest = new()
    {
        Description = "Descripción del evento",
        SituationType = "Near Miss",
        LessonLearned = "Lección aprendida",
        Analysis = "Análisis",
        Consequences = "Consecuencias"
    };

    private static readonly SearchLessonRequest ValidSearchRequest = new()
    {
        Query = "riesgo eléctrico",
        PageNumber = 1,
        PageSize = 10
    };

    private void SetupValidatorPass()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateLessonRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private void SetupSearchValidatorPass()
    {
        _searchValidator
            .Setup(v => v.ValidateAsync(It.IsAny<SearchLessonRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private void SetupEmbeddingSuccess()
    {
        _openAI
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<float[]>.Success(FakeEmbedding));
    }

    // ─── RegisterLesson ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterLesson_NullBody_Returns400()
    {
        var req = new FakeHttpRequestData("null");
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLesson_ValidationFails_Returns400()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateLessonRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Description", "Requerido") }));

        var req = FakeHttpRequestData.WithJson(ValidCreateRequest);
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLesson_EmbeddingFails_Returns500()
    {
        SetupValidatorPass();
        _openAI
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<float[]>.Failure("OpenAI error"));

        var req = FakeHttpRequestData.WithJson(ValidCreateRequest);
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLesson_CosmosDbFails_Returns500()
    {
        SetupValidatorPass();
        SetupEmbeddingSuccess();
        _cosmos
            .Setup(s => s.CreateLessonAsync(It.IsAny<LessonLearned>()))
            .ReturnsAsync(Result<LessonLearned>.Failure("CosmosDB error"));

        var req = FakeHttpRequestData.WithJson(ValidCreateRequest);
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLesson_SearchIndexFails_Returns500()
    {
        SetupValidatorPass();
        SetupEmbeddingSuccess();
        _cosmos
            .Setup(s => s.CreateLessonAsync(It.IsAny<LessonLearned>()))
            .ReturnsAsync(Result<LessonLearned>.Success(new LessonLearned()));
        _search
            .Setup(s => s.IndexLessonAsync(It.IsAny<LessonLearned>()))
            .ReturnsAsync(Result.Failure("Search error"));

        var req = FakeHttpRequestData.WithJson(ValidCreateRequest);
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLesson_Success_Returns201()
    {
        SetupValidatorPass();
        SetupEmbeddingSuccess();
        _cosmos
            .Setup(s => s.CreateLessonAsync(It.IsAny<LessonLearned>()))
            .ReturnsAsync(Result<LessonLearned>.Success(new LessonLearned()));
        _search
            .Setup(s => s.IndexLessonAsync(It.IsAny<LessonLearned>()))
            .ReturnsAsync(Result.Success());
        _search
            .Setup(s => s.TriggerIndexerAsync())
            .ReturnsAsync(Result.Success());

        var req = FakeHttpRequestData.WithJson(ValidCreateRequest);
        var response = await CreateSut().RegisterLessonAsync(req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─── SearchLessons ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchLessons_NullBody_Returns400()
    {
        var req = new FakeHttpRequestData("null");
        var response = await CreateSut().SearchLessonsAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLessons_ValidationFails_Returns400()
    {
        _searchValidator
            .Setup(v => v.ValidateAsync(It.IsAny<SearchLessonRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Query", "Requerida") }));

        var req = FakeHttpRequestData.WithJson(ValidSearchRequest);
        var response = await CreateSut().SearchLessonsAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLessons_EmbeddingFails_Returns500()
    {
        SetupSearchValidatorPass();
        _openAI
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<float[]>.Failure("OpenAI error"));

        var req = FakeHttpRequestData.WithJson(ValidSearchRequest);
        var response = await CreateSut().SearchLessonsAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task SearchLessons_SearchFails_Returns500()
    {
        SetupSearchValidatorPass();
        SetupEmbeddingSuccess();
        _search
            .Setup(s => s.SearchLessonsAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<double?>(),
                It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(Result<PaginatedSearchResult>.Failure("Search error"));

        var req = FakeHttpRequestData.WithJson(ValidSearchRequest);
        var response = await CreateSut().SearchLessonsAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task SearchLessons_Success_Returns200()
    {
        SetupSearchValidatorPass();
        SetupEmbeddingSuccess();
        _search
            .Setup(s => s.SearchLessonsAsync(
                It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<double?>(),
                It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(Result<PaginatedSearchResult>.Success(new PaginatedSearchResult()));

        var req = FakeHttpRequestData.WithJson(ValidSearchRequest);
        var response = await CreateSut().SearchLessonsAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── ReindexFromCosmosDb ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReindexFromCosmosDb_CosmosDbFails_Returns500()
    {
        _cosmos
            .Setup(s => s.GetLessonsAsync())
            .ReturnsAsync(Result<List<LessonLearned>>.Failure("CosmosDB error"));

        var req = new FakeHttpRequestData();
        var response = await CreateSut().ReindexFromCosmosDbAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task ReindexFromCosmosDb_EmptyDb_Returns200WithZeros()
    {
        _cosmos
            .Setup(s => s.GetLessonsAsync())
            .ReturnsAsync(Result<List<LessonLearned>>.Success(new List<LessonLearned>()));

        var req = new FakeHttpRequestData();
        var response = await CreateSut().ReindexFromCosmosDbAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fake = (FakeHttpResponseData)response;
        var body = fake.ReadBodyAsString();
        Assert.Contains("\"Total\":0", body);
        Assert.Contains("\"Reindexed\":0", body);
        Assert.Contains("\"Failed\":0", body);
    }

    [Fact]
    public async Task ReindexFromCosmosDb_Success_Returns200WithCounts()
    {
        var lessons = new List<LessonLearned> { new LessonLearned(), new LessonLearned() };
        _cosmos
            .Setup(s => s.GetLessonsAsync())
            .ReturnsAsync(Result<List<LessonLearned>>.Success(lessons));
        _search
            .Setup(s => s.IndexLessonsAsync(It.IsAny<List<LessonLearned>>()))
            .ReturnsAsync(Result<(int Reindexed, int Failed)>.Success((2, 0)));

        var req = new FakeHttpRequestData();
        var response = await CreateSut().ReindexFromCosmosDbAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fake = (FakeHttpResponseData)response;
        var body = fake.ReadBodyAsString();
        Assert.Contains("\"Total\":2", body);
        Assert.Contains("\"Reindexed\":2", body);
        Assert.Contains("\"Failed\":0", body);
    }

    // ─── SuggestLessons ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestLessons_NullBody_Returns400()
    {
        var req = new FakeHttpRequestData("null");
        var response = await CreateSut().SuggestLessonsAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuggestLessons_EmptyQuery_Returns400()
    {
        var req = FakeHttpRequestData.WithJson(new SuggestLessonRequest { Query = "" });
        var response = await CreateSut().SuggestLessonsAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuggestLessons_SuggestFails_Returns500()
    {
        _search
            .Setup(s => s.SuggestLessonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result<List<string>>.Failure("Search error"));

        var req = FakeHttpRequestData.WithJson(new SuggestLessonRequest { Query = "caída" });
        var response = await CreateSut().SuggestLessonsAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task SuggestLessons_Success_Returns200()
    {
        _search
            .Setup(s => s.SuggestLessonsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result<List<string>>.Success(new List<string> { "[EVT-001] Near Miss - Planta A | caída" }));

        var req = FakeHttpRequestData.WithJson(new SuggestLessonRequest { Query = "caída" });
        var response = await CreateSut().SuggestLessonsAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── FormatearSuggest ────────────────────────────────────────────────────────

    [Fact]
    public async Task FormatearSuggest_NullBody_Returns400()
    {
        var req = new FakeHttpRequestData("null");
        var response = await CreateSut().FormatearSuggestAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FormatearSuggest_EmptyValues_Returns400()
    {
        var req = FakeHttpRequestData.WithJson(new FormatearSuggestRequest { Values = [] });
        var response = await CreateSut().FormatearSuggestAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FormatearSuggest_WithPhrases_Returns200WithFormattedText()
    {
        var request = new FormatearSuggestRequest
        {
            Values =
            [
                new FormatearSuggestRecord
                {
                    RecordId = "1",
                    Data = new FormatearSuggestInputData
                    {
                        Code = "EVT-001",
                        SituationType = "Near Miss",
                        Location = "Planta A",
                        Phrases = ["epi", "altura"]
                    }
                }
            ]
        };

        var req = FakeHttpRequestData.WithJson(request);
        var response = await CreateSut().FormatearSuggestAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("EVT-001", body);
        Assert.Contains("Near Miss", body);
        Assert.Contains("epi", body);
        Assert.Contains("|", body);
    }

    [Fact]
    public async Task FormatearSuggest_NoPhrases_Returns200NoPipe()
    {
        var request = new FormatearSuggestRequest
        {
            Values =
            [
                new FormatearSuggestRecord
                {
                    RecordId = "2",
                    Data = new FormatearSuggestInputData
                    {
                        Code = "EVT-002",
                        SituationType = "Incidente",
                        Location = "Zona B",
                        Phrases = []
                    }
                }
            ]
        };

        var req = FakeHttpRequestData.WithJson(request);
        var response = await CreateSut().FormatearSuggestAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = ((FakeHttpResponseData)response).ReadBodyAsString();
        Assert.Contains("EVT-002", body);
        Assert.DoesNotContain("|", body);
    }

    // ─── RecreateIndex ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RecreateIndex_Fails_Returns500()
    {
        _search
            .Setup(s => s.RecreateIndexAsync())
            .ReturnsAsync(Result.Failure("Error al recrear el índice"));

        var req = new FakeHttpRequestData();
        var response = await CreateSut().RecreateIndexAsync(req);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RecreateIndex_Success_Returns200()
    {
        _search
            .Setup(s => s.RecreateIndexAsync())
            .ReturnsAsync(Result.Success());
        _search
            .Setup(s => s.CreateOrUpdateIndexerPipelineAsync())
            .ReturnsAsync(Result.Success());

        var req = new FakeHttpRequestData();
        var response = await CreateSut().RecreateIndexAsync(req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
