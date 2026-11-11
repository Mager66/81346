using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	public interface ICadSymbolLinkRepository
	{
		/// <summary>
		/// Finds the registered object association for a block in a document.
		/// </summary>
		ICadSymbolLink? GetByBlock(Guid documentId, long blockHandle);

		/// <summary>
		/// Returns the registered symbol links in a document.
		/// </summary>
		IReadOnlyList<ICadSymbolLink> GetByDocumentId(Guid documentId);

		/// <summary>
		/// Returns all registered symbols referencing an engineering object.
		/// </summary>
		IReadOnlyList<ICadSymbolLink> GetByObjectId(Guid engineeringObjectId);

		/// <summary>
		/// Creates a symbol link after validating its references.
		/// An existing document and handle combination is rejected.
		/// </summary>
		(ICadSymbolLink? Value, ValidationResult Validation) Create(
			Guid documentId,
			long blockHandle,
			Guid engineeringObjectId);
	}
}