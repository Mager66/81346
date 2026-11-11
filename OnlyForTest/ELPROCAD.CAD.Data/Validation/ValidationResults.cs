using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.Validation
{
	public static class ValidationResults
	{
		public static ValidationResult Failure(
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
