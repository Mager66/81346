using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Validation;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.CRUD
{
	internal sealed class MoveLocation
	{
		private readonly IDatabaseAccess _database;
		private readonly Action? _afterStaging;

		internal MoveLocation(IDatabaseAccess database, Action? afterStaging = null)
		{
			_database = database ?? throw new ArgumentNullException(nameof(database));
			_afterStaging = afterStaging;
		}

		internal (Location? Value, ValidationResult Validation) Execute(ILocation location, Guid? parentId)
		{
			if (location == null)
				throw new ArgumentNullException(nameof(location));

			if (!location.LoadedVersion.HasValue)
			{
				return (null, ValidationResults.Failure(
					nameof(location.LoadedVersion),
					"The original version is missing. Reload the location."));
			}

			if (parentId == Guid.Empty)
			{
				return (null, ValidationResults.Failure(
					nameof(location.ParentId),
					"The parent identifier must not be empty."));
			}

			try
			{
				return _database.ExecuteExplicit<
					(Location? Value, ValidationResult Validation)>(
					uow => ExecuteMove(uow, location, parentId));
			}
			catch (LockingException)
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The location or related data changed during the move. " +
					"No changes were saved. Reload the data."));
			}
			catch (Exception exception)
				when (VistaDbErrors.IsDuplicateKey(exception))
			{
				return (null, ValidationResults.Failure(
					nameof(location.Designation),
					"The move would create a duplicate designation at the same depth. " +
					"No changes were saved."));
			}
		}

		private (Location? Value, ValidationResult Validation) ExecuteMove(ExplicitUnitOfWork uow, ILocation location, Guid? parentId)
		{
			var existing = uow.GetObjectByKey<Location>(location.Id);

			if (existing == null)
			{
				return (null, ValidationResults.Failure(
					nameof(location.Id),
					"The location no longer exists."));
			}

			if (Concurrency.GetVersion(uow, existing) != location.LoadedVersion)
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The location has changed since it was loaded. Reload the data."));
			}

			Location? parent = null;

			if (parentId.HasValue)
			{
				parent = uow.GetObjectByKey<Location>(parentId.Value);

				if (parent == null)
				{
					return (null, ValidationResults.Failure(
						nameof(location.ParentId),
						"The parent location does not exist."));
				}

				if (parent.ProjectId != existing.ProjectId)
				{
					return (null, ValidationResults.Failure(
						nameof(location.ParentId),
						"The parent location belongs to another project."));
				}
			}

			// An unchanged parent does not require a database update.
			if (existing.ParentId == parentId)
			{
				Concurrency.CaptureVersion(uow, existing);

				return (existing, new ValidationResult());
			}

			var projectId = existing.ProjectId;

			var projectLocations = new XPQuery<Location>(uow)
				.Where(item => item.ProjectId == projectId)
				.ToList();

			var subtree = GetSubtree(existing, projectLocations);
			var subtreeIds = new HashSet<Guid>(
				subtree.Select(item => item.Id));

			if (parentId.HasValue && subtreeIds.Contains(parentId.Value))
			{
				return (null, ValidationResults.Failure(
					nameof(location.ParentId),
					"A location cannot be moved beneath itself or one of its descendants."));
			}

			var newRootDepth = parent == null
				? 0
				: checked(parent.Depth + 1);

			var depthDifference = checked(newRootDepth - existing.Depth);

			var finalDepths = subtree.ToDictionary(
				item => item.Id,
				item => checked(item.Depth + depthDifference));

			if (finalDepths.Values.Any(depth => depth < 0))
			{
				return (null, ValidationResults.Failure(
					nameof(location.ParentId),
					"The location hierarchy contains inconsistent depths."));
			}

			var outsideSubtree = projectLocations
				.Where(item => !subtreeIds.Contains(item.Id))
				.ToList();

			foreach (var item in subtree)
			{
				var finalDepth = finalDepths[item.Id];

				var duplicate = outsideSubtree.Any(candidate =>
					candidate.Depth == finalDepth &&
					string.Equals(
						candidate.Designation,
						item.Designation,
						StringComparison.OrdinalIgnoreCase));

				if (duplicate)
				{
					return (null, ValidationResults.Failure(
						nameof(location.Designation),
						$"Designation '{item.Designation}' already exists " +
						$"at depth {finalDepth}."));
				}
			}

			Location? previousParent = null;

			if (existing.ParentId.HasValue)
			{
				previousParent = uow.GetObjectByKey<Location>(
					existing.ParentId.Value);

				if (previousParent == null ||
					previousParent.ProjectId != existing.ProjectId)
				{
					return (null, ValidationResults.Failure(
						nameof(location.ParentId),
						"The current parent reference is invalid."));
				}
			}

			// Temporarily move the subtree outside the valid depth range.
			// Relative depth differences are preserved, avoiding index collisions.
			foreach (var item in subtree)
			{
				if (item.Depth < 0)
				{
					throw new InvalidOperationException(
						"A persisted location has a negative depth.");
				}

				item.Depth = checked(-item.Depth - 1);
				uow.Save(item);
			}

			uow.FlushChanges();

			// Test hook: failures here must roll back the first write stage.
			_afterStaging?.Invoke();

			foreach (var item in subtree)
			{
				item.Depth = finalDepths[item.Id];
				uow.Save(item);
			}

			existing.ParentId = parentId;
			uow.Save(existing);

			// Changing child membership also changes the parent versions.
			if (previousParent != null)
				DependencyVersions.Touch(uow, new[] { previousParent });

			if (parent != null)
				DependencyVersions.Touch(uow, new[] { parent });

			uow.CommitTransaction();

			Concurrency.CaptureVersion(uow, existing);

			return (existing, new ValidationResult());
		}

		internal static List<Location> GetSubtree(Location root, IEnumerable<Location> locations)
		{
			var childrenByParent = locations
				.Where(item => item.ParentId.HasValue)
				.ToLookup(item => item.ParentId!.Value);

			var result = new List<Location>();
			var visited = new HashSet<Guid>();
			var pending = new Queue<Location>();

			pending.Enqueue(root);

			while (pending.Count > 0)
			{
				var current = pending.Dequeue();

				if (!visited.Add(current.Id))
				{
					throw new InvalidOperationException(
						"The location hierarchy contains a cycle.");
				}

				result.Add(current);

				foreach (var child in childrenByParent[current.Id])
					pending.Enqueue(child);
			}

			return result;
		}
	}
}