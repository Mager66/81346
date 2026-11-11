using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	internal sealed class EngineeringObjectRepository : IEngineeringObjectRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		//private readonly Delete _delete;
		private readonly EngineeringObjectValidator _validator = new();

		internal EngineeringObjectRepository(Read read, Create create, Update update)//, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			//_delete = delete ?? throw new ArgumentNullException(nameof(delete));
		}

		public IEngineeringObject New(Guid projectId)
		{
			return new EngineeringObject
			{
				ProjectId = projectId
			};
		}
		public IEngineeringObject? GetById(Guid id) => _read.GetById<EngineeringObject>(id);

		public IReadOnlyList<IEngineeringObject> GetByProjectId(Guid projectId)
		{
			return _read.GetAll<EngineeringObject>(
				entity => entity.ProjectId == projectId);
		}

		public IReadOnlyList<IEngineeringObject> GetBySheetId(Guid sheetId)
		{
			var objectIds = _read.GetAll<CadSymbolLink>(link => link.SheetId == sheetId)
				.Select(link => link.EngineeringObjectId)
				.Distinct()
				.ToArray();

			if (objectIds.Length == 0)
				return Array.Empty<IEngineeringObject>();

			return _read.GetAll<EngineeringObject>(
				entity => objectIds.Contains(entity.Id));
		}

		public IReadOnlyList<IEngineeringObject> GetByLocationId(Guid locationId)
		{
			return _read.GetAll<EngineeringObject>(
				entity => entity.LocationId == locationId);
		}

		public (IEngineeringObject? Value, ValidationResult Validation) Create(IEngineeringObject entity)
		{
			var input = Snapshot(entity);

			var result = _create.New(
				input,
				item => _validator.Validate(item),
				CopyObject,
				ValidateReferences);

			return (result.Value, result.Validation);
		}

		public (IEngineeringObject? Value, ValidationResult Validation) Update(IEngineeringObject entity)
		{
			var input = Snapshot(entity);

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyObject,
				(unitOfWork, existing) =>
				{
					if (existing.IsMarkedForDeletion)
					{
						return ValidationResults.Failure(
							nameof(IEngineeringObject.IsMarkedForDeletion),
							"The object is marked for deletion. Restore it before editing.");
					}

					if (existing.ProjectId != input.ProjectId)
					{
						return ValidationResults.Failure(
							nameof(IEngineeringObject.ProjectId),
							"Changing the object's project is not supported by this operation.");
					}

					return ValidateReferences(unitOfWork, input);
				});

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(IEngineeringObject entity) => SetDeletionState(entity, markDeleted: true).Validation;

		public (IEngineeringObject? Value, ValidationResult Validation) Restore(IEngineeringObject entity) => SetDeletionState(entity, markDeleted: false);

		private static ValidationResult ValidateReferences(UnitOfWork unitOfWork, EngineeringObject entity)
		{
			var dependencies = new List<VersionedEntity>();

			var results = new[]
			{
				ValidateAspect<PowerFunction>(
					unitOfWork, entity.PowerFunctionId, entity.ProjectId,
					nameof(entity.PowerFunctionId), dependencies),
				
				ValidateAspect<ControlFunction>(
					unitOfWork, entity.ControlFunctionId, entity.ProjectId,
					nameof(entity.ControlFunctionId), dependencies),

				ValidateAspect<TopographicStructure>(
					unitOfWork, entity.TopographicStructureId, entity.ProjectId,
					nameof(entity.TopographicStructureId), dependencies),
				
				ValidateAspect<Location>(
					unitOfWork, entity.LocationId, entity.ProjectId,
					nameof(entity.LocationId), dependencies),
				
				ValidateAspect<Function>(
					unitOfWork, entity.FunctionId, entity.ProjectId,
					nameof(entity.FunctionId), dependencies),
				
				ValidateAspect<Product>(
					unitOfWork, entity.ProductId, entity.ProjectId,
					nameof(entity.ProductId), dependencies)
			};
			
			var validation = new ValidationResult(
				results.SelectMany(result => result.Errors));

			if (!validation.IsValid)
				return validation;

			// Commit aspect dependency versions together with the object changes.
			DependencyVersions.Touch(unitOfWork, dependencies);

			return new ValidationResult();
		}

		private static ValidationResult ValidateAspect<T>(UnitOfWork unitOfWork, Guid? nodeId, Guid projectId, string propertyName, ICollection<VersionedEntity> dependencies)
			where T : AspectNode
		{
			if (!nodeId.HasValue)
				return new ValidationResult();

			var node = unitOfWork.GetObjectByKey<T>(nodeId.Value);

			if (node == null)
			{
				return ValidationResults.Failure(
					propertyName,
					"The referenced aspect node does not exist.");
			}

			if (node.ProjectId != projectId)
			{
				return ValidationResults.Failure(
					propertyName,
					"The referenced aspect node belongs to another project.");
			}

			dependencies.Add(node);

			return new ValidationResult();
		}

		private static void CopyObject(UnitOfWork unitOfWork, EngineeringObject source, EngineeringObject target)
		{
			target.Id = source.Id;
			target.ProjectId = source.ProjectId;
			target.PowerFunctionId = source.PowerFunctionId;
			target.ControlFunctionId = source.ControlFunctionId;
			target.TopographicStructureId = source.TopographicStructureId;
			target.LocationId = source.LocationId;
			target.FunctionId = source.FunctionId;
			target.ProductId = source.ProductId;
		}

		private static EngineeringObject Snapshot(IEngineeringObject entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return new EngineeringObject
			{
				Id = entity.Id,
				ProjectId = entity.ProjectId,
				PowerFunctionId = entity.PowerFunctionId,
				ControlFunctionId = entity.ControlFunctionId,
				TopographicStructureId = entity.TopographicStructureId,
				LocationId = entity.LocationId,
				FunctionId = entity.FunctionId,
				ProductId = entity.ProductId,
				LoadedVersion = entity.LoadedVersion
			};
		}

		private (IEngineeringObject? Value, ValidationResult Validation) SetDeletionState(IEngineeringObject entity, bool markDeleted)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			// Unsaved field edits must not participate in deletion or restoration.
			var input = new EngineeringObject
			{
				Id = entity.Id,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(input,
				_ => new ValidationResult(),
					(unitOfWork, source, target) =>
					{
						target.DeletedAtUtc = markDeleted
						? DateTime.UtcNow
						: (DateTime?)null;
					},
					(unitOfWork, existing) =>
					{
						if (existing.IsMarkedForDeletion == markDeleted)
						{
							return ValidationResults.Failure(
								nameof(IEngineeringObject.IsMarkedForDeletion),
								markDeleted
									? "The object is already marked for deletion."
									: "The object is already active.");
						}

						if (markDeleted)
							return new ValidationResult();

						// Restoration must not reactivate invalid stored relationships.
						var validation = _validator.Validate(existing);

						if (!validation.IsValid)
							return validation;

						return ValidateReferences(unitOfWork, existing);
					});
			return (result.Value, result.Validation);
		}
	}

}
