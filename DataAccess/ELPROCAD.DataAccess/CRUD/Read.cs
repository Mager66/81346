using DevExpress.Xpo;
using ELPROCAD.DataAccess.Database;
using System.Runtime.CompilerServices;

namespace ELPROCAD.DataAccess.CRUD
{
	internal sealed partial class Read
	{
		private readonly IDatabaseAccess _database;

		internal Read(IDatabaseAccess database) =>
			_database = database ?? throw new ArgumentNullException(nameof(database));

		public T? GetById<T>(Guid id) where T : class
		{
			return _database.Execute(unitOfWork =>
			{
				var item = unitOfWork.GetObjectByKey<T>(id);

				if (item != null)
					Concurrency.CaptureVersion(unitOfWork, item);

				return item;
			});
		}

		public IReadOnlyList<T> GetAll<T>() where T : class
		{
			return _database.Execute<IReadOnlyList<T>>(unitOfWork =>
				Materialize(unitOfWork, new XPQuery<T>(unitOfWork)));
		
		}

		public IReadOnlyList<T> GetAll<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
			where T : class
		{
			if (predicate == null)
				throw new ArgumentNullException(nameof(predicate));

			return _database.Execute<IReadOnlyList<T>>(unitOfWork =>
				Materialize(
					unitOfWork, new XPQuery<T>(unitOfWork)
					.Where(predicate)));
		}

		private static IReadOnlyList<T> Materialize<T>(UnitOfWork uow, IQueryable<T> query) where T : class
		{
			var items = query.ToList();

			foreach (var item in items)
				Concurrency.CaptureVersion(uow, item);

			return items.AsReadOnly();
		}
	}
}
