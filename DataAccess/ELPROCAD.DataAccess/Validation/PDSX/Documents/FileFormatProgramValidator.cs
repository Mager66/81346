using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.PDSX.Documents
{
	internal sealed class FileFormatProgramValidator : AbstractValidator<IFileFormatProgram>
	{
		internal FileFormatProgramValidator()
		{
			RuleFor(entity => entity.FileFormatId)
				.NotEmpty()
				.WithMessage("A file format identifier is required.");

			RuleFor(entity => entity.OpeningProgramId)
				.NotEmpty()
				.WithMessage("An opening program identifier is required.");

			RuleFor(entity => entity.Arguments)
				.Cascade(CascadeMode.Stop)
				.NotNull()
					.WithMessage("Arguments must not be null.")
				.MaximumLength(2048)
					.WithMessage("Arguments must not exceed 2048 characters.");
		}
	}
}
