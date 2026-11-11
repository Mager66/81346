using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.PDSX.Documents
{
	internal sealed class FileFormatValidator : AbstractValidator<IFileFormat>
	{
		internal FileFormatValidator()
		{
			RuleFor(entity => entity.Name)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A file format name is required.")
				.MaximumLength(50)
					.WithMessage("The file format name must not exceed 50 characters.");

			RuleFor(entity => entity.Description)
				.MaximumLength(255)
				.WithMessage("The description must not exceed 255 characters.");

			RuleFor(entity => entity.Extension)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A file extension is required.")
				.Must(extension =>
					FileExtensions.Normalize(extension).Length <= 16)
					.WithMessage("The normalized file extension must not exceed 16 characters.")
				.Must(extension =>
					System.Text.RegularExpressions.Regex.IsMatch(
						FileExtensions.Normalize(extension),
						@"^\.[a-z0-9]+(?:\.[a-z0-9]+)*$"))
					.WithMessage(
						"The file extension must contain letters or digits separated by periods.");
		}
	}
}
