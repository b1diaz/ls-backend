using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Services;

public interface IOpenAIService
{
    Task<Result<float[]>> GenerateEmbeddingAsync(string text);
}
