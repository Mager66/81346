using DevExpress.Xpo;
using ELPROCAD.DataAccess.Changes.Locations;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Aspects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Aspects
{
	internal sealed class LocationRepository : ILocationRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly Delete _delete;
		private readonly MoveLocation _move;
		private readonly AspectNodeValidator _validator = new();
		private readonly LocationChangeWriter _changeWriter;

		internal LocationRepository(Read read, Create create, Update update, Delete delete, MoveLocation move, LocationChangeWriter changeWriter)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
			_move = move ?? throw new ArgumentNullException(nameof(move));
			_changeWriter = changeWriter ?? throw new ArgumentNullException(nameof(changeWriter));
		}

		public ILocation? GetById(Guid id) => _read.GetById<Location>(id);

		public IReadOnlyList<ILocation> GetRoots(Guid projectId)
		{
			return OrderByDesignation(
				_read.GetAll<Location>(
					entity => entity.ProjectId == projectId
						&& entity.ParentId == null));
		}

		public IReadOnlyList<ILocation> GetChildren(Guid parentId)
		{
			return OrderByDesignation(
				_read.GetAll<Location>(
					entity => entity.ParentId == parentId));
		}

		public IReadOnlyList<ILocation> GetByProjectId(Guid projectId)
		{
			return _read.GetAll<Location>(
					entity => entity.ProjectId == projectId)
				.OrderBy(entity => entity.Depth)
				.ThenBy(
					entity => entity.Designation,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ILocation>()
				.ToList()
				.AsReadOnly();
		}

		public (ILocation? Value, ValidationResult Validation) Create(Guid projectId, string designation, Guid? parentId = null)
		{
			var input = new Location
			{
				ProjectId = projectId,
				Designation = designation,
				ParentId = parentId
			};

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyLocation,
				ValidateCreation);

			return (result.Value, result.Validation);
		}

		private static ValidationResult ValidateCreation(UnitOfWork unitOfWork,	Location entity)
		{
			Location? parent = null;

			if (entity.ParentId.HasValue)
			{
				parent = unitOfWork.GetObjectByKey<Location>(
					entity.ParentId.Value);

				if (parent == null)
				{
					return ValidationResults.Failure(
						nameof(ILocation.ParentId),
						"The parent location does not exist.");
				}

				if (parent.ProjectId != entity.ProjectId)
				{
					return ValidationResults.Failure(
						nameof(ILocation.ParentId),
						"The parent location belongs to another project.");
				}

				entity.Depth = checked(parent.Depth + 1);
			}
			else
			{
				entity.Depth = 0;
			}

			var duplicate = new XPQuery<Location>(unitOfWork)
				.Any(item =>
					item.ProjectId == entity.ProjectId &&
					item.Depth == entity.Depth &&
					item.Designation == entity.Designation);

			if (duplicate)
			{
				return ValidationResults.Failure(
					nameof(ILocation.Designation),
					"The designation is already used at this depth in the project's location structure.");
			}

			if (parent != null)
			{
				// Commit the parent dependency version together with the new child.
				DependencyVersions.Touch(unitOfWork, new[] { parent });
			}

			return new ValidationResult();
		}

		public (ILocation? Value, ValidationResult Validation) Update(ILocation entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			var input = new Location
			{
				Id = entity.Id,
				ProjectId = entity.ProjectId,
				Designation = entity.Designation,
				ParentId = entity.ParentId,
				Depth = entity.Depth,
				RepresentedByObjectId = entity.RepresentedByObjectId,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyChanges,
				(unitOfWork, existing) =>
				{
					if (input.ProjectId != existing.ProjectId)
					{
						return ValidationResults.Failure(
							nameof(ILocation.ProjectId),
							"Changing the project is not supported by this operation.");
					}
					
					if (input.ParentId != existing.ParentId || input.Depth != existing.Depth)
					{
						return ValidationResults.Failure(
							nameof(ILocation.ParentId),
							"Changing the node's position is not supported by this operation.");
					}
					
					if (input.RepresentedByObjectId != existing.RepresentedByObjectId)
					{
						return ValidationResults.Failure(
							nameof(ILocation.RepresentedByObjectId),
							"Changing the representing object is not supported by this operation.");
					}
					
					var duplicate = new XPQuery<Location>(unitOfWork)
					.Any(item =>
						item.ProjectId == existing.ProjectId &&
						item.Depth == existing.Depth &&
						item.Designation == input.Designation &&
						item.Id != existing.Id);
					
					return duplicate
					? ValidationResults.Failure(
						nameof(ILocation.Designation),
						"The designation is already used at this depth in the project's location structure.")
					: new ValidationResult();
		});

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(ILocation entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<Location>(
				entity.Id,
				entity.LoadedVersion,
				validateDatabase: (unitOfWork, existing) =>
				{
					var hasChildren = new XPQuery<Location>(unitOfWork)
						.Any(item => item.ParentId == existing.Id);

					if (hasChildren)
					{
						return ValidationResults.Failure(
							string.Empty,
							"The location cannot be deleted while it contains child locations.");
					}

					var hasObjects = new XPQuery<EngineeringObject>(unitOfWork)
						.Any(item => item.LocationId == existing.Id);

					if (hasObjects)
					{
						return ValidationResults.Failure(
							string.Empty,
							"The location cannot be deleted while engineering objects belong to it.");
					}

					if (existing.RepresentedByObjectId.HasValue)
					{
						return ValidationResults.Failure(
							nameof(ILocation.RepresentedByObjectId),
							"The location cannot be deleted while it is represented by an engineering object.");
					}

					return new ValidationResult();
				});
		}

		public (ILocation? Value, ValidationResult Validation) SetRepresentingObject(ILocation location, Guid? objectId)
		{
			if (location == null)
				throw new ArgumentNullException(nameof(location));

			if (objectId.HasValue && objectId.Value == Guid.Empty)
			{
				return (null, ValidationResults.Failure(
					nameof(ILocation.RepresentedByObjectId),
					"The representing object identifier must not be an empty GUID."));
			}

			var input = new Location
			{
				Id = location.Id,
				LoadedVersion = location.LoadedVersion
			};

			var result = _update.Save(input,
				// This operation changes only the representing-object reference.
				_ => new ValidationResult(),
				(unitOfWork, source, target) =>
				{
					target.RepresentedByObjectId = objectId;
				},
				(unitOfWork, existing) =>
				{
					if (!objectId.HasValue)
						return new ValidationResult();
					
					var engineeringObject =
						unitOfWork.GetObjectByKey<EngineeringObject>(objectId.Value);
					
					if (engineeringObject == null)
					{
						return ValidationResults.Failure(
							nameof(ILocation.RepresentedByObjectId),
							"The engineering object does not exist.");
					}
					
					if (engineeringObject.ProjectId != existing.ProjectId)
					{
						return ValidationResults.Failure(
							nameof(ILocation.RepresentedByObjectId),
							"The engineering object belongs to another project.");
					}
					
					// Include the referenced object in concurrency checks.
					DependencyVersions.Touch(
						unitOfWork,
						new[] { engineeringObject });
					
					return new ValidationResult();
				});

			return (result.Value, result.Validation);
		}

		public (ILocation? Value, ValidationResult Validation) Move(ILocation location,	Guid? parentId)
		{
			var result = _move.Execute(location, parentId);

			return (result.Value, result.Validation);
		}

		public (ILocation? Value, ValidationResult Validation) ChangePath(ILocation location, IReadOnlyList<string> targetLevels) => _changeWriter.Apply(location, targetLevels);

		private static IReadOnlyList<ILocation> OrderByDesignation(IEnumerable<Location> locations)
		{
			return locations
				.OrderBy(
					entity => entity.Designation,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ILocation>()
				.ToList()
				.AsReadOnly();
		}

		private static void CopyLocation(UnitOfWork unitOfWork, Location source, Location target)
		{
			target.Id = source.Id;
			target.ProjectId = source.ProjectId;
			target.Designation = source.Designation;
			target.ParentId = source.ParentId;
			target.Depth = source.Depth;
		}

		private static void CopyChanges(UnitOfWork unitOfWork, Location source, Location target)
		{
			// Renaming a node does not alter its identity or structural relationships.
			target.Designation = source.Designation;
		}
	}
}
