using Newtonsoft.Json;
using System.Text.Json.Serialization;


namespace LeccionesAprendidas.Models;

public class LessonLearned
{
    /// <summary>
    /// Identificador unico de la leccion aprendida.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Codigo o numero unico del evento.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Descripcion detallada de lo ocurrido: que paso, en que contexto y como se presento.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de situacion observada (Near Miss, Incidente, Acto Inseguro, etc.).
    /// </summary>
    [JsonPropertyName("situationType")]
    public string SituationType { get; set; } = string.Empty;

    /// <summary>
    /// Ubicacion del evento: proyecto, area o sitio y lugar especifico.
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Cargo de la persona involucrada o del reportante si no hubo afectado directo.
    /// </summary>
    [JsonPropertyName("relatedPosition")]
    public string RelatedPosition { get; set; } = string.Empty;

    /// <summary>
    /// Analisis de causas del evento: origen, factores contribuyentes, etc.
    /// </summary>
    [JsonPropertyName("analysis")]
    public string Analysis { get; set; } = string.Empty;

    /// <summary>
    /// Consecuencias reales o potenciales del evento (personas, equipos, procesos, ambiente, etc.).
    /// </summary>
    [JsonPropertyName("consequences")]
    public string Consequences { get; set; } = string.Empty;

    /// <summary>
    /// Aprendizaje generado a partir del evento, incluyendo su tipo (preventivo, correctivo, mejora, etc.).
    /// </summary>
    [JsonPropertyName("lesson")]
    public string Lesson { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora en que ocurrio o fue detectado el evento.
    /// </summary>
    [JsonPropertyName("dateTime")]
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Texto combinado con los campos de contenido principal, usado para busqueda semantica e indexacion.
    /// </summary>
    [JsonPropertyName("searchContent")]
    public string SearchContent =>
        $"{Consequences}. {Description}. {Analysis}. {Lesson}.";

    /// <summary>
    /// Texto para el autocompletado (Suggester), generado al registrar la leccion.
    /// </summary>
    [JsonPropertyName("suggestDisplay")]
    public string SuggestDisplay { get; set; } = string.Empty;

    /// <summary>
    /// Representacion numerica de searchContent (embedding) para comparacion semantica mediante IA.
    /// </summary>
    [JsonPropertyName("searchContentEmbedding")]
    public float[] SearchContentEmbedding { get; set; } = [];
}

public class SearchResult
{
    /// <summary>
    /// Leccion aprendida que coincide con la busqueda.
    /// </summary>
    public LessonLearned Lesson { get; set; } = new LessonLearned();

    /// <summary>
    /// Puntuacion de similitud o relevancia entre la consulta y el contenido (0 a 1).
    /// </summary>
    public double Score { get; set; }
}

public record LessonEnrichment(string SuggestDisplay);

public class PaginatedSearchResult
{
    /// <summary>
    /// Lista de resultados de la búsqueda para la página actual.
    /// </summary>
    public List<SearchResult> Results { get; set; } = new List<SearchResult>();

    /// <summary>
    /// Número total de resultados que coinciden con la búsqueda (sin paginación).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Número de página actual (base 1).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Cantidad de resultados por página.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Número total de páginas disponibles.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
