using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Validation;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.CRUD
{
	internal class Delete
	{
		private readonly IDatabaseAccess _database;

		internal Delete(IDatabaseAccess database) =>
			_database = database ?? throw new ArgumentNullException(nameof(database));

		/// <summary>
		/// Deletes an existing entity only if its loaded version is current.
		/// </summary>
		internal ValidationResult Remove<T>(
			Guid id,
			int? loadedVersion,
			Action<UnitOfWork, T>? beforeDelete = null,
			Func<UnitOfWork, T, ValidationResult>? validateDatabase = null)
			where T : VersionedEntity
		{
			if (!loadedVersion.HasValue)
			{
				return ValidationResults.Failure(string.Empty,
					"The original record version is missing. Reload the record.");
			}

			return _database.Execute(unitOfWork =>
			{
				var existing = unitOfWork.GetObjectByKey<T>(id);

				if (existing == null)
				{
					return ValidationResults.Failure(
						nameof(PocoEntity.Id),
						"The record no longer exists.");
				}

				if (Concurrency.GetVersion(unitOfWork, existing)
					!= loadedVersion.Value)
				{
					return ValidationResults.Failure(
						string.Empty,
						"The record has been changed by another user. " +
						"It was not deleted. Reload the record.");
				}

				if(validateDatabase != null)
				{
					var validation=validateDatabase(unitOfWork, existing);

					if (!validation.IsValid)
						return validation;
				}

				try
				{
					// Related changes must be committed together with the deletion.
					beforeDelete?.Invoke(unitOfWork, existing);

					unitOfWork.Delete(existing);
					unitOfWork.CommitChanges();
				}
				catch (LockingException)
				{
					// Another writer changed the record after the version check.
					return ValidationResults.Failure(
						string.Empty,
						"The record or related data changed during deletion. " +
						"It was not deleted. Reload the record.");
				}

				return new ValidationResult();

			});
		}
	}
}
