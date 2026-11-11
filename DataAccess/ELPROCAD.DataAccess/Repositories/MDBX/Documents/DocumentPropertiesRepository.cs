using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	internal sealed class DocumentPropertiesRepository : IDocumentPropertiesRepository
	{
		private readonly Read _read;

		internal DocumentPropertiesRepository(Read read) =>
			_read = read ?? throw new ArgumentNullException(nameof(read));

		public IDocumentProperties? GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<DocumentProperties>(
					entity => entity.DocumentId == documentId)
				.SingleOrDefault();
		}
	}
}
