using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.CAD.Data.Model;
using ELPROCAD.CAD.Data.Provider;
using ELPROCAD.CAD.Data.Validation;
using ELPROCAD.CAD.Data.Validation.Conflict;
using ELPROCAD.CAD.Data.Validation.VistDb;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.CRUD
{
	public sealed partial class Create
	{
		private readonly DatabaseAccess _database;
		private readonly Action? _beforePersist;

		public Create(DatabaseAccess database) : this(database, null) { }



		internal Create(DatabaseAccess database, Action? beforePersist)
		{
			_database = database ?? throw new ArgumentNullException(nameof(database));
			_beforePersist = beforePersist;
		}

		public (T? Value, ValidationResult Validation) New<T>(
			T entity,
			Action<UnitOfWork, T, T> copyValues,
			Func<UnitOfWork, T, ValidationResult> validateDatabase) where T : PocoEntity, new() =>
			Save(entity, isNew: true, copyValues: copyValues, validateDatabase: validateDatabase);


		private (T? Value, ValidationResult Validation) Save<T>(
			T entity, 
			bool isNew, 
			Action<UnitOfWork,T, T> copyValues,
			Func<UnitOfWork, T, ValidationResult> validateDatabase) 
			where T : PocoEntity, new()
		{
			if(entity == null)
				throw new ArgumentNullException(nameof(entity));

			if(copyValues == null)
				throw new ArgumentNullException(nameof(copyValues));

			if(validateDatabase == null)
				throw new ArgumentNullException(nameof(validateDatabase));

			// Validate without open the database
			var validation = entity.Validate();
			if(!validation.IsValid)
				return(null, validation);

			return _database.Execute<(T? Value, ValidationResult Validation)>(uow =>
			{
				uow.LockingOption = LockingOption.Optimistic;

				T item;

				if (isNew)
				{
					item = new T
					{
						Id = entity.Id
					};
				}
				else
				{
					var existing = uow.GetObjectByKey<T>(entity.Id);

					if (existing == null)
					{
						return (null, ValidationResults.Failure(nameof(entity.Id), "Posten finns inte längre."));
					}

					var currentVersion = Concurrency.GetVersion(uow, existing);

					if (currentVersion.HasValue)
					{
						if (!entity.LoadedVersion.HasValue)
						{
							return (null, ValidationResults.Failure(
								string.Empty,
								"Postens ursprungliga version saknas. Läs in posten igen."));
						}

						if (currentVersion != entity.LoadedVersion)
						{
							return (null, ValidationResults.Failure(
							string.Empty,
							"Posten har ändrats av någon annan. " +
							"Dina ändringar har inte sparats. Läs in senaste versionen."));
						}
					}

					item = existing;

				}

				// Exempelvis kontroll av unik förkortning.
				var databaseValidation = validateDatabase(uow, entity);

				if (!databaseValidation.IsValid)
					return (null, databaseValidation);

				// Referenser måste här lösas i aktuell session.
				copyValues(uow, entity, item);

				var itemValidation = item.Validate();

				if (!itemValidation.IsValid)
					return (null, itemValidation);

				try
				{
					_beforePersist?.Invoke();

					uow.Save(item);
					uow.CommitChanges();
				}
				catch (LockingException)
				{
					return (null, ValidationResults.Failure(
						string.Empty,
						"Posten ändrades av någon annan under sparningen. " +
						"Dina ändringar har inte sparats. Läs in senaste versionen."));
				}
				catch (Exception ex) when (VistaDbErrors.IsDuplicateKey(ex))
				{
					return (null, ValidationResults.Failure(
						string.Empty,
						"Posten kunde inte sparas eftersom en unik nyckel redan finns. " +
						"Förkortningen kan ha registrerats av någon annan. Läs in senaste data."));
				}

				Concurrency.CaptureVersion(uow, item);

				return (item, itemValidation);
			});
		}	
	}
}
