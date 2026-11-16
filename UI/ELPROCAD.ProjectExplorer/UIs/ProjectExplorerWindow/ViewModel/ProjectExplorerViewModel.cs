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
		public ObservableCollection<ProjectExplorerSymbol> Symbols { get; } = new();
		public ObservableCollection<string> TerminalDetails { get; } = new();
		public ObservableCollection<string> ReferenceDetails { get; } = new();

		public string SelectedTitle => SelectedNode?.Title ?? "Select a node";

		public int RowCount => Rows.Count;

		public string DetailError
		{
			get => _detailError;
			private set => SetProperty(ref _detailError, value);
		}
		private string _detailError = "";

		public bool DetailsOpen
		{
			get => _detailsOpen;
			set
			{
				if (!SetProperty(ref _detailsOpen, value))
					return;

				if (value)
					LoadSymbols();
			}
		}
		private bool _detailsOpen;

		public ProjectExplorerRow? SelectedRow
		{
			get => _selectedRow;
			set
			{
				if (!SetProperty(ref _selectedRow, value))
					return;

				ClearDetails();

				if (value?.HasSymbolDetails != true)
					DetailsOpen = false;
				else if (DetailsOpen)
					LoadSymbols();
			}
		}
		private ProjectExplorerRow? _selectedRow;

		public ProjectExplorerSymbol? SelectedSymbol
		{
			get => _selectedSymbol;
			set
			{
				if (!SetProperty(ref _selectedSymbol, value))
					return;

				TerminalDetails.Clear();
				ReferenceDetails.Clear();
				DetailError = "";

				if (value == null)
					return;

				try
				{
					var details = value.ReadDetails();

					foreach (var terminal in details.Terminals)
						TerminalDetails.Add(terminal);

					foreach (var reference in details.References)
						ReferenceDetails.Add(reference);
				}
				catch (Exception exception)
				{
					DetailError =
						$"Could not load symbol details: {exception.Message}";
				}
			}
		}
		private ProjectExplorerSymbol? _selectedSymbol;


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
		private ProjectTreeNodeViewModel? _selectedNode;

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

			var rows = _readRows?.Invoke(node)
				?? throw new InvalidOperationException("The row reader returned null.");

			return rows.ToList();
		}

		private void ReplaceRows(IReadOnlyList<ProjectExplorerRow> rows)
		{
			SelectedRow = null;
			DetailsOpen = false;
			Rows.Clear();

			foreach( var row in rows) 
				Rows.Add(row);

			OnPropertyChanged(nameof(RowCount));
		}

		private void ClearDetails()
		{
			SelectedSymbol = null;
			Symbols.Clear();
			TerminalDetails.Clear();
			ReferenceDetails.Clear();
			DetailError = "";
		}

		private void LoadSymbols()
		{
			ClearDetails();

			try
			{
				var symbols = SelectedRow?.ReadSymbols?.Invoke();

				if (symbols == null)
					return;

				foreach (var symbol in symbols)
					Symbols.Add(symbol);

				// Multiple presentations require an explicit selection.
				if (Symbols.Count == 1)
					SelectedSymbol = Symbols[0];
			}
			catch (Exception exception)
			{
				DetailError =
					$"Could not load CAD symbols: {exception.Message}";
			}
		}

	}
}
