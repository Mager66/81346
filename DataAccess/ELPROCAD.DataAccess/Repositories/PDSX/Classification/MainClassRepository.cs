using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.Classification;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Classification
{
	internal sealed class MainClassRepository : IMainClassRepository
	{
		private readonly Read _read;

		internal MainClassRepository(Read read) =>
			_read = read ?? throw new ArgumentNullException(nameof(read));

		public IMainClass? GetById(Guid id) => _read.GetById<MainClass>(id);

		public IReadOnlyList<IMainClass> GetAll()
		{
			return _read.GetAll<MainClass>()
				.OrderBy(
					entity => entity.Abbrevation,
					StringComparer.OrdinalIgnoreCase)
				.Cast<IMainClass>()
				.ToList()
				.AsReadOnly();
		}
	}
}
