using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.MDBX.Documents
{
	internal sealed class DocumentValidator : AbstractValidator<IDocument>
	{
		internal DocumentValidator()
		{
			RuleFor(entity => entity.ProjectId)
				.NotEmpty()
				.WithMessage("A project identifier is required.");

			RuleFor(entity => entity.Name)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A document name is required.")
				.MaximumLength(50)
					.WithMessage("The document name must not exceed 50 characters.");

			RuleFor(entity => entity.Path)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A document path is required.")
				.MaximumLength(255)
					.WithMessage("The document path must not exceed 255 characters.");

			RuleFor(entity => entity.FileFormatId)
				.NotEmpty()
					.WithMessage("A document kind identifier is required.");
		}
	}
}
