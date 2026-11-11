using System.Text.Json;

namespace ELPROCAD.CAD.Library.BlockDefinitions
{
	public static class BlockDefinitionParser
	{
		private static readonly JsonSerializerOptions Options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true
		};

		public static BlockDefinitionDocument Load(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				throw new ArgumentException(
					"En filsökväg måste anges.", nameof(filePath));

			return Parse(File.ReadAllText(filePath));
		}

		public static BlockDefinitionDocument Parse(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
				throw new ArgumentException(
					"JSON-innehållet får inte vara tomt.", nameof(json));

			using var source = JsonDocument.Parse(json);
			ValidateStructure(source.RootElement);

			var document = JsonSerializer.Deserialize<BlockDefinitionDocument>(
				json, Options)
				?? throw new JsonException("Dokumentet får inte vara null.");

			if (document.SchemaVersion != 1)
				throw new NotSupportedException(
					$"Schemaversion {document.SchemaVersion} stöds inte.");

			foreach (var block in document.Blocks)
			{
				if (block.Prefix.Length != 4 ||
			   block.TerminalVariant.Length != 1 ||
			   block.DeviceType.Length != 2 ||
			   block.DeviceVariant.Length != 1)
				{
					throw new JsonException(
						"Blocknamnets delar måste ha längderna 4, 1, 2 och 1.");
				}

				var terminalNumbers = new HashSet<int>();

				foreach (var terminal in block.Terminals)
				{
					if (terminal.Number < 1 ||
				  !terminalNumbers.Add(terminal.Number))
					{
						throw new JsonException(
							"Uttagsnummer måste vara positiva och unika inom blocket.");
					}

					if (terminal.Direction < 0 || terminal.Direction > 3)
						throw new JsonException(
							$"Ogiltig uttagsriktning: {terminal.Direction}.");

					if (double.IsNaN(terminal.Offset.X) ||
						double.IsInfinity(terminal.Offset.X) ||
						double.IsNaN(terminal.Offset.Y) ||
						double.IsInfinity(terminal.Offset.Y))
					{
						throw new JsonException(
							"Uttagskoordinater måste vara ändliga tal.");
					}
				}
			}

			return document;
		}

		private static void ValidateStructure(JsonElement root)
		{
			Require(root, "schemaVersion", JsonValueKind.Number);
			var blocks = Require(root, "blocks", JsonValueKind.Array);

			foreach (var block in blocks.EnumerateArray())
			{
				Require(block, "prefix", JsonValueKind.String);
				Require(block, "terminalVariant", JsonValueKind.String);
				Require(block, "deviceType", JsonValueKind.String);
				Require(block, "deviceVariant", JsonValueKind.String);
				Require(block, "objectType", JsonValueKind.Number);

				var terminals = Require(block, "terminals", JsonValueKind.Array);

				foreach (var terminal in terminals.EnumerateArray())
				{
					Require(terminal, "number", JsonValueKind.Number);
					Require(terminal, "direction", JsonValueKind.Number);

					var offset = Require(terminal, "offset", JsonValueKind.Object);

					Require(offset, "x", JsonValueKind.Number);
					Require(offset, "y", JsonValueKind.Number);
				}
			}
		}

		private static JsonElement Require(JsonElement parent, string propertyName, JsonValueKind expectedKind)
		{
			if (parent.ValueKind != JsonValueKind.Object ||
				!parent.TryGetProperty(propertyName, out var value) ||
				value.ValueKind != expectedKind)
			{
				throw new JsonException(
					$"Fältet '{propertyName}' måste finnas och ha typen {expectedKind}.");
			}

			return value;
		}
	}
}
