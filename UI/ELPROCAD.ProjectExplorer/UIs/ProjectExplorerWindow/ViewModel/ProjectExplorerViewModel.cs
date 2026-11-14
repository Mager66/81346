using ELPROCAD.ProjectExplorer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel
{
	/// <summary>
	/// Coordinates tree selection and grid content.
	/// Sorting, filtering and grouping belong to the view.
	/// </summary>
	internal class ProjectExplorerViewModel : DependencyViewModel
	{
		private readonly Func<ProjectTreeNodeViewModel, IEnumerable<ProjectExplorerRow>>? _readRows;

		private ProjectTreeNodeViewModel? _selectedNode;

		/// <summary>
		/// Gets the root nodes displayed in the tree.
		/// </summary>
		public ObservableCollection<ProjectTreeNodeViewModel> Nodes
		{
			get;
		} = new();

		/// <summary>
		/// Gets the source collection consumed by the view's CollectionViewSource.
		/// </summary>
		public ObservableCollection<ProjectExplorerRow> Rows
		{
			get;
		} = new();

		public string SelectedTitle => SelectedNode?.Title ?? "Select a node";

		public int RowCount => Rows.Count;

		public ProjectExplorerViewModel(Func<ProjectTreeNodeViewModel, IEnumerable<ProjectExplorerRow>>? readRows)
		{
			//if (DesignerProperties.GetIsInDesignMode(this))
			//	return;
			_readRows = readRows ?? throw new ArgumentNullException(nameof(readRows));
		}

		public ProjectTreeNodeViewModel? SelectedNode
		{
			get => _selectedNode;
			set
			{
				if(ReferenceEquals(_selectedNode, value)) 
					return;

				// Read first so a failed operation does not clear existing content.
				var rows = ReadRows(value);

				var previous = _selectedNode;
				_selectedNode = value;

				if (previous != null)
					previous.IsSelected = false;

				if (value != null)
					value.IsSelected = true;

				ReplaceRows(rows);

				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectedTitle));
			}
		}

		/// <summary>
		/// Reloads the selected node's content without changing the selection.
		/// </summary>
		public void RefreshSelectedNode() => ReplaceRows(ReadRows(SelectedNode));

		private IReadOnlyList<ProjectExplorerRow> ReadRows(ProjectTreeNodeViewModel? node)
		{
			if(DesignerProperties.GetIsInDesignMode(new DependencyObject()))
				return Array.Empty<ProjectExplorerRow>();

			if(node == null)
				return Array.Empty<ProjectExplorerRow>();

			var rows = _readRows(node)
				?? throw new InvalidOperationException("The row reader returned null.");

			return rows.ToList();
		}

		private void ReplaceRows(IReadOnlyList<ProjectExplorerRow> rows)
		{
			Rows.Clear();

			foreach( var row in rows) 
				Rows.Add(row);

			OnPropertyChanged(nameof(RowCount));
		}
	}
}
