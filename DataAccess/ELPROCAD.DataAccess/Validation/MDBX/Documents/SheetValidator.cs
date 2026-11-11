using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.MDBX.Documents
{
	internal sealed class SheetValidator : AbstractValidator<ISheet>
	{
		internal SheetValidator()
		{
			RuleFor(entity => entity.DocumentId)
				.NotEmpty()
				.WithMessage("A document identifier is required.");

			RuleFor(entity => entity.SheetNo)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A sheet number is required.")
				.MaximumLength(10)
					.WithMessage("The sheet number must not exceed 10 characters.");
		}
	}
}
