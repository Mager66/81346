using ELPROCAD.CAD.Data.Interface;
using FluentValidation;

namespace ELPROCAD.CAD.Data.Validation.Classification
{
	public abstract class ClassificationValidator<T> : AbstractValidator<T>
		where T : IClassification
	{
		protected ClassificationValidator()
		{
			

			RuleFor(x => x.Tooltip)
				.NotEmpty()
					.WithMessage("Ange ett tooltip.");
		}
	}
}
