using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	public interface ICadDocumentCustomPropertyRepository
	{
		/// <summary>
		/// Finds a custom property by its identifier.
		/// </summary>
		ICadDocumentCustomProperty? GetById(Guid id);

		/// <summary>
		/// Returns a document's custom properties ordered by name.
		/// </summary>
		IReadOnlyList<ICadDocumentCustomProperty> GetByDocumentId(Guid documentId);
	}
}
