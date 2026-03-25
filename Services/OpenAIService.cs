using Azure;
using Azure.AI.OpenAI;
using LeccionesAprendidas.Models;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Services;

public class OpenAIService : IOpenAIService
{
    private readonly OpenAIClient _client;
    private readonly string _embeddingDeployment;
    private readonly string _chatDeployment;

    public OpenAIService(IOptions<OpenAIOptions> options)
    {
        var config = options.Value;

        var endpoint = new Uri(config.Endpoint ?? throw new ArgumentNullException(nameof(config.Endpoint)));
        var key = config.Key ?? throw new ArgumentNullException(nameof(config.Key));

        _client = new OpenAIClient(endpoint, new AzureKeyCredential(key));
        _embeddingDeployment = config.EmbeddingDeployment;
        _chatDeployment = config.ChatDeployment;
    }

    public async Task<Result<float[]>> GenerateEmbeddingAsync(string text)
    {
        var options = new EmbeddingsOptions(_embeddingDeployment, [text]);
        var response = await _client.GetEmbeddingsAsync(options);
        var embedding = response.Value.Data[0].Embedding.ToArray();

        return Result<float[]>.Success(embedding);
    }

    public async Task<Result<LessonEnrichment>> GenerateLessonEnrichmentAsync(string text)
    {
        try
        {
            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = _chatDeployment,
                Messages =
                {
                    new ChatRequestSystemMessage(
                        "Eres un experto en seguridad industrial y gestión del conocimiento. " +
                        "Tu tarea es leer el contenido de una lección aprendida y generar UNA SOLA FRASE en español " +
                        "que sirva como etiqueta de búsqueda y autocompletado para esa lección. " +
                        "El contenido incluye: descripción del evento, análisis de causas, consecuencias, " +
                        "aprendizaje generado, tipo de situación, ubicación y cargo del involucrado. " +
                        "La frase debe capturar los términos clave que un profesional usaría al buscar eventos similares: " +
                        "el tipo de evento o peligro, la causa principal, el área o cargo involucrado, y el lugar. " +
                        "Piensa en la frase como la respuesta a: '¿Qué escribiría alguien que vivió algo parecido y quiere encontrar esta lección?'. " +
                        "Máximo 150 caracteres. " +
                        "Responde ÚNICAMENTE con la frase, sin comillas, sin puntuación final, sin explicaciones, sin formato."),
                    new ChatRequestUserMessage(text)
                },
                MaxTokens = 200,
                Temperature = 0
            };

            var response = await _client.GetChatCompletionsAsync(chatOptions);
            var suggestDisplay = response.Value.Choices[0].Message.Content?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(suggestDisplay))
                throw new InvalidOperationException("El modelo no devolvió texto válido.");

            return Result<LessonEnrichment>.Success(new LessonEnrichment(suggestDisplay));
        }
        catch (Exception ex)
        {
            return Result<LessonEnrichment>.Failure($"Error generating lesson enrichment: {ex.Message}");
        }
    }
}

