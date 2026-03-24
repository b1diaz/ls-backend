
using FluentValidation;
using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Validations
{
    public class SearchLessonRequestValidator : AbstractValidator<SearchLessonRequest>
    {
        public SearchLessonRequestValidator()
        {
            RuleFor(x => x.Query)
                .NotEmpty().WithMessage("Query es requerida.")
                .MinimumLength(3).WithMessage("Query debe tener al menos 3 caracteres.");

            RuleFor(x => x.DateTo)
                .GreaterThanOrEqualTo(x => x.DateFrom)
                .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
                .WithMessage("DateTo debe ser mayor o igual a DateFrom.");

            RuleFor(x => x.MinScore)
                .InclusiveBetween(0, 1)
                .When(x => x.MinScore.HasValue)
                .WithMessage("MinScore debe estar entre 0 y 1.");
        }
    }
}
