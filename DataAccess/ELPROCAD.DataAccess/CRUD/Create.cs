using DevExpress.Xpo;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Validation;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.CRUD
{
	internal sealed partial class Create
	{
		private readonly IDatabaseAccess _database;
		private readonly Action? _beforePersist;

		internal Create(IDatabaseAccess database, Action? beforePersist = null)
		{
			_database = database ?? throw new ArgumentNullException(nameof(database));
			_beforePersist = beforePersist;
		}

		/// <summary>
		/// Validates and inserts a separate entity instance.
		/// The supplied entity is never attached to the unit of work.
		/// </summary>
		public (T? Value, ValidationResult Validation) New<T>(
			T entity,
			Func<T, ValidationResult> validate,
			Action<UnitOfWork, T, T> copyValues,
			Func<UnitOfWork, T, ValidationResult>? validateDatabase = null)
			where T : class, new()
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			if (validate == null)
				throw new ArgumentNullException(nameof(validate));

			if (copyValues == null)
				throw new ArgumentNullException(nameof(copyValues));

			// Reject invalid input before opening a database connection.
			var validation = validate(entity);

			if (!validation.IsValid)
				return (null, validation);

			return _database.Execute<(T? Value, ValidationResult Validation)>(
				unitOfWork =>
				{
					if(validateDatabase != null)
					{
						var databaseValidation = validateDatabase(unitOfWork, entity);

						if(!databaseValidation.IsValid)
							return (null, databaseValidation);
					}

					var item = new T();

					// Resolve related entities in this unit of work when required.
					copyValues(unitOfWork, entity, item);

					var itemValidation = validate(item);

					if (!itemValidation.IsValid)
						return (null, itemValidation);

					try
					{
						// Allows integration tests to interleave a competing database operation.
						_beforePersist?.Invoke();

						unitOfWork.Save(item);
						unitOfWork.CommitChanges();						
					}
					catch (DevExpress.Xpo.DB.Exceptions.LockingException)
					{
						return (null, ValidationResults.Failure(
							string.Empty,
							"Related data changed during creation. " +
							"The record was not created. Reload the data."));
					}
					catch (Exception exception)	when (VistaDbErrors.IsDuplicateKey(exception))
					{
						return (null, ValidationResults.Failure(
							string.Empty,
							"The record could not be saved because a unique key already exists."));
					}

					Concurrency.CaptureVersion(unitOfWork, item);

					return (item, itemValidation);
				});
		}		
	}
}
