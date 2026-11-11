namespace ELPROCAD.DataAccess.Changes.Locations
{
	/// <summary>
	/// Describes the requested change to an existing location node.
	/// </summary>
	internal sealed class LocationChangePlan
	{
		public Guid LocationId { get; }

		public int? LoadedVersion { get; }

		public LocationChangeKind Kind { get; }

		public string Designation { get; }

		/// <summary>
		/// Gets the desired parent path.
		/// An empty collection means the root level.
		/// </summary>
		public IReadOnlyList<string> ParentLevels { get; }

		internal LocationChangePlan(Guid locationId, int? loadedVersion, LocationChangeKind kind, IEnumerable<string> targetLevels)
		{
			LocationId = locationId;
			LoadedVersion = loadedVersion;
			Kind = kind;

			var levels = targetLevels.ToArray();

			Designation = levels[levels.Length - 1];
			ParentLevels = Array.AsReadOnly(
				levels.Take(levels.Length - 1).ToArray());
		}
	}
}
