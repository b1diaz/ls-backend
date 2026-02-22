namespace LeccionesAprendidas.Models;

/// <summary>
/// Tipos de campos disponibles para búsqueda semántica.
/// </summary>
public enum SearchFieldType
{
    /// <summary>
    /// Buscar en la descripción detallada del evento.
    /// </summary>
    Description,
    
    /// <summary>
    /// Buscar en el análisis de causas del evento.
    /// </summary>
    Analysis,
    
    /// <summary>
    /// Buscar en las consecuencias del evento.
    /// </summary>
    Consequences,
    
    /// <summary>
    /// Buscar en el aprendizaje generado.
    /// </summary>
    Lesson
}

