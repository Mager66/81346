using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	public interface IDocumentPropertiesRepository
	{
		/// <summary>
		/// Returns the document's summary properties, or null if none are stored.
		/// </summary>
		IDocumentProperties? GetByDocumentId(Guid documentId);
	}
}
