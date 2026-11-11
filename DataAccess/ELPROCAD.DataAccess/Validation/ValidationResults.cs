using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Validation
{
	internal static class ValidationResults
	{
		/// <summary>
		/// Creates a validation result containing a single failure.
		/// </summary>
		internal static ValidationResult Failure(
			string propertyName,
			string message)
		{
			return new ValidationResult(new[]
			{
				new ValidationFailure(propertyName, message)
			});
		}
	}
}
