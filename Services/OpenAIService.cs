using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using LeccionesAprendidas.Models;
using Microsoft.Extensions.Options;

namespace LeccionesAprendidas.Services;

public class OpenAIService : IOpenAIService
{
    private static readonly JsonSerializerOptions EnrichmentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
                        "Act as a Precise Data Extractor and Safety Expert. Your ONLY goal is to generate a SPANISH 'suggestDisplay' string (MAX 80 chars). " +
                        "You MUST strictly REPLACE the placeholders in this template with actual JSON values: " +
                        "'[SituationType] ([Technical Category]) por [Analysis Summary] en [Location] | [Consequences] ([Code])'. " +
                        "STRICT INSTRUCTIONS: " +
                        "1. [Code]: Use the LITERAL value of the 'Code' field (e.g., DP-24-01-01). NEVER output the word 'Código' or '[Code]'. " +
                        "2. [Location]: Use only the city or plant name from the 'Location' field. " +
                        "3. Keep original worker vocabulary (e.g., 'Geta', 'Susto', 'Machucón') for the [SituationType] part. " +
                        "4. [Analysis Summary]: Summarize the 'Analysis' field in 3 words maximum. " +
                        "5. Response must be ONLY a JSON object: { \"suggestDisplay\": \"...\" }."),
                    new ChatRequestUserMessage(text)
                },
                MaxTokens = 200,
                Temperature = 0
            };

            var response = await _client.GetChatCompletionsAsync(chatOptions);
            var content = response.Value.Choices[0].Message.Content;
            var json = ExtractJsonObject(content);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Respuesta vacía del modelo.");

            var dto = JsonSerializer.Deserialize<EnrichmentResponse>(json, EnrichmentJsonOptions)
                ?? throw new InvalidOperationException("Respuesta vacía del modelo.");

            var suggestDisplay = (dto.SuggestDisplay ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(suggestDisplay))
                throw new InvalidOperationException("El modelo no devolvió suggestDisplay válido.");

            return Result<LessonEnrichment>.Success(new LessonEnrichment(suggestDisplay));
        }
        catch (Exception ex)
        {
            return Result<LessonEnrichment>.Failure($"Error generating lesson enrichment: {ex.Message}");
        }
    }

    /// <summary>
    /// El modelo suele envolver JSON en bloques Markdown (```json ... ```) o añadir texto alrededor.
    /// </summary>
    private static string ExtractJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();

        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var lineBreak = s.IndexOf('\n');
            s = lineBreak >= 0 ? s[(lineBreak + 1)..].TrimStart() : s[3..].TrimStart();
            var closing = s.LastIndexOf("```", StringComparison.Ordinal);
            if (closing >= 0)
                s = s[..closing].Trim();
        }

        var start = s.IndexOf('{');
        if (start < 0)
            return s;

        var depth = 0;
        for (var i = start; i < s.Length; i++)
        {
            if (s[i] == '{')
                depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return s[start..(i + 1)];
            }
        }

        return s[start..];
    }

    private record EnrichmentResponse([property: JsonPropertyName("suggestDisplay")] string? SuggestDisplay);
}

