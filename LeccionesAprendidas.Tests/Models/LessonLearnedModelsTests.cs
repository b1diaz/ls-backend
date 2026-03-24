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

        // SearchContent incluye los campos de contenido principal
        Assert.Contains("Desc", content);
        Assert.Contains("Analisis", content);
        Assert.Contains("Consecuencias", content);
        Assert.Contains("Leccion", content);

        // SituationType, Location y RelatedPosition ya no forman parte de searchContent
        Assert.DoesNotContain("Near Miss", content);
        Assert.DoesNotContain("Planta A", content);
        Assert.DoesNotContain("Operador", content);
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
