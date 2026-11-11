using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace ELPROCAD.DataAccess.Database
{
	internal sealed class DatabaseAccess : IDatabaseAccess
	{
		private readonly string _connectionString;

		internal DatabaseAccess(string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new ArgumentException(
					"A connection string is required.",
					nameof(connectionString));
			}

			_connectionString = connectionString;
		}

		public TResult Execute<TResult>(Func<UnitOfWork, TResult> operation)
		{
			if (operation == null)
				throw new ArgumentNullException(nameof(operation));

			// Schema creation belongs to database initialization.
			using var dataLayer = XpoDefault.GetDataLayer(
				_connectionString,
				AutoCreateOption.None);

			using var unitOfWork = new UnitOfWork(dataLayer)
			{
				LockingOption = LockingOption.Optimistic
			};

			return operation(unitOfWork);
		}

		public void Execute(Action<UnitOfWork> operation)
		{
			if (operation == null)
				throw new ArgumentNullException(nameof(operation));

			Execute<object?>(unitOfWork =>
			{
				operation(unitOfWork);
				return null;
			});
		}

		public TResult ExecuteExplicit<TResult>(Func<ExplicitUnitOfWork, TResult> operation)
		{
			if (operation == null)
				throw new ArgumentNullException(nameof(operation));

			// Database initialization must complete before this operation.
			// Schema checks cannot run inside an explicit VistaDB transaction.
			using var dataLayer = XpoDefault.GetDataLayer(
				_connectionString,
				AutoCreateOption.SchemaAlreadyExists);

			using var unitOfWork = new ExplicitUnitOfWork(dataLayer)
			{
				LockingOption = LockingOption.Optimistic
			};

			unitOfWork.BeginTransaction();

			// The operation commits explicitly on success.
			// Disposing an uncommitted unit of work rolls back the transaction.
			return operation(unitOfWork);
		}
	}
}
