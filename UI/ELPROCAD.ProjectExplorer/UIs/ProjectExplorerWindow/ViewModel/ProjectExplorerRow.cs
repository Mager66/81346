using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel
{
	/// <summary>
	/// Represents a read-only grid row without a database session.
	/// Display text does not change the underlying entity model.
	/// </summary>
	internal sealed class ProjectExplorerRow
	{
		public Guid EntityId { get; }

		public string Title { get; }

		public string Description { get; }

		public ProjectExplorerRow(Guid entityId, string title, string description = "", Func<IReadOnlyList<ProjectExplorerSymbol>>? readSymbols = null)
		{
			if (entityId == Guid.Empty)
			{
				throw new ArgumentException(
					"An entity identifier is required.",
					nameof(entityId));
			}

			EntityId = entityId;
			Title = title ?? throw new ArgumentNullException(nameof(title));
			Description = description ?? throw new ArgumentNullException(nameof(description));
			ReadSymbols = readSymbols;
		}

		internal Func<IReadOnlyList<ProjectExplorerSymbol>>? ReadSymbols { get; }

		public bool HasSymbolDetails => ReadSymbols != null;
	}
}
