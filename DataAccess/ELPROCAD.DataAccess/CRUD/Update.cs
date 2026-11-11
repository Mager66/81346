using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Validation;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.CRUD
{
	internal class Update
	{
		private readonly IDatabaseAccess _database;

		internal Update(IDatabaseAccess database) =>
			_database = database ?? throw new ArgumentNullException(nameof(database));

		public (T? Value, ValidationResult Validation) Save<T>(
			T entity,
			Func<T, ValidationResult> validate,
			Action<UnitOfWork, T, T> copyValues,
			Func<UnitOfWork, T, ValidationResult>? validateDatabase = null)
			where T : VersionedEntity
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			if (validate == null)
				throw new ArgumentNullException(nameof(validate));

			if (copyValues == null)
				throw new ArgumentNullException(nameof(copyValues));

			var validation = validate(entity);

			if (!validation.IsValid)
				return (null, validation);

			if (!entity.LoadedVersion.HasValue)
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The original record version is missing. Reload the record."));
			}

			return _database.Execute<(T? Value, ValidationResult Validation)>(
				unitOfWork =>
				{
					var existing = unitOfWork.GetObjectByKey<T>(entity.Id);

					if (existing == null)
						return (null, ValidationResults.Failure(nameof(entity.Id), "The record no longer exists."));

					if (Concurrency.GetVersion(unitOfWork, existing) != entity.LoadedVersion.Value)
						return (null, ValidationResults.Failure(
							string.Empty,
							"The record has been changed by another user. " +
							"Your changes were not saved. Reload the record."));

					// Validate database-dependent rules against the current stored instance.
					if (validateDatabase != null)
					{
						var databaseValidation = validateDatabase(unitOfWork, existing);

						if (!databaseValidation.IsValid)
							return (null, databaseValidation);
					}

					copyValues(unitOfWork, entity, existing);

					var itemValidation = validate(existing);

					if (!itemValidation.IsValid)
						return (null, itemValidation);

					try
					{
						unitOfWork.Save(existing);
						unitOfWork.CommitChanges();
					}
					catch (LockingException)
					{
						// Another writer committed after the version check.
						return (null, ValidationResults.Failure(
							string.Empty,
							"The record changed during saving. " +
							"Your changes were not saved. Reload the record."));
					}
					catch (Exception ex) when (VistaDbErrors.IsDuplicateKey(ex))
					{
						return (null, ValidationResults.Failure(
							string.Empty,
							"The record could not be saved because a unique key already exists."));
					}

					Concurrency.CaptureVersion(unitOfWork, existing);

					return (existing, itemValidation);
				});
		}
	}
}
