using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using FluentValidation;
using System.Linq.Expressions;

namespace ELPROCAD.DataAccess.Validation.MDBX.Objects
{
	internal sealed class EngineeringObjectValidator : AbstractValidator<IEngineeringObject>
	{
		internal EngineeringObjectValidator()
		{
			RuleFor(entity => entity.ProjectId)
				.NotEmpty()
				.WithMessage("A project identifier is required.");

			ValidateOptionalReference(entity => entity.PowerFunctionId);
			ValidateOptionalReference(entity => entity.ControlFunctionId);
			ValidateOptionalReference(entity => entity.TopographicStructureId);
			ValidateOptionalReference(entity => entity.LocationId);
			ValidateOptionalReference(entity => entity.FunctionId);
			ValidateOptionalReference(entity => entity.ProductId);

			RuleFor(entity => entity)
				.Must(HasAspectMembership)
				.WithMessage(
					"An engineering object must belong to at least one aspect node.");
		}

		private void ValidateOptionalReference(Expression<Func<IEngineeringObject, Guid?>> reference)
		{
			RuleFor(reference)
				.Must(id => !id.HasValue || id.Value != Guid.Empty)
				.WithMessage("{PropertyName} must not be an empty GUID.");
		}

		private static bool HasAspectMembership(IEngineeringObject entity)
		{
			return IsSpecified(entity.PowerFunctionId)
				|| IsSpecified(entity.ControlFunctionId)
				|| IsSpecified(entity.TopographicStructureId)
				|| IsSpecified(entity.LocationId)
				|| IsSpecified(entity.FunctionId)
				|| IsSpecified(entity.ProductId);
		}

		private static bool IsSpecified(Guid? id)
		{
			return id.HasValue && id.Value != Guid.Empty;
		}
	}
}
