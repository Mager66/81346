using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Changes.Locations
{
	internal sealed class LocationChangePlanner
	{
		/// <summary>
		/// Classifies a designation change without modifying persistent data.
		/// The target path must already be parsed into individual levels.
		/// </summary>
		internal LocationChangePlan Plan(ILocation source, IReadOnlyList<string> targetLevels, IEnumerable<ILocation> projectLocations)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			if (targetLevels == null)
				throw new ArgumentNullException(nameof(targetLevels));

			if (projectLocations == null)
				throw new ArgumentNullException(nameof(projectLocations));

			if (targetLevels.Count == 0 || targetLevels.Any(level => string.IsNullOrWhiteSpace(level)))			
				throw new ArgumentException("The target path must contain non-empty designation levels.", nameof(targetLevels));

			var locations = projectLocations
				.Where(location => location.ProjectId == source.ProjectId)
				.ToDictionary(location => location.Id);

			var currentLevels = GetCurrentPath(source, locations);
			var target = targetLevels.ToArray();

			var parentChanged = !currentLevels
				.Take(currentLevels.Count - 1)
				.SequenceEqual(
					target.Take(target.Length - 1),
					StringComparer.OrdinalIgnoreCase);

			var designationChanged = !string.Equals(
				source.Designation,
				target[target.Length - 1],
				StringComparison.OrdinalIgnoreCase);

			var kind = parentChanged
				? designationChanged
					? LocationChangeKind.MoveAndRename
					: LocationChangeKind.Move
				: designationChanged
					? LocationChangeKind.Rename
					: LocationChangeKind.None;

			return new LocationChangePlan(
				source.Id,
				source.LoadedVersion,
				kind,
				target);
		}

		private static IReadOnlyList<string> GetCurrentPath(ILocation source, IReadOnlyDictionary<Guid, ILocation> locations)
		{
			var levels = new List<string>();
			var visited = new HashSet<Guid>();
			var current = source;

			while (true)
			{
				if (!visited.Add(current.Id))
				{
					throw new InvalidOperationException(
						"The location hierarchy contains a cycle.");
				}

				levels.Add(current.Designation);

				if (!current.ParentId.HasValue)
					break;

				if (!locations.TryGetValue(
					current.ParentId.Value,
					out var parent))
				{
					throw new InvalidOperationException(
						"The parent location could not be found in the project.");
				}

				current = parent;
			}

			levels.Reverse();
			return levels.AsReadOnly();
		}
	}
}
