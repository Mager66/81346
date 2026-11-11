using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	internal sealed class CadDocumentSpaceRepository : ICadDocumentSpaceRepository
	{
		private readonly Read _read;

		internal CadDocumentSpaceRepository(Read read) =>
			_read = read ?? throw new ArgumentNullException(nameof(read));

		public ICadDocumentSpace? GetById(Guid id) => _read.GetById<CadDocumentSpace>(id);

		public IReadOnlyList<ICadDocumentSpace> GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<CadDocumentSpace>(
					entity => entity.DocumentId == documentId)
				.OrderBy(entity => entity.SpaceKind)
				.ThenBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ICadDocumentSpace>()
				.ToList()
				.AsReadOnly();
		}
	}
}
