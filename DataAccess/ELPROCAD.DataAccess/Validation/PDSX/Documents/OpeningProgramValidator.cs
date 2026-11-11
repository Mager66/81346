using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.DataAccess.Validation.PDSX.Documents
{
	internal sealed class OpeningProgramValidator : AbstractValidator<IOpeningProgram>
	{
		internal OpeningProgramValidator()
		{
			RuleFor(entity => entity.Name)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A program name is required.")
				.MaximumLength(50)
					.WithMessage("The program name must not exceed 50 characters.");

			RuleFor(entity => entity.ExecutablePath)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("An executable path is required.")
				.MaximumLength(1024)
					.WithMessage("The executable path must not exceed 1024 characters.");
		}
	}
}
