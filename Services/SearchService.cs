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
    private readonly SearchIndexerClient _indexerClient;
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
        _indexerClient = new SearchIndexerClient(endpoint, credential);
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
                // Frases clave extraidas por el skillset de Azure AI Search
                new SearchField("keyPhrases", SearchFieldDataType.Collection(SearchFieldDataType.String))
                {
                    IsSearchable = true,
                    IsFilterable = true
                },
                // Texto para autocompletado, generado por el custom skill de Azure AI Search
                new SearchableField("suggestDisplay")
                {
                    IsFilterable = false,
                    IsSortable = false
                },
                // Embedding unificado de searchContent para busqueda vectorial
                new SearchField("searchContentEmbedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 3072,
                    VectorSearchProfileName = "vector-profile-1"
                }
            },
            Suggesters =
            {
                new SearchSuggester("suggester-1", new[] { "suggestDisplay" })
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
            var requiredFields = new[] { "searchContentEmbedding", "keyPhrases", "suggestDisplay" };
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

            // Create/update indexer pipeline (data source, skillset, indexer)
            var pipelineResult = await CreateOrUpdateIndexerPipelineAsync();
            if (pipelineResult.IsError)
                return Result.Failure($"Index recreated but pipeline failed: {pipelineResult.Error}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error recreating index: {ex.Message}");
        }
    }

    public async Task<Result> CreateOrUpdateIndexerPipelineAsync()
    {
        try
        {
            // 1. Data source → Cosmos DB
            var dataSource = new SearchIndexerDataSourceConnection(
                _config.DataSourceName,
                SearchIndexerDataSourceType.CosmosDb,
                _config.CosmosDbConnectionString,
                new SearchIndexerDataContainer("Lecciones"));

            await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);

            // 2. Skillset → KeyPhraseExtraction + custom WebApiSkill (FormatearSuggest)
            var keyPhraseSkill = new KeyPhraseExtractionSkill(
                inputs: new[]
                {
                    new InputFieldMappingEntry("text") { Source = "/document/searchContent" }
                },
                outputs: new[]
                {
                    new OutputFieldMappingEntry("keyPhrases") { TargetName = "key_phrases" }
                })
            {
                DefaultLanguageCode = KeyPhraseExtractionSkillLanguage.Es,
                Context = "/document"
            };

            var webApiSkill = new WebApiSkill(
                inputs: new[]
                {
                    new InputFieldMappingEntry("situationType") { Source = "/document/situationType" },
                    new InputFieldMappingEntry("location") { Source = "/document/location" },
                    new InputFieldMappingEntry("code") { Source = "/document/code" },
                    new InputFieldMappingEntry("keyPhrases") { Source = "/document/key_phrases" }
                },
                outputs: new[]
                {
                    new OutputFieldMappingEntry("suggestDisplay") { TargetName = "suggestDisplay" }
                },
                uri: _config.FormatearSuggestUrl)
            {
                Context = "/document",
                BatchSize = 1,
                DegreeOfParallelism = 1
            };

            var skillset = new SearchIndexerSkillset(_config.SkillsetName, new SearchIndexerSkill[] { keyPhraseSkill, webApiSkill })
            {
                CognitiveServicesAccount = new CognitiveServicesAccountKey(_config.CognitiveServicesKey)
            };

            await _indexerClient.CreateOrUpdateSkillsetAsync(skillset);

            // 3. Indexer → links data source + index + skillset
            var indexer = new SearchIndexer(_config.IndexerName, _config.DataSourceName, _indexName)
            {
                SkillsetName = _config.SkillsetName,
                OutputFieldMappings =
                {
                    new FieldMapping("/document/key_phrases") { TargetFieldName = "keyPhrases" },
                    new FieldMapping("/document/suggestDisplay") { TargetFieldName = "suggestDisplay" }
                }
            };

            await _indexerClient.CreateOrUpdateIndexerAsync(indexer);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error configuring indexer pipeline: {ex.Message}");
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


    public async Task<Result<PaginatedSearchResult>> SearchLessonsAsync(
        string queryText,
        float[] queryEmbedding,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        double? minScore = null,
        int pageNumber = 1,
        int pageSize = 10)
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
                Select = { "id", "code", "description", "lesson", "situationType", "location", "relatedPosition", "analysis", "consequences", "dateTime", "searchContent", "keyPhrases", "suggestDisplay" }
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

            // Configurar búsqueda vectorial siempre sobre searchContentEmbedding
            options.VectorSearch ??= new VectorSearchOptions();

            // KNearestNeighborsCount debe cubrir todos los candidatos necesarios antes de aplicar Skip+Size
            options.VectorSearch.Queries.Add(new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = skip + take,
                Fields = { "searchContentEmbedding" }
            });

            var response = await _searchClient.SearchAsync<LessonLearned>(queryText, options);
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

    public async Task<Result> TriggerIndexerAsync()
    {
        try
        {
            // Ejecutar sin reset: el indexer usa change tracking y procesa solo documentos nuevos
            await _indexerClient.RunIndexerAsync(_config.IndexerName);
            return Result.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // El indexer ya esta corriendo, no es un error
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error triggering indexer: {ex.Message}");
        }
    }

    public async Task<Result<List<string>>> SuggestLessonsAsync(string queryText, int size = 5)
    {
        try
        {
            var options = new SuggestOptions
            {
                Size = size,
                UseFuzzyMatching = true
            };

            var response = await _searchClient.SuggestAsync<LessonLearned>(queryText, "suggester-1", options);
            var suggestions = response.Value.Results
                .Select(r => r.Text)
                .ToList();

            return Result<List<string>>.Success(suggestions);
        }
        catch (Exception ex)
        {
            return Result<List<string>>.Failure($"Error fetching suggestions: {ex.Message}");
        }
    }

}