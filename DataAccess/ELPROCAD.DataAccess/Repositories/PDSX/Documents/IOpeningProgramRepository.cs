using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	public interface IOpeningProgramRepository
	{
		/// <summary>
		/// Finds a program by its identifier.
		/// </summary>
		IOpeningProgram? GetById(Guid id);

		/// <summary>
		/// Returns programs ordered by name.
		/// </summary>
		IReadOnlyList<IOpeningProgram> GetAll();

		/// <summary>
		/// Validates and creates a program configuration.
		/// </summary>
		(IOpeningProgram? Value, ValidationResult Validation) Create(
			string name,
			string executablePath);

		/// <summary>
		/// Updates a program using its originally loaded version.
		/// </summary>
		(IOpeningProgram? Value, ValidationResult Validation) Update(
			IOpeningProgram entity);
	}
}
