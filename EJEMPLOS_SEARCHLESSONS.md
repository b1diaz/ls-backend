# Ejemplos JSON para API SearchLessons

**Endpoint:** `POST http://localhost:7071/api/SearchLessons`

## Campos del Request

### Campos Requeridos:
- `query` (string): Consulta de texto libre (mínimo 3 caracteres)
- `searchField` (int/enum): Campo donde se realizará la búsqueda semántica

### Campos Opcionales:
- `dateFrom` (DateTime): Fecha desde la cual filtrar (formato ISO 8601)
- `dateTo` (DateTime): Fecha hasta la cual filtrar (formato ISO 8601)
- `minScore` (double): Score mínimo requerido (rango: 0 a 1)
- `pageNumber` (int): Número de página a retornar (base 1, por defecto: 1)
- `pageSize` (int): Cantidad de resultados por página. Valores permitidos: 10, 25, 50, 100 (por defecto: 10)

## Enum SearchFieldType

Los valores válidos para `searchField` son:

| Valor | Nombre | Descripción |
|-------|--------|-------------|
| `0` | `Description` | Buscar en la descripción detallada del evento |
| `1` | `Analysis` | Buscar en el análisis de causas del evento |
| `2` | `Consequences` | Buscar en las consecuencias del evento |
| `3` | `Lesson` | Buscar en el aprendizaje generado |

**Nota:** El serializador JSON es case-insensitive, por lo que puedes usar camelCase (recomendado) o PascalCase.

---

## Ejemplo 1: Búsqueda básica en Description

```json
{
  "query": "despliegue producción servicio interrupción",
  "searchField": 0
}
```

**Explicación:** Busca en el campo `Description` con una consulta simple. Retorna la primera página con 10 resultados por defecto.

---

## Ejemplo 2: Búsqueda en Analysis con filtros de fecha

```json
{
  "query": "causa principal presión fecha límite",
  "searchField": 1,
  "dateFrom": "2025-01-01T00:00:00",
  "dateTo": "2025-12-31T23:59:59"
}
```

**Explicación:** Busca en el campo `Analysis` con filtro de rango de fechas.

---

## Ejemplo 3: Búsqueda en Consequences

```json
{
  "query": "interrupción servicio usuarios afectados",
  "searchField": 2
}
```

**Explicación:** Busca en el campo `Consequences` (consecuencias del evento).

---

## Ejemplo 4: Búsqueda en Lesson (Aprendizaje)

```json
{
  "query": "proceso validación aprobación despliegue preventivo",
  "searchField": 3
}
```

**Explicación:** Busca en el campo `Lesson` (aprendizaje generado).

---

## Ejemplo 5: Búsqueda completa con todos los filtros

```json
{
  "query": "proceso obligatorio validación antes despliegue",
  "searchField": 3,
  "dateFrom": "2025-03-01T00:00:00",
  "dateTo": "2025-03-31T23:59:59",
  "minScore": 0.75
}
```

**Explicación:** 
- Busca en el campo `Lesson`
- Filtra por rango de fechas (marzo 2025)
- Solo retorna resultados con score >= 0.75
- Retorna la primera página con 10 resultados por defecto

---

## Ejemplo 6: Búsqueda con score mínimo alto

```json
{
  "query": "near miss incidente seguridad",
  "searchField": 0,
  "minScore": 0.85
}
```

**Explicación:** Solo retorna resultados con score >= 0.85 (alta similitud).

---

## Ejemplo 7: Búsqueda con paginación

```json
{
  "query": "despliegue producción servicio interrupción",
  "searchField": 0,
  "pageNumber": 2,
  "pageSize": 25
}
```

**Explicación:** 
- Busca en el campo `Description`
- Retorna la página 2 con 25 resultados por página

---

## Ejemplo 8: Búsqueda completa con paginación

```json
{
  "query": "proceso obligatorio validación antes despliegue",
  "searchField": 3,
  "dateFrom": "2025-03-01T00:00:00",
  "dateTo": "2025-03-31T23:59:59",
  "minScore": 0.75,
  "pageNumber": 1,
  "pageSize": 50
}
```

**Explicación:** 
- Busca en el campo `Lesson`
- Filtra por rango de fechas (marzo 2025)
- Solo retorna resultados con score >= 0.75
- Retorna la primera página con 50 resultados por página

---

## Estructura de la Respuesta

La API retorna un objeto `PaginatedSearchResult` con la siguiente estructura:

```json
{
  "results": [
    {
      "lesson": {
        "id": "guid-del-documento",
        "code": "COD-001",
        "description": "...",
        "situationType": "...",
        "location": "...",
        "relatedPosition": "...",
        "analysis": "...",
        "consequences": "...",
        "lesson": "...",
        "dateTime": "2025-03-07T14:35:00",
        "searchContent": "..."
      },
      "score": 0.8542
    }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 15
}
```

### Campos de la Respuesta:
- `results` (array): Lista de resultados de la búsqueda para la página actual
- `totalCount` (int): Número total de resultados que coinciden con la búsqueda (sin paginación)
- `pageNumber` (int): Número de página actual (base 1)
- `pageSize` (int): Cantidad de resultados por página
- `totalPages` (int): Número total de páginas disponibles (calculado automáticamente)

---

## Notas Importantes

1. **searchField es excluyente**: Solo se puede seleccionar UN campo a la vez (Description, Analysis, Consequences o Lesson).

2. **minScore**: Score mínimo requerido (rango: 0 a 1). Azure AI Search devuelve scores de similitud entre 0 y 1. Solo se retornan resultados con score igual o superior al valor especificado (ej: 0.7).

3. **Fechas**: Usar formato ISO 8601 (ej: `2025-03-07T14:35:00` o `2025-03-07T14:35:00Z`).

4. **Query**: Mínimo 3 caracteres. Puede incluir palabras clave, descripciones o cualquier dato relevante.

5. **Paginación**: 
   - `pageNumber` debe ser >= 1 (por defecto: 1)
   - `pageSize` solo acepta valores: 10, 25, 50, 100 (por defecto: 10)
   - Si se especifica un `pageSize` inválido, se usará 10 por defecto
   - Si se especifica un `pageNumber` inválido (< 1), se usará 1 por defecto

