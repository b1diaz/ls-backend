using FluentValidation;
using LeccionesAprendidas.Models;
using LeccionesAprendidas.Validations;

namespace LeccionesAprendidas.Tests.Validators;

public class SearchLessonRequestValidatorTests
{
    private readonly IValidator<SearchLessonRequest> _validator = new SearchLessonRequestValidator();

    private static SearchLessonRequest ValidRequest() => new()
    {
        Query = "riesgo eléctrico",
        PageNumber = 1,
        PageSize = 10
    };

    [Fact]
    public async Task Valid_Request_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Empty_Query_Fails()
    {
        var req = ValidRequest();
        req.Query = "";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.Query));
    }

    [Fact]
    public async Task Short_Query_Fails()
    {
        var req = ValidRequest();
        req.Query = "ab";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.Query));
    }

    [Fact]
    public async Task DateTo_Before_DateFrom_Fails()
    {
        var req = ValidRequest();
        req.DateFrom = new DateTime(2024, 6, 1);
        req.DateTo = new DateTime(2024, 5, 1);
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.DateTo));
    }

    [Fact]
    public async Task MinScore_Below_Zero_Fails()
    {
        var req = ValidRequest();
        req.MinScore = -0.1;
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.MinScore));
    }

    [Fact]
    public async Task MinScore_Above_One_Fails()
    {
        var req = ValidRequest();
        req.MinScore = 1.1;
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.MinScore));
    }

    [Fact]
    public async Task MinScore_At_Boundaries_Passes()
    {
        var reqZero = ValidRequest();
        reqZero.MinScore = 0;
        var resultZero = await _validator.ValidateAsync(reqZero);
        Assert.True(resultZero.IsValid);

        var reqOne = ValidRequest();
        reqOne.MinScore = 1;
        var resultOne = await _validator.ValidateAsync(reqOne);
        Assert.True(resultOne.IsValid);
    }

    [Fact]
    public async Task Valid_Field_Passes()
    {
        foreach (var field in new[] { "searchContent", "description", "analysis", "consequences", "lesson" })
        {
            var req = ValidRequest();
            req.Field = field;
            var result = await _validator.ValidateAsync(req);
            Assert.True(result.IsValid, $"Field '{field}' debería ser válido.");
        }
    }

    [Fact]
    public async Task Invalid_Field_Fails()
    {
        var req = ValidRequest();
        req.Field = "campoInexistente";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchLessonRequest.Field));
    }
}
