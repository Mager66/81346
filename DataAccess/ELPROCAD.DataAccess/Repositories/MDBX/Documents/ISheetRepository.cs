using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	public interface ISheetRepository
	{
		/// <summary>
		/// Finds a sheet by its identifier.
		/// </summary>
		ISheet? GetById(Guid id);

		/// <summary>
		/// Returns a document's sheets ordered by sheet number.
		/// </summary>
		IReadOnlyList<ISheet> GetByDocumentId(Guid documentId);

		/// <summary>
		/// Creates a sheet belonging to an existing document.
		/// </summary>
		(ISheet? Value, ValidationResult Validation) Create(
			Guid documentId,
			string sheetNo);

		/// <summary>
		/// Updates the sheet number using the originally loaded version.
		/// </summary>
		(ISheet? Value, ValidationResult Validation) Update(ISheet entity);

		/// <summary>
		/// Deletes a sheet using its originally loaded version.
		/// </summary>
		ValidationResult Delete(ISheet entity);
	}
}
