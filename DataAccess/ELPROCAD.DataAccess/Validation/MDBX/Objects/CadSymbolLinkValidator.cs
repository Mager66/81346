using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.MDBX.Objects
{
	internal sealed class CadSymbolLinkValidator
		: AbstractValidator<ICadSymbolLink>
	{
		internal CadSymbolLinkValidator()
		{
			RuleFor(entity => entity.DocumentId)
				.NotEmpty()
				.WithMessage("A document identifier is required.");

			RuleFor(entity => entity.SheetId)
				.NotEmpty()
				.WithMessage("A sheet is required.");

			RuleFor(entity => entity.BlockHandle)
				.NotEqual(0L)
				.WithMessage("A non-zero block handle is required.");

			RuleFor(entity => entity.EngineeringObjectId)
				.NotEmpty()
				.WithMessage("An engineering object identifier is required.");
		}
	}
}