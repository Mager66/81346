namespace ELPROCAD.CAD.Library.BlockDefinitions
{
	public sealed class BlockDefinitionLookup
	{
		private readonly Dictionary<string, BlockDefinition> definitions;

		public BlockDefinitionLookup(BlockDefinitionDocument document)
		{
			if (document == null)
				throw new ArgumentNullException(nameof(document));

			definitions = new Dictionary<string, BlockDefinition>(
				StringComparer.OrdinalIgnoreCase);

			foreach (var definition in document.Blocks)
			{
				var blockName = string.Concat(
					definition.Prefix,
					definition.TerminalVariant,
					definition.DeviceType,
					definition.DeviceVariant).TrimEnd(' ');

				if (!definitions.ContainsKey(blockName))
					definitions.Add(blockName, definition);
			}
		}

		public BlockDefinition? Find(string blockName)
		{
			if (string.IsNullOrWhiteSpace(blockName))
				throw new ArgumentException(
					"Ett blocknamn måste anges.",
					nameof(blockName));

			return definitions.TryGetValue(
				blockName.TrimEnd(' '),
				out var definition)
					? definition
					: null;
		}
	}
}
