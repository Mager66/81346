using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	public interface IFileFormatRepository
	{
		/// <summary>
		/// Finds a file format by its identifier.
		/// </summary>
		IFileFormat? GetById(Guid id);

		/// <summary>
		/// Finds a file format using a normalized file extension.
		/// </summary>
		IFileFormat? GetByExtension(string extension);

		/// <summary>
		/// Returns file formats ordered by name.
		/// </summary>
		IReadOnlyList<IFileFormat> GetAll();

		/// <summary>
		/// Validates and creates a file format.
		/// </summary>
		(IFileFormat? Value, ValidationResult Validation) Create(
			string name,
			string description,
			string extension);

		/// <summary>
		/// Updates a file format using its originally loaded version.
		/// </summary>
		(IFileFormat? Value, ValidationResult Validation) Update(
			IFileFormat entity);
	}
}
