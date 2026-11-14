using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{

		private static string[] ParseLocation(string value)
		{
			var text = value.Trim();

			if (text.StartsWith(LocationPrefix, StringComparison.Ordinal))
				text = text.Substring(LocationPrefix.Length);

			// Membership notation points to the same location node.
			// Object membership and representation are handled separately.
			if (text.EndsWith(LevelSeparator.ToString(), StringComparison.Ordinal))
				text = text.Substring(0, text.Length - 1);

			string[] levels = text
				.Split(new[] { LevelSeparator }, StringSplitOptions.None)
				.Select(level => level.Trim())
				.ToArray();

			if (levels.Any(level => string.IsNullOrWhiteSpace(level)))
				throw new ArgumentException($"Location designation '{value}' contains an empty level.", nameof(value));

			return levels;
		}

		private static string[] ReadPath(Guid locationId, IReadOnlyDictionary<Guid, ILocation> locations)
		{
			var levels = new List<string>();
			var visited = new HashSet<Guid>();
			Guid? currentId = locationId;

			while (currentId.HasValue)
			{
				if (!visited.Add(currentId.Value))
					throw new InvalidOperationException(
						"The location hierarchy contains a cycle.");

				if (!locations.TryGetValue(currentId.Value, out var location))
					throw new InvalidOperationException(
						"The location hierarchy contains a missing node.");

				levels.Add(location.Designation);
				currentId = location.ParentId;
			}

			levels.Reverse();
			return levels.ToArray();
		}

		private static bool SamePath(IEnumerable<string> first, IEnumerable<string> second)
		{
			return first.SequenceEqual(
				second,
				StringComparer.OrdinalIgnoreCase);
		}

		private string[] RebaseParentPath(string[] requestedLevels, IReadOnlyDictionary<Guid, string[]> originalPaths)
		{
			var parentLength = requestedLevels.Length - 1;

			// Locate the deepest known ancestor in the original parent path.
			foreach (var candidate in originalPaths
				.Where(item => item.Value.Length <= parentLength)
				.OrderByDescending(item => item.Value.Length))
			{
				if (!SamePath(
					candidate.Value,
					requestedLevels.Take(candidate.Value.Length)))
				{
					continue;
				}

				var current = _mdbx.Location.GetById(candidate.Key);

				if (current == null)
					continue;

				var currentLocations = _mdbx.Location
					.GetByProjectId(current.ProjectId)
					.ToDictionary(location => location.Id);

				return ReadPath(current.Id, currentLocations)
					.Concat(requestedLevels.Skip(candidate.Value.Length))
					.ToArray();
			}

			return requestedLevels.ToArray();
		}

		private ILocation ResolveLocationPath(Guid projectId, IReadOnlyList<string> levels, bool createMissing)
		{
			var locations = _mdbx.Location
				.GetByProjectId(projectId)
				.ToList();

			ILocation? current = null;

			for (var depth = 0; depth < levels.Count; depth++)
			{
				var designation = levels[depth];
				var parentId = current?.Id;

				var existing = locations.SingleOrDefault(location =>
					location.ParentId == parentId &&
					string.Equals(
						location.Designation,
						designation,
						StringComparison.OrdinalIgnoreCase));

				if (existing != null)
				{
					current = existing;
					continue;
				}

				if (!createMissing)
					throw new InvalidOperationException(
						$"Location '{designation}' does not exist.");

				current = Require(_mdbx.Location.Create(
					projectId,
					designation,
					parentId));

				locations.Add(current);
			}

			return current
				?? throw new InvalidOperationException(
					"The designation contains no location levels.");
		}

		/// <summary>
		/// Applies a requested location path while preserving object identity.
		/// </summary>
		private (ILocation? Value, ValidationResult Validation) UpdateLinkedLocation(
			Guid objectId,
			Guid projectId,
			IReadOnlyList<string> targetLevels)
		{
			var entity = _mdbx.EngineeringObject.GetById(objectId);

			if (entity == null)
			{
				return (null, new ValidationResult(new[]
				{
			new ValidationFailure(
				nameof(objectId),
				"The engineering object referenced by XData no longer exists.")
		}));
			}

			if (entity.ProjectId != projectId)
			{
				return (null, new ValidationResult(new[]
				{
			new ValidationFailure(
				nameof(entity.ProjectId),
				"The engineering object belongs to another project.")
		}));
			}

			var location = GetRepresentedLocation(entity, projectId);

			return _mdbx.Location.ChangePath(location, targetLevels);
		}
	}
}

