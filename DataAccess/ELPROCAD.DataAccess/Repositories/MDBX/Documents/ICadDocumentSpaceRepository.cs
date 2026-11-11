using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	public interface ICadDocumentSpaceRepository
	{
		/// <summary>
		/// Finds a CAD space by its identifier.
		/// </summary>
		ICadDocumentSpace? GetById(Guid id);

		/// <summary>
		/// Returns a document's spaces, with model space first,
		/// followed by paper spaces ordered by name.
		/// </summary>
		IReadOnlyList<ICadDocumentSpace> GetByDocumentId(Guid documentId);
	}
}
