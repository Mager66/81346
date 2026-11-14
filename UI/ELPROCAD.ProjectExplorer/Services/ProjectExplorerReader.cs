using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel;

namespace ELPROCAD.ProjectExplorer.Services
{
	/// <summary>
	/// Reads project navigation and grid rows without retaining a database session.
	/// </summary>
	internal sealed class ProjectExplorerReader
	{
		private readonly IMdbxRepository _mdbx;
		private readonly Guid _projectId;

		public ProjectExplorerReader(IMdbxRepository mdbx, Guid projectId)
		{
			_mdbx = mdbx ?? throw new ArgumentNullException(nameof(mdbx));
			if(projectId == Guid.Empty)
				throw new ArgumentException("A project identifier is required.", nameof(projectId));

			_projectId = projectId;
		}

		/// <summary>
		/// Creates a view model populated from the selected project's database.
		/// Must be called on the window's UI thread.
		/// </summary>
		public ProjectExplorerViewModel CreateViewModel()
		{
			var model = new ProjectExplorerViewModel(ReadRows);

			foreach(var node in ReadNodes())
				model.Nodes.Add(node);

			model.SelectedNode = model.Nodes[0];

			return model;
		}

		/// <summary>
		/// Builds the location hierarchy and the document/sheet hierarchy.
		/// </summary>
		public IReadOnlyList<ProjectTreeNodeViewModel> ReadNodes()
		{
			var locationRoot = new ProjectTreeNodeViewModel(
			   ProjectTreeNodeKind.Locations, "Locations")
			{ IsExpanded = true };
			var documentRoot = new ProjectTreeNodeViewModel(
				ProjectTreeNodeKind.Documents, "Documents")
			{ IsExpanded = true };

			var locations = _mdbx.Location.GetByProjectId(_projectId);
			var children = locations.Where(x => x.ParentId.HasValue)
				.ToLookup(x => x.ParentId!.Value);
			var visited = new HashSet<Guid>();

			ProjectTreeNodeViewModel CreateLocationNode(ILocation location)
			{
				if (!visited.Add(location.Id))
					throw new InvalidOperationException("The location hierarchy contains a cycle or duplicate identifier.");

				var node = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Location, location.Designation, location.Id);

				foreach (var child in children[location.Id]
					.OrderBy(x => x.Designation, StringComparer.OrdinalIgnoreCase))
				{
					node.Children.Add(CreateLocationNode(child));
				}

				return node;
			}

			foreach (var location in locations.Where(x => !x.ParentId.HasValue)
			   .OrderBy(x => x.Designation, StringComparer.OrdinalIgnoreCase))
			{
				locationRoot.Children.Add(CreateLocationNode(location));
			}

			if (visited.Count != locations.Count)
				throw new InvalidOperationException("Some locations cannot be reached from a root. Check for missing parents or cycles.");

			foreach (var document in _mdbx.Document.GetAll()
				.Where(x => x.ProjectId == _projectId)
				.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
			{
				var documentNode = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Document, document.Name, document.Id);

				foreach (var sheet in _mdbx.Sheet.GetByDocumentId(document.Id)
					.OrderBy(x => x.SheetNo, StringComparer.OrdinalIgnoreCase))
				{
					documentNode.Children.Add(new ProjectTreeNodeViewModel(
						ProjectTreeNodeKind.Sheet, sheet.SheetNo, sheet.Id));
				}

				documentRoot.Children.Add(documentNode);
			}

			return new[] { locationRoot, documentRoot };
		}

		/// <summary>
		/// Reads rows for the selected node. Grid sorting remains a view responsibility.
		/// </summary>
		public IEnumerable<ProjectExplorerRow> ReadRows(ProjectTreeNodeViewModel node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));

			switch(node.Kind)
			{
				case ProjectTreeNodeKind.Locations:
					return ReadObjectRows(_mdbx.EngineeringObject.GetByProjectId(_projectId)
						.Where(x => x.LocationId.HasValue));

				//case ProjectTreeNodeKind.Location:
				//	// Only direct members are shown, not members of descendant nodes.
				//	return ReadObjectRows(_mdbx.EngineeringObject.GetByLocationId(node.EntityId!.Value));
				case ProjectTreeNodeKind.Location:
					return ReadLocationSubtree(node.EntityId!.Value);

				case ProjectTreeNodeKind.Documents:
					return _mdbx.Document.GetAll()
						.Where(x => x.ProjectId == _projectId)
						.Select(x => new ProjectExplorerRow(x.Id, x.Name, x.Path))
						.ToArray();

				case ProjectTreeNodeKind.Document:
					return _mdbx.Sheet.GetByDocumentId(node.EntityId!.Value)
						.Select(x => new ProjectExplorerRow(x.Id, x.SheetNo, "Sheet"))
						.ToArray();

				case ProjectTreeNodeKind.Sheet:
					// The repository resolves sheet membership through CAD symbol links.
					return ReadObjectRows(_mdbx.EngineeringObject.GetBySheetId(node.EntityId!.Value));

				default:
					throw new ArgumentOutOfRangeException(nameof(node), "The node kind is not supported.");
			}			 
		}

		private IReadOnlyList<ProjectExplorerRow> ReadObjectRows(IEnumerable<IEngineeringObject> objects)
		{
			var locations = _mdbx.Location.GetByProjectId(_projectId)
			  .ToDictionary(x => x.Id);

			string ReadLocationPath(Guid id)
			{
				var parts = new List<string>();
				var visited = new HashSet<Guid>();
				Guid? currentId = id;

				while (currentId.HasValue)
				{
					if (!visited.Add(currentId.Value))
						throw new InvalidOperationException("The location hierarchy contains a cycle.");

					if (!locations.TryGetValue(currentId.Value, out var location))
						throw new InvalidOperationException($"Location '{currentId.Value}' was not found in the project.");

					parts.Add(location.Designation);
					currentId = location.ParentId;
				}

				parts.Reverse();
				// TODO: Apply configured designation formatting instead of a fixed separator.
				return string.Join(".", parts);
			}

			return objects.Where(x => x.ProjectId == _projectId)
			   .GroupBy(x => x.Id)
			   .Select(group => group.First())
			   .Select(entity => new ProjectExplorerRow(
				   entity.Id,
				   entity.LocationId.HasValue
					   ? ReadLocationPath(entity.LocationId.Value)
					   : entity.Id.ToString(),
				   entity.IsMarkedForDeletion ? "Marked for deletion" : "Engineering object"))
			   .ToArray();
		}

		/// <summary>
		/// Reads objects assigned to the selected location or any descendant.
		/// </summary>
		private IReadOnlyList<ProjectExplorerRow> ReadLocationSubtree(Guid locationId)
		{
			var locations = _mdbx.Location.GetByProjectId(_projectId);

			var children = locations
				.Where(location => location.ParentId.HasValue)
				.ToLookup(location => location.ParentId!.Value);

			var locationIds = new HashSet<Guid>();
			var pending = new Stack<Guid>();
			pending.Push(locationId);

			while (pending.Count > 0)
			{
				var currentId = pending.Pop();

				if (!locationIds.Add(currentId))
				{
					throw new InvalidOperationException(
						"The location hierarchy contains a cycle or duplicate identifier.");
				}

				foreach (var child in children[currentId])
					pending.Push(child.Id);
			}

			var objects = _mdbx.EngineeringObject
				.GetByProjectId(_projectId)
				.Where(entity =>
					entity.LocationId.HasValue &&
					locationIds.Contains(entity.LocationId.Value));

			return ReadObjectRows(objects);
		}
	}
}
