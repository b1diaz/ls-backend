using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using LeccionesAprendidas.Models;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Services;

public class SearchService
{
    private readonly SearchIndexClient _adminClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;

    public SearchService(IOptions<AzureSearchOptions> options)
    {
        var config = options.Value;

        var endpoint = new Uri(config.Endpoint ?? throw new ArgumentNullException(nameof(config.Endpoint)));
        var credential = new AzureKeyCredential(config.AdminKey ?? throw new ArgumentNullException(nameof(config.AdminKey)));

        _indexName = config.IndexName ?? throw new ArgumentNullException(nameof(config.IndexName));
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
                    AnalyzerName = LexicalAnalyzerName.EnLucene
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
                    AnalyzerName = LexicalAnalyzerName.EnLucene
                },
                new SearchableField("consequences")
                {
                    AnalyzerName = LexicalAnalyzerName.EnLucene
                },
                new SearchableField("lesson")
                {
                    AnalyzerName = LexicalAnalyzerName.EnLucene
                },
                new SearchableField("searchContent")
                {
                    AnalyzerName = LexicalAnalyzerName.EnLucene
                },
                new SimpleField("dateTime", SearchFieldDataType.DateTimeOffset)
                {
                    IsFilterable = true,
                    IsSortable = true
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
            
            // Verificar si el índice tiene la estructura correcta (debe tener los campos de embedding nuevos)
            var requiredFields = new[] { "descriptionEmbedding", "analysisEmbedding", "consequencesEmbedding", "lessonEmbedding" };
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
                return Result.Failure($"Error al crear el índice: {createEx.Message}");
            }
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error al verificar el índice: {ex.Message}");
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
            return Result.Failure($"Error al recrear el índice: {ex.Message}");
        }
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
                return Result.Failure($"Fallo al indexar uno o más documentos: {errorMsg}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Excepción al indexar documento: {ex.Message}");
        }
    }


    public async Task<Result<PaginatedSearchResult>> SearchLessonsAsync(
        string queryText, 
        float[] queryEmbedding, 
        SearchFieldType searchField,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        double? minScore = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            // Determinar el campo de embedding según el tipo de búsqueda
            string embeddingField = searchField switch
            {
                SearchFieldType.Description => "descriptionEmbedding",
                SearchFieldType.Analysis => "analysisEmbedding",
                SearchFieldType.Consequences => "consequencesEmbedding",
                SearchFieldType.Lesson => "lessonEmbedding",
                _ => throw new ArgumentException($"Campo de búsqueda no válido: {searchField}")
            };

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

            // Configurar búsqueda vectorial
            options.VectorSearch ??= new VectorSearchOptions();

            // KNearestNeighborsCount debe ser al menos el tamaño de página solicitado
            // Para mejores resultados, podemos usar un múltiplo del tamaño de página
            // pero por ahora usamos el tamaño de página directamente
            options.VectorSearch.Queries.Add(new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = take,
                Fields = { embeddingField }
            });

            var response = await _searchClient.SearchAsync<LessonLearned>("", options);
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
            return Result<PaginatedSearchResult>.Failure($"Error al buscar lecciones: {ex.Message}");
        }
    }

}