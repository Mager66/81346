namespace ELPROCAD.CAD.Library.BlockDefinitions
{
	public sealed class TerminalDefinition
	{
		public int Number { get; set; }

		public int Direction { get; set; }

		public TerminalOffset Offset { get; set; } = new();
	}
}
