using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.Classification;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Classification
{
	internal sealed class SubClassRepository : ISubClassRepository
	{
		private readonly Read _read;

		internal SubClassRepository(Read read) =>
			_read = read ?? throw new ArgumentNullException(nameof(read));

		public ISubClass? GetById(Guid id) => _read.GetById<SubClass>(id);

		public IReadOnlyList<ISubClass> GetByMainClassId(Guid mainClassId)
		{
			return _read.GetAll<SubClass>(
					entity => entity.MainClass.Id == mainClassId)
				.OrderBy(
					entity => entity.Abbrevation,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ISubClass>()
				.ToList()
				.AsReadOnly();
		}
	}
}
