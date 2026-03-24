using System.Text.Json.Serialization;

namespace LeccionesAprendidas.Models;

public class CreateLessonRequest
{
    /// <summary>
    /// Código o número único del evento.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora en que ocurrió o fue detectado el evento.
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Cargo de la persona involucrada o del reportante si no hubo afectado directo.
    /// </summary>
    public string RelatedPosition { get; set; } = string.Empty;

    /// <summary>
    /// Ubicación del evento: proyecto, área o sitio y lugar específico.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de situación observada (Near Miss, Incidente, Acto Inseguro, etc.).
    /// </summary>
    public string SituationType { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada de lo ocurrido: qué pasó, en qué contexto y cómo se presentó.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Análisis de causas del evento: origen, factores contribuyentes, etc.
    /// </summary>
    public string Analysis { get; set; } = string.Empty;

    /// <summary>
    /// Consecuencias reales o potenciales del evento (personas, equipos, procesos, ambiente, etc.).
    /// </summary>
    public string Consequences { get; set; } = string.Empty;

    /// <summary>
    /// Aprendizaje generado a partir del evento, incluyendo su tipo (preventivo, correctivo, mejora, etc.).
    /// </summary>
    public string LessonLearned { get; set; } = string.Empty;

    /// <summary>
    /// Campo combinado con los campos de contenido principal, usado para búsqueda semántica.
    /// </summary>
    public string SearchContent => $"{Consequences}. {Description}. {Analysis}. {LessonLearned}";
}

public class SearchLessonRequest
{
    /// <summary>
    /// Consulta de texto libre para buscar lecciones aprendidas relacionadas.
    /// Puede incluir palabras clave, descripciones o cualquier dato relevante.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Fecha desde la cual filtrar las lecciones aprendidas (opcional).
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Fecha hasta la cual filtrar las lecciones aprendidas (opcional).
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Score mínimo requerido para que una lección aprendida sea retornada (0 a 1).
    /// Si el score está por debajo de este valor, no se retorna.
    /// </summary>
    public double? MinScore { get; set; }

    /// <summary>
    /// Número de página a retornar (base 1). Por defecto es 1.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Cantidad de resultados por página. Valores permitidos: 10, 25, 50, 100. Por defecto es 10.
    /// </summary>
    public int PageSize { get; set; } = 10;
}

public class SuggestLessonRequest
{
    /// <summary>
    /// Texto parcial escrito por el usuario para obtener sugerencias de autocompletado.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad máxima de sugerencias a retornar. Por defecto es 5.
    /// </summary>
    public int Size { get; set; } = 5;
}

// ─── Modelos para el custom skill de Azure AI Search (FormatearSuggest) ───────

public class FormatearSuggestRequest
{
    public List<FormatearSuggestRecord> Values { get; set; } = [];
}

public class FormatearSuggestRecord
{
    public string RecordId { get; set; } = string.Empty;
    public FormatearSuggestInputData Data { get; set; } = new();
}

public class FormatearSuggestInputData
{
    public string SituationType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<string> KeyPhrases { get; set; } = [];
}

public class FormatearSuggestResponse
{
    public List<FormatearSuggestOutputRecord> Values { get; set; } = [];
}

public class FormatearSuggestOutputRecord
{
    public string RecordId { get; set; } = string.Empty;
    public FormatearSuggestOutputData Data { get; set; } = new();
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class FormatearSuggestOutputData
{
    [JsonPropertyName("suggestDisplay")]
    public string SuggestDisplay { get; set; } = string.Empty;
}
