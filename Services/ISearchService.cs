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
        int pageSize,
        string field = "searchContent",
        LessonSource? source = null);
    Task<Result<List<SuggestionResult>>> SuggestLessonsAsync(string queryText, float[] queryEmbedding, int size = 5, string field = "searchContent", LessonSource? source = null);
}
