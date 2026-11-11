using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using FluentValidation;

namespace ELPROCAD.DataAccess.Validation.MDBX.Aspects
{
	internal sealed class AspectNodeValidator : AbstractValidator<IAspectNode>
	{
		internal AspectNodeValidator()
		{
			RuleFor(entity => entity.ProjectId)
				.NotEmpty()
				.WithMessage("A project identifier is required.");

			RuleFor(entity => entity.Designation)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("A node designation is required.")
				.MaximumLength(255)
					.WithMessage("The node designation must not exceed 255 characters.");

			RuleFor(entity => entity.ParentId)
				.Cascade(CascadeMode.Stop)
				.Must(id => !id.HasValue || id.Value != Guid.Empty)
					.WithMessage("The parent identifier must not be an empty GUID.")
				.Must((entity, id) => !id.HasValue || id.Value != entity.Id)
					.WithMessage("A node cannot be its own parent.");

			RuleFor(entity => entity.RepresentedByObjectId)
				.Must(id => !id.HasValue || id.Value != Guid.Empty)
				.WithMessage(
					"The representing object identifier must not be an empty GUID.");
		}
	}
}
