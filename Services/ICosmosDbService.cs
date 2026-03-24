using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Services;

public interface ICosmosDbService
{
    Task<Result<LessonLearned>> CreateLessonAsync(LessonLearned lesson);
    Task<Result<List<LessonLearned>>> GetLessonsAsync();
}
