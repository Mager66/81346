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
		private readonly Delete _delete;
		private readonly EngineeringObjectValidator _validator = new();

		internal EngineeringObjectRepository(Read read, Create create, Update update, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
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
			return _read.GetAll<EngineeringObject>(
				entity => entity.SheetId == sheetId);
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

		public ValidationResult Delete(IEngineeringObject entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<EngineeringObject>(
				entity.Id,
				entity.LoadedVersion,
				validateDatabase: (unitOfWork, existing) =>
				{
					var hasSymbolLinks = new XPQuery<CadSymbolLink>(unitOfWork).Any(link => link.EngineeringObjectId == existing.Id);

					if (hasSymbolLinks)
					{
						return ValidationResults.Failure(
							string.Empty,
							"The engineering object cannot be deleted while CAD symbols reference it.");
					}

					var representsNode =
						RepresentsNode<PowerFunction>(unitOfWork, existing.Id)
						|| RepresentsNode<ControlFunction>(unitOfWork, existing.Id)
						|| RepresentsNode<TopographicStructure>(unitOfWork, existing.Id)
						|| RepresentsNode<Location>(unitOfWork, existing.Id)
						|| RepresentsNode<Function>(unitOfWork, existing.Id)
						|| RepresentsNode<Product>(unitOfWork, existing.Id);

					return representsNode
						? ValidationResults.Failure(
							string.Empty,
							"The object cannot be deleted while it represents an aspect node.")
						: new ValidationResult();
				});
		}
		
		private static bool RepresentsNode<T>(UnitOfWork unitOfWork, Guid objectId)	where T : AspectNode
		{
			return new XPQuery<T>(unitOfWork)
				.Any(node => node.RepresentedByObjectId == objectId);
		}

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

			if (entity.SheetId.HasValue)
			{
				var sheet = unitOfWork.GetObjectByKey<Sheet>(
					entity.SheetId.Value);

				if (sheet == null)
				{
					return ValidationResults.Failure(
						nameof(entity.SheetId),
						"The sheet does not exist.");
				}

				var document = unitOfWork.GetObjectByKey<Document>(
					sheet.DocumentId);

				if (document == null || document.ProjectId != entity.ProjectId)
				{
					return ValidationResults.Failure(
						nameof(entity.SheetId),
						"The sheet must belong to a document in the object's project.");
				}

				dependencies.Add(sheet);
			}

			// Commit dependency versions together with the new object.
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
			target.SheetId = source.SheetId;
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
				SheetId = entity.SheetId,
				PowerFunctionId = entity.PowerFunctionId,
				ControlFunctionId = entity.ControlFunctionId,
				TopographicStructureId = entity.TopographicStructureId,
				LocationId = entity.LocationId,
				FunctionId = entity.FunctionId,
				ProductId = entity.ProductId,
				LoadedVersion = entity.LoadedVersion
			};
		}
	}
	
}
