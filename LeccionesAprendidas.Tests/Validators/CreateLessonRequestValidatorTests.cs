using FluentValidation;
using LeccionesAprendidas.Models;
using LeccionesAprendidas.Validations;

namespace LeccionesAprendidas.Tests.Validators;

public class CreateLessonRequestValidatorTests
{
    private readonly IValidator<CreateLessonRequest> _validator = new CreateLessonRequestValidator();

    private static CreateLessonRequest ValidRequest() => new()
    {
        Description = "Descripción del evento",
        SituationType = "Near Miss",
        LessonLearned = "Lección aprendida del evento",
        Location = "Planta A",
        RelatedPosition = "Operador",
        Analysis = "Análisis de causas",
        Consequences = "Sin consecuencias graves"
    };

    [Fact]
    public async Task Valid_Request_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Empty_Description_Fails()
    {
        var req = ValidRequest();
        req.Description = "";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.Description));
    }

    [Fact]
    public async Task Empty_SituationType_Fails()
    {
        var req = ValidRequest();
        req.SituationType = "";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.SituationType));
    }

    [Fact]
    public async Task Empty_LessonLearned_Fails()
    {
        var req = ValidRequest();
        req.LessonLearned = "";
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.LessonLearned));
    }

    [Fact]
    public async Task Location_TooLong_Fails()
    {
        var req = ValidRequest();
        req.Location = new string('x', 251);
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.Location));
    }

    [Fact]
    public async Task RelatedPosition_TooLong_Fails()
    {
        var req = ValidRequest();
        req.RelatedPosition = new string('x', 151);
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.RelatedPosition));
    }

    [Fact]
    public async Task Analysis_TooLong_Fails()
    {
        var req = ValidRequest();
        req.Analysis = new string('x', 1001);
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.Analysis));
    }

    [Fact]
    public async Task Consequences_TooLong_Fails()
    {
        var req = ValidRequest();
        req.Consequences = new string('x', 1001);
        var result = await _validator.ValidateAsync(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLessonRequest.Consequences));
    }
}
