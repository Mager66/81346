using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;
using ELPROCAD.CAD.Data.Provider;

namespace ELPROCAD.CAD.Data.CRUD
{
	public sealed partial class Read
	{
		private readonly DatabaseAccess _database;

		public Read(DatabaseAccess database) => _database = database ?? throw new ArgumentNullException(nameof(database));

		public T? GetById<T>(Guid id) where T : PocoEntity
		{
			return _database.Execute(uow =>
			{
				var item = uow.GetObjectByKey<T>(id);

				if(item != null)
					CaptureVersion(uow, item);

				return item;
			});
		}

		public IReadOnlyList<T> GetAll<T>() where T : PocoEntity
		{
			return _database.Execute<IReadOnlyList<T>>(uow =>
			{
				var items = new XPQuery<T>(uow)
				.ToList();

				foreach (var item in items)
					CaptureVersion(uow, item);

				return items.AsReadOnly();
			});
		}

		private static void CaptureVersion(UnitOfWork uow, PocoEntity entity)
		{
			var member = uow.GetClassInfo(entity.GetType())
				.GetMember("OptimisticLockField");

			// Entities without optimistic locking do not have version field
			entity.LoadedVersion = member == null
				? null
				: Convert.ToInt32(member.GetValue(entity));
		}
	}
}
