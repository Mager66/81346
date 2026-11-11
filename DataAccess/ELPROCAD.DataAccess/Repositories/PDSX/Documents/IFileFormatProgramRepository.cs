using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	public interface IFileFormatProgramRepository
	{
		/// <summary>
		/// Finds an association by its identifier.
		/// </summary>
		IFileFormatProgram? GetById(Guid id);

		/// <summary>
		/// Returns the program associations for a file format.
		/// </summary>
		IReadOnlyList<IFileFormatProgram> GetByFileFormatId(Guid fileFormatId);

		/// <summary>
		/// Creates an association between an existing format and program.
		/// </summary>
		(IFileFormatProgram? Value, ValidationResult Validation) Create(
			Guid fileFormatId,
			Guid openingProgramId,
			string arguments);

		/// <summary>
		/// Updates launch arguments using the originally loaded version.
		/// </summary>
		(IFileFormatProgram? Value, ValidationResult Validation) Update(
			IFileFormatProgram entity);

		/// <summary>
		/// Deletes an association without deleting its format or program.
		/// </summary>
		ValidationResult Delete(IFileFormatProgram entity);

		/// <summary>
		/// Returns the default program association for a file format, if selected.
		/// </summary>
		IFileFormatProgram? GetDefault(Guid fileFormatId);

		/// <summary>
		/// Selects or clears the default association using the format's loaded version.
		/// Returns the file format with its updated version.
		/// </summary>
		(IFileFormat? Value, ValidationResult Validation) SetDefault(
			IFileFormat fileFormat,
			Guid? associationId);

		/// <summary>
		/// Resolves the default program and its launch configuration for a file format.
		/// </summary>
		(IOpeningConfiguration? Value, ValidationResult Validation) ResolveDefault(
			Guid fileFormatId);
	}
}
