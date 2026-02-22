using Azure;
using Azure.AI.OpenAI;
using LeccionesAprendidas.Models;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Services;

public class OpenAIService
{
    private readonly OpenAIClient _client;
    private readonly string _embeddingDeployment;

    public OpenAIService(IOptions<OpenAIOptions> options)
    {
        var config = options.Value;

        var endpoint = new Uri(config.Endpoint ?? throw new ArgumentNullException(nameof(config.Endpoint)));
        var key = config.Key ?? throw new ArgumentNullException(nameof(config.Key));

        _client = new OpenAIClient(endpoint, new AzureKeyCredential(key));
        _embeddingDeployment = config.EmbeddingDeployment;
    }

    public async Task<Result<float[]>> GenerateEmbeddingAsync(string text)
    {
        var options = new EmbeddingsOptions(_embeddingDeployment, [text]);
        var response = await _client.GetEmbeddingsAsync(options);
        var embedding = response.Value.Data[0].Embedding.ToArray();

        return Result<float[]>.Success(embedding);
    }
}

