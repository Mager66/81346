using ELPROCAD.ProjectExplorer.ViewModels;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel
{
	/// <summary>
	/// Represents a read-only row in the project explorer.
	/// </summary>
	internal sealed class ProjectExplorerRowViewModel : DependencyViewModel
	{
		public Guid Id { get; }

		public string Name { get; }

		public string Description { get; }

		public string Status { get; }	

		public ProjectExplorerRowViewModel(Guid id, string name, string description = "", string status = "")
		{
			if (id == Guid.Empty)
				throw new ArgumentException("An entity identifier is required.", nameof(id));

			Id = id;
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Description = description ?? throw new ArgumentNullException(nameof(description));
			Status = status ?? throw new ArgumentNullException(nameof(status));
		}
	}
}
