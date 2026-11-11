using ELPROCAD.CAD.Data.Model.Classification;

namespace ELPROCAD.CAD.Data.CRUD
{
	public sealed partial class Read
	{
		public IReadOnlyList<MainClass> GetAll()
		{
			return GetAll<MainClass>()
				.OrderBy(x => x.Abbrevation)
				.ToList()
				.AsReadOnly();
		}

		public IReadOnlyList<SubClass> GetAll(Guid MainClassId)
		{
			var main = GetById<MainClass>(MainClassId);

			return GetAll<SubClass>()
				.Where(p=>p.MainClass == main)
				.OrderBy(x => x.Abbrevation)
				.ToList()
				.AsReadOnly();
		}
	}
}
