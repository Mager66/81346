namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel
{
	/// <summary>
	/// Identifies one CAD presentation and loads its details on selection.
	/// </summary>
	internal sealed class ProjectExplorerSymbol
	{
		public Guid LinkId { get; }
		public string Caption { get; }

		internal Func<(
			IReadOnlyList<string> Terminals,
			IReadOnlyList<string> References)> ReadDetails	{ get; }

		internal ProjectExplorerSymbol(
			Guid linkId,
			string caption,
			Func<(
				IReadOnlyList<string> Terminals,
				IReadOnlyList<string> References)> readDetails)
		{
			LinkId = linkId;
			Caption = caption;
			ReadDetails = readDetails;
		}
	}
}
