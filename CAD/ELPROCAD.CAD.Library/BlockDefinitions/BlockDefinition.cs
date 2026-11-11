namespace ELPROCAD.CAD.Library.BlockDefinitions
{
	public sealed class BlockDefinition
	{
		public string Prefix { get; set; } = string.Empty;

		public string TerminalVariant { get; set; } = string.Empty;

		public string DeviceType { get; set; } = string.Empty;

		public string DeviceVariant { get; set; } = string.Empty;

		public int ObjectType { get; set; }

		public List<TerminalDefinition> Terminals { get; set; } = new();
	}
}
