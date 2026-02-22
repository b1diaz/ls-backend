
using FluentValidation;
using LeccionesAprendidas.Models;

namespace LeccionesAprendidas.Validations
{
    public class CreateLessonRequestValidator : AbstractValidator<CreateLessonRequest>
    {
        public CreateLessonRequestValidator()
        {
            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description es requerido.");

            RuleFor(x => x.SituationType)
                .NotEmpty().WithMessage("SituationType es requerido.");

            RuleFor(x => x.LessonLearned)
                .NotEmpty().WithMessage("LessonLearned es requerido.");

            RuleFor(x => x.Location)
                .MaximumLength(250).WithMessage("Location no debe superar los 250 caracteres.");

            RuleFor(x => x.RelatedPosition)
                .MaximumLength(150).WithMessage("RelatedPosition no debe superar los 150 caracteres.");

            RuleFor(x => x.Analysis)
                .MaximumLength(1000).WithMessage("Analysis no debe superar los 1000 caracteres.");

            RuleFor(x => x.Consequences)
                .MaximumLength(1000).WithMessage("Consequences no debe superar los 1000 caracteres.");
        }

    }
}
