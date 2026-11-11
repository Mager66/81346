using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.PDSX.Classification
{
	internal sealed class SubClassValidator
		: ClassificationValidator<ISubClass>
	{
		internal SubClassValidator()
		{
			// Validate the reference without revalidating the parent's fields.
			RuleFor(entity => entity.MainClass)
				.NotNull()
				.WithMessage("A main classification is required.");
		}
	}
}
