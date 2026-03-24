using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Services;

public interface ISearchService
{
    Task<Result> CreateIndexIfNotExistsAsync();
    Task<Result> RecreateIndexAsync();
    Task<Result> IndexLessonAsync(LessonLearned lesson);
    Task<Result<(int Reindexed, int Failed)>> IndexLessonsAsync(List<LessonLearned> lessons);
    Task<Result<PaginatedSearchResult>> SearchLessonsAsync(
        string queryText,
        float[] queryEmbedding,
        DateTime? dateFrom,
        DateTime? dateTo,
        double? minScore,
        int pageNumber,
        int pageSize);
    Task<Result<List<string>>> SuggestLessonsAsync(string queryText, int size = 5);
}
