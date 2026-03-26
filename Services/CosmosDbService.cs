using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly Container _container;

    public CosmosDbService(IOptions<CosmosDbOptions> options)
    {
        var config = options.Value;

        var endpoint = config.Endpoint ?? throw new ArgumentNullException(nameof(config.Endpoint));
        var key = config.Key ?? throw new ArgumentNullException(nameof(config.Key));
        var databaseName = config.DatabaseName ?? throw new ArgumentNullException(nameof(config.DatabaseName));
        var containerName = config.ContainerName ?? throw new ArgumentNullException(nameof(config.ContainerName));

        var client = new CosmosClient(endpoint, key);
        _container = client.GetContainer(databaseName, containerName);
    }

    public async Task<Result<LessonLearned>> CreateLessonAsync(LessonLearned lesson)
    {
        try
        {
            var response = await _container.CreateItemAsync(lesson);
            return Result<LessonLearned>.Success(response.Resource);
        }
        catch (CosmosException ex)
        {
            return Result<LessonLearned>.Failure($"Cosmos DB error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<LessonLearned>.Failure($"Unexpected error saving lesson: {ex.Message}");
        }
    }


    public async Task<Result<LessonLearned>> GetLessonByIdAsync(string id)
    {
        try
        {
            var query = _container.GetItemQueryIterator<LessonLearned>(
                new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id));

            while (query.HasMoreResults)
            {
                var page = await query.ReadNextAsync();
                var lesson = page.FirstOrDefault();
                if (lesson != null)
                    return Result<LessonLearned>.Success(lesson);
            }

            return Result<LessonLearned>.Failure("NOT_FOUND");
        }
        catch (Exception ex)
        {
            return Result<LessonLearned>.Failure($"Unexpected error fetching lesson: {ex.Message}");
        }
    }

    public async Task<Result<List<LessonLearned>>> GetLessonsAsync()
    {
        var query = _container.GetItemQueryIterator<LessonLearned>();
        var results = new List<LessonLearned>();

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response.ToList());
        }

        return Result<List<LessonLearned>>.Success(results);
    }
}
