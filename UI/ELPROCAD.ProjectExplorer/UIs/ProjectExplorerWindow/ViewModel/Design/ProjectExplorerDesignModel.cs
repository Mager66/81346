using System;
using System.Collections.Generic;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel.Design
{
	/// <summary>
	/// Provides sample navigation and grid data without database access.
	/// </summary>
	internal sealed class ProjectExplorerDesignModel : ProjectExplorerViewModel
	{
		public static ProjectExplorerDesignModel Instance { get; set; } = new();

		public ProjectExplorerDesignModel() : this(new DesignData()) { }

		private ProjectExplorerDesignModel(DesignData data)
			: base(data.ReadRows)
		{
			foreach (var node in data.Roots)
				Nodes.Add(node);

			SelectedNode = data.InitialSelection;

			var sample = new ProjectExplorerRow(
				Guid.NewGuid(),
				"Q03",
				"Contactor",
				readSymbols: () => new[]
				{
					new ProjectExplorerSymbol(
						Guid.NewGuid(),
						@"Crap.dwg \ 002 — 355",
						() => (
						new[] { "UTTAG1: 1", "UTTAG2: 2" },
						new[] { @"Kretsschema.dwg\001 (9.C)" })),

					new ProjectExplorerSymbol(
						Guid.NewGuid(),
						@"Kretsschema.dwg \ 001 — 5BE",
						() => (
						new[] { "UTTAG1: 1", "UTTAG2: 2" },
						new[] { @"Crap.dwg\002 (2.D)" }))
				});

			Rows.Add(sample);
			SelectedRow = sample;
			DetailsOpen = true;
			SelectedSymbol = Symbols[0];
		}

		private sealed class DesignData
		{
			private readonly Dictionary<
				ProjectTreeNodeViewModel,
				IReadOnlyList<ProjectExplorerRow>> _rows = new();

			internal IReadOnlyList<ProjectTreeNodeViewModel> Roots { get; }

			internal ProjectTreeNodeViewModel InitialSelection { get; }

			internal DesignData()
			{
				var cabinetId = Guid.NewGuid();
				var contactorId = Guid.NewGuid();
				var terminalId = Guid.NewGuid();

				var locations = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Locations,
					"Locations")
				{
					IsExpanded = true
				};

				var cabinet = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Location,
					"AS1",
					Guid.NewGuid())
				{
					IsExpanded = true
				};

				var contactor = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Location,
					"K1",
					Guid.NewGuid());

				var terminal = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Location,
					"X1",
					Guid.NewGuid());

				locations.Children.Add(cabinet);
				cabinet.Children.Add(contactor);
				cabinet.Children.Add(terminal);

				// Descriptions are sample presentation text, not classification data.
				var cabinetRow = new ProjectExplorerRow(
					cabinetId,
					"AS1",
					"Control cabinet");

				var contactorRow = new ProjectExplorerRow(
					contactorId,
					"AS1.K1",
					"Contactor");

				var terminalRow = new ProjectExplorerRow(
					terminalId,
					"AS1.X1",
					"Terminal");

				_rows.Add(locations, new[]
				{
					cabinetRow,
					contactorRow,
					terminalRow
				});

				// Selecting a location shows objects directly assigned to that node.
				_rows.Add(cabinet, new[] { cabinetRow });
				_rows.Add(contactor, new[] { contactorRow });
				_rows.Add(terminal, new[] { terminalRow });

				var documents = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Documents,
					"Documents")
				{
					IsExpanded = true
				};

				var documentId = Guid.NewGuid();
				var firstSheetId = Guid.NewGuid();
				var secondSheetId = Guid.NewGuid();

				var document = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Document,
					"Control.dwg",
					documentId)
				{
					IsExpanded = true
				};

				var firstSheet = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Sheet,
					"001",
					firstSheetId);

				var secondSheet = new ProjectTreeNodeViewModel(
					ProjectTreeNodeKind.Sheet,
					"002",
					secondSheetId);

				documents.Children.Add(document);
				document.Children.Add(firstSheet);
				document.Children.Add(secondSheet);

				_rows.Add(documents, new[]
				{
					new ProjectExplorerRow(
						documentId,
						"Control.dwg",
						@"C:\Projects\Example\Control.dwg")
				});

				_rows.Add(document, new[]
				{
					new ProjectExplorerRow(
						firstSheetId,
						"001",
						"Control.dwg"),

					new ProjectExplorerRow(
						secondSheetId,
						"002",
						"Control.dwg")
				});

				_rows.Add(firstSheet, new[]
				{
					cabinetRow,
					contactorRow,
					terminalRow
				});

				// The same object can be presented on more than one sheet.
				_rows.Add(secondSheet, new[]
				{
					contactorRow,
					terminalRow
				});

				Roots = new[] { locations, documents };
				InitialSelection = locations;
			}

			internal IEnumerable<ProjectExplorerRow> ReadRows(
				ProjectTreeNodeViewModel node)
			{
				return _rows.TryGetValue(node, out var rows)
					? rows
					: Array.Empty<ProjectExplorerRow>();
			}
		}
	}
}