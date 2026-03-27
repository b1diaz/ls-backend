using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using LeccionesAprendidas.Models;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Services;

public class SearchService : ISearchService
{
    private readonly SearchIndexClient _adminClient;
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _config;
    private readonly string _indexName;

    public SearchService(IOptions<AzureSearchOptions> options)
    {
        _config = options.Value;

        var endpoint = new Uri(_config.Endpoint ?? throw new ArgumentNullException(nameof(_config.Endpoint)));
        var credential = new AzureKeyCredential(_config.AdminKey ?? throw new ArgumentNullException(nameof(_config.AdminKey)));

        _indexName = _config.IndexName ?? throw new ArgumentNullException(nameof(_config.IndexName));
        _adminClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _indexName, credential);
    }

    private SearchIndex CreateIndexDefinition()
    {
        return new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String)
                {
                    IsKey = true,
                    IsFilterable = true,
                    IsSortable = true
                },
                new SimpleField("code", SearchFieldDataType.String)
                {
                    IsFilterable = true,
                    IsSortable = true
                },
                new SearchableField("description")
                {
                    AnalyzerName = LexicalAnalyzerName.EsLucene
                },
                new SearchableField("situationType")
                {
                    IsFilterable = true,
                    IsSortable = true
                },
                new SearchableField("location")
                {
                    IsFilterable = true
                },
                new SearchableField("relatedPosition")
                {
                    IsFilterable = true,
                    IsSortable = true
                },
                new SearchableField("analysis")
                {
                    AnalyzerName = LexicalAnalyzerName.EsLucene
                },
                new SearchableField("consequences")
                {
                    AnalyzerName = LexicalAnalyzerName.EsLucene
                },
                new SearchableField("lesson")
                {
                    AnalyzerName = LexicalAnalyzerName.EsLucene
                },
                new SearchableField("searchContent")
                {
                    AnalyzerName = LexicalAnalyzerName.EsLucene
                },
                new SimpleField("dateTime", SearchFieldDataType.DateTimeOffset)
                {
                    IsFilterable = true,
                    IsSortable = true
                },
                // Embeddings vectoriales por campo
                new SearchField("searchContentEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                },
                new SearchField("descriptionEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                },
                new SearchField("analysisEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                },
                new SearchField("consequencesEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                },
                new SearchField("lessonEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                }
            },
            VectorSearch = new VectorSearch
            {
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("hnsw-1")
                    {
                        Parameters = new HnswParameters
                        {
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500,
                            Metric = VectorSearchAlgorithmMetric.Cosine
                        }
                    }
                },
                Profiles =
                {
                    new VectorSearchProfile("vector-profile-1", "hnsw-1")
                }
            }
        };
    }

    public async Task<Result> CreateIndexIfNotExistsAsync()
    {
        try
        {
            var existingIndex = await _adminClient.GetIndexAsync(_indexName);
            
            // Verificar si el índice tiene la estructura correcta
            var requiredFields = new[] { "searchContentEmbedding", "descriptionEmbedding", "analysisEmbedding", "consequencesEmbedding", "lessonEmbedding" };
            var existingFieldNames = existingIndex.Value.Fields.Select(f => f.Name).ToHashSet();
            
            bool hasRequiredFields = requiredFields.All(field => existingFieldNames.Contains(field));
            
            if (!hasRequiredFields)
            {
                // El índice existe pero tiene estructura antigua, recrearlo
                return await RecreateIndexAsync();
            }
            
            return Result.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // El índice no existe, crearlo
            try
            {
                var index = CreateIndexDefinition();
                await _adminClient.CreateIndexAsync(index);
                return Result.Success();
            }
            catch (Exception createEx)
            {
                return Result.Failure($"Error creating index: {createEx.Message}");
            }
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error verifying index: {ex.Message}");
        }
    }

    public async Task<Result> RecreateIndexAsync()
    {
        try
        {
            // Intentar eliminar el índice si existe
            try
            {
                await _adminClient.DeleteIndexAsync(_indexName);
                // Esperar un momento para asegurar que el índice se haya eliminado completamente
                await Task.Delay(1000);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // El índice no existe, continuar con la creación
            }

            // Crear el índice con la nueva estructura
            var index = CreateIndexDefinition();
            await _adminClient.CreateIndexAsync(index);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error recreating index: {ex.Message}");
        }
    }


    public async Task<Result<(int Reindexed, int Failed)>> IndexLessonsAsync(List<LessonLearned> lessons)
    {
        const int batchSize = 1000;
        int reindexed = 0;
        int failed = 0;

        for (int i = 0; i < lessons.Count; i += batchSize)
        {
            var batch = lessons.Skip(i).Take(batchSize).ToList();

            try
            {
                var indexBatch = IndexDocumentsBatch.Upload(batch);
                var response = await _searchClient.IndexDocumentsAsync(indexBatch);

                foreach (var result in response.Value.Results)
                {
                    if (result.Succeeded) reindexed++;
                    else failed++;
                }
            }
            catch (Exception ex)
            {
                return Result<(int, int)>.Failure($"Error processing batch {i / batchSize + 1}: {ex.Message}");
            }
        }

        return Result<(int, int)>.Success((reindexed, failed));
    }

    public async Task<Result> IndexLessonAsync(LessonLearned lesson)
    {
        try
        {
            var batch = IndexDocumentsBatch.Upload(new[] { lesson });
            var response = await _searchClient.IndexDocumentsAsync(batch);

            var failures = response.Value.Results.Where(r => !r.Succeeded).ToList();

            if (failures.Any())
            {
                var errorMsg = string.Join(" | ", failures.Select(f =>
                    $"Id: {f.Key}, Error: {f.ErrorMessage}"));
                return Result.Failure($"Failed to index one or more documents: {errorMsg}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Exception indexing document: {ex.Message}");
        }
    }


    private static readonly Dictionary<string, string> FieldToEmbedding = new()
    {
        ["searchContent"] = "searchContentEmbedding",
        ["description"]   = "descriptionEmbedding",
        ["analysis"]      = "analysisEmbedding",
        ["consequences"]  = "consequencesEmbedding",
        ["lesson"]        = "lessonEmbedding"
    };

    public async Task<Result<PaginatedSearchResult>> SearchLessonsAsync(
        string queryText,
        float[] queryEmbedding,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        double? minScore = null,
        int pageNumber = 1,
        int pageSize = 10,
        string field = "searchContent")
    {
        try
        {

            // Validar y ajustar parámetros de paginación
            var validPageSizes = new[] { 10, 25, 50, 100 };
            var validPageSize = validPageSizes.Contains(pageSize) ? pageSize : 10;
            var validPageNumber = pageNumber < 1 ? 1 : pageNumber;
            var skip = (validPageNumber - 1) * validPageSize;
            var take = validPageSize;

            // Para obtener el total, necesitamos hacer una búsqueda sin límite primero
            // Azure Search no proporciona TotalCount directamente, así que usaremos IncludeTotalCount
            var options = new SearchOptions
            {
                Size = take,
                Skip = skip,
                IncludeTotalCount = true,
                Select = { "id", "code", "description", "lesson", "situationType", "location", "relatedPosition", "analysis", "consequences", "dateTime", "searchContent" }
            };

            // Construir filtro OData para fechas
            var filters = new List<string>();
            
            if (dateFrom.HasValue)
            {
                var dateFromOffset = dateFrom.Value.Kind == DateTimeKind.Unspecified 
                    ? new DateTimeOffset(dateFrom.Value, TimeSpan.Zero)
                    : new DateTimeOffset(dateFrom.Value);
                // Formato OData para DateTimeOffset: yyyy-MM-ddTHH:mm:ss.fffZ
                filters.Add($"dateTime ge {dateFromOffset:O}");
            }
            
            if (dateTo.HasValue)
            {
                var dateToOffset = dateTo.Value.Kind == DateTimeKind.Unspecified 
                    ? new DateTimeOffset(dateTo.Value, TimeSpan.Zero)
                    : new DateTimeOffset(dateTo.Value);
                // Formato OData para DateTimeOffset: yyyy-MM-ddTHH:mm:ss.fffZ
                filters.Add($"dateTime le {dateToOffset:O}");
            }

            if (filters.Any())
            {
                options.Filter = string.Join(" and ", filters);
            }

            // Seleccionar embedding del campo solicitado
            var embeddingField = FieldToEmbedding.TryGetValue(field, out var ef) ? ef : "searchContentEmbedding";

            // KNearestNeighborsCount alto para que Azure Search evalúe suficientes candidatos
            // y TotalCount refleje el total real, no solo los de la página actual
            const int maxCandidates = 1000;

            options.VectorSearch ??= new VectorSearchOptions();
            options.VectorSearch.Queries.Add(new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = Math.Max(maxCandidates, skip + take),
                Fields = { embeddingField }
            });

            var response = await _searchClient.SearchAsync<LessonLearned>(null, options);
            var results = response.Value.GetResults();
            var totalCount = response.Value.TotalCount ?? 0;

            // Aplicar filtro de score mínimo (Azure Search devuelve scores de 0 a 1)
            var minScoreValue = minScore ?? 0.0;
            var mapped = results
                .Select(r =>
                {
                    var score = Math.Round(r.Score ?? 0.0, 4);
                    return new SearchResult
                    {
                        Lesson = r.Document,
                        Score = score
                    };
                })
                .Where(r => r.Score >= minScoreValue)
                .ToList();

            // Nota: El TotalCount de Azure Search incluye todos los resultados antes del filtro de MinScore
            // Si necesitamos un TotalCount más preciso, tendríamos que hacer una segunda búsqueda sin paginación
            // Por ahora, usamos el TotalCount aproximado de Azure Search
            var paginatedResult = new PaginatedSearchResult
            {
                Results = mapped,
                TotalCount = (int)totalCount,
                PageNumber = validPageNumber,
                PageSize = validPageSize
            };

            return Result<PaginatedSearchResult>.Success(paginatedResult);
        }
        catch (Exception ex)
        {
            return Result<PaginatedSearchResult>.Failure($"Error searching lessons: {ex.Message}");
        }
    }


    private static readonly HashSet<string> AllowedHighlightFields =
        new() { "searchContent", "consequences", "description", "analysis", "lesson" };

    // Extrae una ventana de contexto de ~windowSize chars alrededor del primer <mark>
    private static string TrimHighlight(string highlight, int windowSize = 180)
    {
        var markStart = highlight.IndexOf("<mark>", StringComparison.Ordinal);
        if (markStart < 0)
            return highlight.Length <= windowSize ? highlight : highlight[..windowSize] + "…";

        // Centro de la ventana: mitad antes del <mark>, mitad después del </mark>
        var half = windowSize / 2;
        var start = Math.Max(0, markStart - half);
        var end = Math.Min(highlight.Length, markStart + windowSize);

        var snippet = highlight[start..end];

        // Añadir elipsis si el snippet no empieza/termina en el extremo del texto
        if (start > 0) snippet = "…" + snippet.TrimStart();
        if (end < highlight.Length) snippet = snippet.TrimEnd() + "…";

        return snippet;
    }

    private static string GetFieldContent(LessonLearned lesson, string field) => field switch
    {
        "description"  => lesson.Description,
        "analysis"     => lesson.Analysis,
        "consequences" => lesson.Consequences,
        "lesson"       => lesson.Lesson,
        _              => lesson.SearchContent
    };

    // Extrae un fragmento del texto plano alrededor de la primera aparición de cualquier palabra de la query
    private static string ExtractFallbackExcerpt(string? text, string queryText, int windowSize = 180)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var idx = -1;
        foreach (var word in queryText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            idx = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) break;
        }

        // Sin coincidencia de texto → mostrar inicio del campo (match fue semántico)
        if (idx < 0)
            return text.Length <= windowSize ? text : text[..windowSize] + "…";

        var start = Math.Max(0, idx - windowSize / 2);
        var end   = Math.Min(text.Length, idx + windowSize);
        var snippet = text[start..end];
        if (start > 0) snippet = "…" + snippet.TrimStart();
        if (end < text.Length) snippet = snippet.TrimEnd() + "…";
        return snippet;
    }

    public async Task<Result<List<SuggestionResult>>> SuggestLessonsAsync(string queryText, float[] queryEmbedding, int size = 5, string field = "searchContent")
    {
        try
        {
            var searchField    = AllowedHighlightFields.Contains(field) ? field : "searchContent";
            var embeddingField = FieldToEmbedding.TryGetValue(searchField, out var ef) ? ef : "searchContentEmbedding";

            var options = new SearchOptions
            {
                Size = size,
                HighlightPreTag = "<mark>",
                HighlightPostTag = "</mark>"
            };
            options.Select.Add("id");
            options.Select.Add("code");
            options.Select.Add(searchField);          // necesario para el fallback excerpt
            options.HighlightFields.Add(searchField);
            options.SearchFields.Add(searchField);    // texto solo busca en el campo seleccionado

            // Búsqueda híbrida: texto (BM25 restringido al campo) + vector (embedding del campo)
            options.VectorSearch = new VectorSearchOptions();
            options.VectorSearch.Queries.Add(new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = size,
                Fields = { embeddingField }
            });

            var response = await _searchClient.SearchAsync<LessonLearned>(queryText, options);
            var results = response.Value.GetResults()
                .Select(r =>
                {
                    var hasHighlight = r.Highlights != null
                        && r.Highlights.TryGetValue(searchField, out var hl)
                        && hl.Count > 0;

                    return new SuggestionResult
                    {
                        Id   = r.Document.Id,
                        Code = r.Document.Code,
                        Highlights = hasHighlight
                            ? r.Highlights![searchField].Select(h => TrimHighlight(h)).ToList()
                            : new List<string> { ExtractFallbackExcerpt(GetFieldContent(r.Document, searchField), queryText) }
                    };
                })
                .ToList();

            return Result<List<SuggestionResult>>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<List<SuggestionResult>>.Failure($"Error fetching suggestions: {ex.Message}");
        }
    }

}