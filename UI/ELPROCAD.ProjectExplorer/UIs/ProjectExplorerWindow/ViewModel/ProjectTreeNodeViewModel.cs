using ELPROCAD.ProjectExplorer.ViewModels;
using System.Collections.ObjectModel;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel
{
	/// <summary>
	/// Represents a navigation node without holding a database session.
	/// </summary>
	internal sealed class ProjectTreeNodeViewModel : DependencyViewModel
	{
		private string _title;
		private bool _isExpanded;
		private bool _isSelected;

		/// <summary>
		/// Gets the type of content represented by this node.
		/// </summary>
		public ProjectTreeNodeKind Kind { get; }

		/// <summary>
		/// Gets the persistent entity identifier.
		/// Grouping nodes do not reference an entity.
		/// </summary>
		public Guid? EntityId { get; }

		/// <summary>
		/// Gets or sets the text displayed in the tree.
		/// </summary>
		public string Title
		{
			get => _title;
			set
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					throw new ArgumentException(
						"A node title is required.",
						nameof(value));
				}

				SetProperty(ref _title, value);
			}
		}

		/// <summary>
		/// Gets the child nodes.
		/// </summary>
		public ObservableCollection<ProjectTreeNodeViewModel> Children
		{
			get;
		} = new();

		public bool IsExpanded
		{
			get => _isExpanded;
			set => SetProperty(ref _isExpanded, value);
		}

		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		public ProjectTreeNodeViewModel(ProjectTreeNodeKind kind,string title, Guid? entityId = null)
		{
			if (string.IsNullOrWhiteSpace(title))			
				throw new ArgumentException("A node title is required.", nameof(title));
			
			switch(kind)
			{
				case ProjectTreeNodeKind.Locations:
				case ProjectTreeNodeKind.Documents:
					if (entityId.HasValue)					
						throw new ArgumentException("A grouping node must not reference an entity.", nameof(entityId));
					
					break;

				case ProjectTreeNodeKind.Location:
				case ProjectTreeNodeKind.Document:
				case ProjectTreeNodeKind.Sheet:
					if (!entityId.HasValue || entityId.Value == Guid.Empty)					
						throw new ArgumentException("An entity node requires a non-empty identifier.", nameof(entityId));
					
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(kind));
			}

			Kind = kind;
			EntityId = entityId;
			_title = title;
		}
	}
}
