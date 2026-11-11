using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.MDBX.Documents;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	internal sealed class CadDocumentCustomPropertyRepository : ICadDocumentCustomPropertyRepository
	{
		private readonly Read _read;

		internal CadDocumentCustomPropertyRepository(Read read) =>
			_read = read ?? throw new ArgumentNullException(nameof(read));

		public ICadDocumentCustomProperty? GetById(Guid id) => _read.GetById<CadDocumentCustomProperty>(id);

		public IReadOnlyList<ICadDocumentCustomProperty> GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<CadDocumentCustomProperty>(
					entity => entity.DocumentId == documentId)
				.OrderBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ICadDocumentCustomProperty>()
				.ToList()
				.AsReadOnly();
		}
	}
}
