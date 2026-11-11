using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.DataAccess.Validation.PDSX.Classification
{
	internal abstract class ClassificationValidator<T> : AbstractValidator<T>
	where T : IClassification
	{
		protected ClassificationValidator()
		{
			RuleFor(entity => entity.Abbrevation)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("An abbreviation is required.")
				.Length(2, 3)
					.WithMessage("The abbreviation must contain 2–3 characters.");

			RuleFor(entity => entity.Tooltip)
				.NotEmpty()
				.WithMessage("A tooltip is required.");
		}
	}
}
