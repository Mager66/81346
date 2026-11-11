using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	public interface IDocumentRepository
	{
		/// <summary>
		/// Finds a document by its identifier.
		/// </summary>
		IDocument? GetById(Guid id);

		/// <summary>
		/// Returns documents ordered by name.
		/// </summary>
		IReadOnlyList<IDocument> GetAll();

		/// <summary>
		/// Validates and creates a document associated with the supplied project.
		/// </summary>
		(IDocument? Value, ValidationResult Validation) Create(
			Guid projectId,
			Guid FileFormatId,
			string name,
			string path);

		/// <summary>
		/// Validates and updates a document using its originally loaded version.
		/// </summary>
		(IDocument? Value, ValidationResult Validation) Update(IDocument entity);

		/// <summary>
		/// Deletes a document using its originally loaded version.
		/// </summary>
		ValidationResult Delete(IDocument entity);
	}
}
