using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.PDSX.ProjectData
{
	internal sealed class ProjectValidator : AbstractValidator<IProject>
	{
		internal ProjectValidator()
		{
			RuleFor(entity => entity.Name)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A project name is required.")
				.MaximumLength(50)
					.WithMessage("The project name must not exceed 50 characters.");

			RuleFor(entity => entity.Path)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A project path is required.")
				.MaximumLength(255)
					.WithMessage("The project path must not exceed 255 characters.");

			RuleFor(entity => entity.Description)
				.MaximumLength(255)
				.WithMessage("The project description must not exceed 255 characters.");
		}
	}
}
