using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Tests.Models;

public class LessonLearnedModelsTests
{
    [Fact]
    public void SearchContent_ContainsAllFields()
    {
        var lesson = new LessonLearned
        {
            Description = "Desc",
            SituationType = "Near Miss",
            Location = "Planta A",
            RelatedPosition = "Operador",
            Analysis = "Análisis",
            Consequences = "Consecuencias",
            Lesson = "Lección"
        };

        var content = lesson.SearchContent;

        Assert.Contains("Desc", content);
        Assert.Contains("Near Miss", content);
        Assert.Contains("Planta A", content);
        Assert.Contains("Operador", content);
        Assert.Contains("Análisis", content);
        Assert.Contains("Consecuencias", content);
        Assert.Contains("Lección", content);
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
