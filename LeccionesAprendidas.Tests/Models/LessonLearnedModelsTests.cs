using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Tests.Models;

public class LessonLearnedModelsTests
{
    [Fact]
    public void SearchContent_ContainsContentFields()
    {
        var lesson = new LessonLearned
        {
            Description = "Desc",
            SituationType = "Near Miss",
            Location = "Planta A",
            RelatedPosition = "Operador",
            Analysis = "Analisis",
            Consequences = "Consecuencias",
            Lesson = "Leccion"
        };

        var content = lesson.SearchContent;

        Assert.Contains("Desc", content);
        Assert.Contains("Analisis", content);
        Assert.Contains("Consecuencias", content);
        Assert.Contains("Leccion", content);
        Assert.Contains("Near Miss", content);
        Assert.Contains("Planta A", content);
        Assert.Contains("Operador", content);
    }

    [Fact]
    public void SearchContent_OmitsNaAndEmptyFields()
    {
        var lesson = new LessonLearned
        {
            Description = "Desc",
            Analysis = "Analisis",
            Consequences = "Consecuencias",
            Lesson = "Leccion",
            SituationType = "N/A",
            Location = "",
            RelatedPosition = "na"
        };

        var content = lesson.SearchContent;
        var segments = content.Split(". ");

        // Solo los 4 campos con valor real deben estar presentes
        Assert.Equal(4, segments.Length);
        Assert.DoesNotContain("N/A", content);
        Assert.DoesNotContain("..", content); // sin separadores dobles por campos vacíos
    }

    [Fact]
    public void TotalPages_ExactDivision()
    {
        var result = new PaginatedSearchResult
        {
            TotalCount = 20,
            PageSize = 10
        };

        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void TotalPages_RoundsUp()
    {
        var result = new PaginatedSearchResult
        {
            TotalCount = 21,
            PageSize = 10
        };

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void LessonLearned_DefaultId_IsNotEmpty()
    {
        var lesson = new LessonLearned();

        Assert.False(string.IsNullOrEmpty(lesson.Id));
        Assert.NotEqual(Guid.Empty.ToString(), lesson.Id);
    }
}
