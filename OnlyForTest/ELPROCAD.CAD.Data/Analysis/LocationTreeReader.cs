using DevExpress.Utils.Filtering.Internal;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.CAD.Data.Analysis
{
	public sealed class LocationTreeReader
	{
		private readonly IPdsxRepository _pdsx;
		private readonly IMdbxRepository _mdbx;

		public LocationTreeReader(IPdsxRepository pdsx, IMdbxRepository mdbx)
		{
			_pdsx = pdsx ?? throw new ArgumentNullException(nameof(pdsx));
			_mdbx = mdbx ?? throw new ArgumentNullException(nameof(mdbx));
		}

		/// <summary>
		/// Reads the persisted location tree and formats it for diagnostic output.
		/// </summary>
		public IReadOnlyList<string> ReadLines()
		{
			var projects = _pdsx.Project.GetAll();

			if (projects.Count != 1)
				throw new InvalidOperationException("The analysis database must contain exactly one project.");

			var locations = _mdbx.Location.GetByProjectId(projects[0].Id);

			var children = locations
				.Where(location => location.ParentId.HasValue)
				.ToLookup(location => location.ParentId!.Value);

			var lines = new List<string>();
			var visited = new HashSet<Guid>();

			void Append(ELPROCAD.DataAccess.Contracts.MDBX.Aspects.ILocation location, int level)
			{
				if (!visited.Add(location.Id))
					throw new InvalidOperationException("The location hierarchy contains a cycle.");

				if (location.Depth != level)
					throw new InvalidOperationException(
					$"Location '{location.Designation}' has depth " +
					$"{location.Depth}, but its parent hierarchy implies {level}.");

				var indent = new string(' ', level * 2);

				lines.Add(
				$"{indent}{location.Designation} " +
				$"[Depth: {location.Depth}, Id: {location.Id}]");

				foreach (var child in children[location.Id].OrderBy(item => item.Designation, StringComparer.OrdinalIgnoreCase))
					Append(child, level + 1);
			}

			foreach (var root in locations
				.Where(location => !location.ParentId.HasValue)
				.OrderBy(location => location.Designation, StringComparer.OrdinalIgnoreCase))
				Append(root, 0);

			if (visited.Count != locations.Count)
			{
				throw new InvalidOperationException(
					"Some locations cannot be reached from a root. " +
					"Check for missing parents or cycles.");
			}

			return lines.AsReadOnly();
		}

		/// <summary>
		/// Reads persisted document, sheet and object associations for the project.
		/// </summary>
		public IReadOnlyList<string> ReadObjectLines()
		{
			var projects = _pdsx.Project.GetAll();

			if (projects.Count != 1)
			{
				throw new InvalidOperationException(
					"The analysis database must contain exactly one project.");
			}

			var projectId = projects[0].Id;

			var locations = _mdbx.Location
				.GetByProjectId(projectId)
				.ToDictionary(location => location.Id);

			var objects = _mdbx.EngineeringObject
				.GetByProjectId(projectId);

			var lines = new List<string>();

			string GetLocationPath(Guid locationId)
			{
				var parts = new List<string>();
				var visited = new HashSet<Guid>();
				Guid? currentId = locationId;

				while (currentId.HasValue)
				{
					if (!visited.Add(currentId.Value))
						throw new InvalidOperationException(
							"The location hierarchy contains a cycle.");

					if (!locations.TryGetValue(currentId.Value, out var location))
						throw new InvalidOperationException(
							$"Location '{currentId.Value}' could not be found.");

					parts.Add(location.Designation);
					currentId = location.ParentId;
				}

				parts.Reverse();

				// Diagnostic presentation only; separators are not stored.
				return string.Join(".", parts);
			}

			foreach (var document in _mdbx.Document.GetAll()
				.Where(document => document.ProjectId == projectId))
			{
				lines.Add($"Document: {document.Name} [{document.Id}]");
				lines.Add($"  Path: {document.Path}");

				foreach (var sheet in _mdbx.Sheet.GetByDocumentId(document.Id))
				{
					var sheetObjects = objects
						.Where(entity => entity.SheetId == sheet.Id)
						.OrderBy(entity => entity.Id)
						.ToList();

					lines.Add(
						$"  Sheet: {sheet.SheetNo} [{sheet.Id}], " +
						$"objects: {sheetObjects.Count}");

					foreach (var entity in sheetObjects)
					{
						var locationPath = entity.LocationId.HasValue
							? GetLocationPath(entity.LocationId.Value)
							: "(none)";

						lines.Add(
							$"    Object: {entity.Id}, location: {locationPath}");

						foreach (var represented in locations.Values
							.Where(location =>
								location.RepresentedByObjectId == entity.Id))
						{
							lines.Add(
								$"      Represents: {GetLocationPath(represented.Id)}");
						}
					}
				}
			}

			lines.Add(
				$"Objects without a sheet: " +
				$"{objects.Count(entity => !entity.SheetId.HasValue)}");

			return lines.AsReadOnly();
		}
	}
}

