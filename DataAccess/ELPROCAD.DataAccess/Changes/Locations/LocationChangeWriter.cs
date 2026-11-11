using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Aspects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Changes.Locations
{
	internal sealed class LocationChangeWriter
	{
		private readonly IDatabaseAccess _database;
		private readonly Action? _afterStaging;
		private readonly LocationChangePlanner _planner = new();
		private readonly AspectNodeValidator _validator = new();

		internal LocationChangeWriter(IDatabaseAccess database, Action? afterStaging = null)
		{
			_database = database ?? throw new ArgumentNullException(nameof(database));

			_afterStaging = afterStaging;
		}

		internal (ILocation? Value, ValidationResult Validation) Apply(ILocation source, IReadOnlyList<string> targetLevels)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			if (targetLevels == null)
				throw new ArgumentNullException(nameof(targetLevels));

			if (!source.LoadedVersion.HasValue)
				return (null, ValidationResults.Failure(nameof(source.LoadedVersion), "The original version is missing. Reload the location."));

			var target = targetLevels.ToArray();

			if (target.Length == 0 ||
				target.Any(level =>
					string.IsNullOrWhiteSpace(level) || level.Length > 255))
			{
				return (null, ValidationResults.Failure(
					nameof(targetLevels),
					"Each designation level must contain between 1 and 255 characters."));
			}

			try
			{
				return _database.ExecuteExplicit<
					(ILocation? Value, ValidationResult Validation)>(
					uow => Apply(uow, source, target));
			}
			catch (LockingException)
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The location or related data changed during the operation. " +
					"No changes were saved. Reload the data."));
			}
			catch (Exception exception) when (VistaDbErrors.IsDuplicateKey(exception))
			{
				return (null, ValidationResults.Failure(
					nameof(source.Designation),
					"The requested change conflicts with an existing designation. " +
					"No changes were saved."));
			}
		}

		private (ILocation? Value, ValidationResult Validation) Apply(ExplicitUnitOfWork uow, ILocation source, string[] targetLevels)
		{
			var existing = uow.GetObjectByKey<Location>(source.Id);

			if (existing == null)
			{
				return (null, ValidationResults.Failure(
					nameof(source.Id),
					"The location no longer exists."));
			}

			if (existing.ProjectId != source.ProjectId ||
				Concurrency.GetVersion(uow, existing) != source.LoadedVersion)
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The location has changed. Reload it before applying the change."));
			}

			var projectId = existing.ProjectId;

			var locations = new XPQuery<Location>(uow)
				.Where(location => location.ProjectId == projectId)
				.ToList();

			// Rebuild the plan against the current database state.
			var plan = _planner.Plan(existing, targetLevels, locations);

			if (plan.Kind == LocationChangeKind.None)
			{
				Concurrency.CaptureVersion(uow, existing);
				return (existing, new ValidationResult());
			}

			var subtree = MoveLocation.GetSubtree(existing, locations);
			var subtreeIds = new HashSet<Guid>(
				subtree.Select(location => location.Id));

			var previousParent = existing.ParentId.HasValue
				? locations.Single(location =>
					location.Id == existing.ParentId.Value)
				: null;

			var newParents = new List<Location>();
			Location? targetParent = null;

			// Resolve or prepare the target parent path without saving anything.
			for (var depth = 0; depth < plan.ParentLevels.Count; depth++)
			{
				var designation = plan.ParentLevels[depth];
				var parentId = targetParent?.Id;

				var candidate = locations.SingleOrDefault(location =>
					location.Depth == depth &&
					string.Equals(
						location.Designation,
						designation,
						StringComparison.OrdinalIgnoreCase));

				if (candidate != null)
				{
					if (subtreeIds.Contains(candidate.Id))
					{
						return (null, ValidationResults.Failure(
							nameof(source.ParentId),
							"A location cannot be moved beneath itself or a descendant."));
					}

					if (candidate.ParentId != parentId)
					{
						return (null, ValidationResults.Failure(
							nameof(source.ParentId),
							$"Designation '{designation}' already exists " +
							$"at depth {depth} beneath another parent."));
					}

					targetParent = candidate;
					continue;
				}

				candidate = new Location
				{
					ProjectId = projectId,
					Designation = designation,
					ParentId = parentId,
					Depth = depth
				};

				var validation = _validator.Validate(candidate);

				if (!validation.IsValid)
					return (null, validation);

				newParents.Add(candidate);
				locations.Add(candidate);
				targetParent = candidate;
			}

			var newDepth = plan.ParentLevels.Count;
			var depthDifference = checked(newDepth - existing.Depth);

			var originalDepths = subtree.ToDictionary(
				location => location.Id,
				location => location.Depth);

			var finalDepths = subtree.ToDictionary(
				location => location.Id,
				location => checked(location.Depth + depthDifference));

			if (originalDepths.Values.Any(depth => depth < 0) ||
				finalDepths.Values.Any(depth => depth < 0))
			{
				return (null, ValidationResults.Failure(
					string.Empty,
					"The location hierarchy contains invalid depths."));
			}

			// Validate the complete final structure, including prepared parents.
			var finalState = locations.Select(location => new
			{
				Depth = finalDepths.TryGetValue(location.Id, out var depth)
					? depth
					: location.Depth,

				Designation = location.Id == existing.Id
					? plan.Designation
					: location.Designation
			});

			foreach (var level in finalState.GroupBy(location => location.Depth))
			{
				var duplicate = level
					.GroupBy(
						location => location.Designation,
						StringComparer.OrdinalIgnoreCase)
					.FirstOrDefault(group => group.Count() > 1);

				if (duplicate != null)
				{
					return (null, ValidationResults.Failure(
						nameof(source.Designation),
						$"Designation '{duplicate.Key}' would occur more than once " +
						$"at depth {level.Key}."));
				}
			}

			// Stage existing nodes outside the valid depth range.
			// This avoids intermediate unique-index collisions.
			foreach (var location in subtree)
			{
				location.Depth = checked(-originalDepths[location.Id] - 1);
				uow.Save(location);
			}

			uow.FlushChanges();

			// Test hook used to verify rollback after database writes.
			_afterStaging?.Invoke();

			foreach (var parent in newParents)
				uow.Save(parent);

			existing.ParentId = targetParent?.Id;
			existing.Designation = plan.Designation;

			foreach (var location in subtree)
			{
				location.Depth = finalDepths[location.Id];
				uow.Save(location);
			}

			if (previousParent?.Id != targetParent?.Id)
			{
				var changedParents = new List<Location>();

				if (previousParent != null)
					changedParents.Add(previousParent);

				if (targetParent != null)
					changedParents.Add(targetParent);

				DependencyVersions.Touch(uow, changedParents);

				RemoveUnusedAncestors(
					uow,
					previousParent,
					locations);
			}

			uow.CommitTransaction();

			Concurrency.CaptureVersion(uow, existing);

			return (existing, new ValidationResult());
		}

		private static void RemoveUnusedAncestors(ExplicitUnitOfWork uow, Location? candidate, List<Location> locations)
		{
			while (candidate != null)
			{
				var candidateId = candidate.Id;

				var hasChildren = locations.Any(location =>
					location.ParentId == candidateId);

				if (hasChildren || candidate.RepresentedByObjectId.HasValue)
					return;

				var hasObjects = new XPQuery<EngineeringObject>(uow)
					.Any(entity => entity.LocationId == candidateId);

				if (hasObjects)
					return;

				var parentId = candidate.ParentId;

				uow.Delete(candidate);
				locations.Remove(candidate);

				candidate = parentId.HasValue
					? locations.Single(location => location.Id == parentId.Value)
					: null;

				// Removing a child changes the parent's dependency version.
				if (candidate != null)
					DependencyVersions.Touch(uow, new[] { candidate });
			}
		}
	}
}
