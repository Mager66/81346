using ELPROCAD.CAD.Library.Analysis.Sheets;
using System.Globalization;

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{
		/// <summary>
		/// Stores the local nets only after all symbol imports have succeeded.
		/// Geometric junctions already determine membership in result.Networks.
		/// This does not create physical conductors or infer cross-sheet continuity.
		/// </summary>
		private void StoreConnections(SheetAnalysisData analysis, ImportContext context)
		{
			if (context.Errors.Count != 0)
			{
				context.Errors.Add((analysis.TitleBlock.ObjectId,
					"Connection storage was skipped because symbol import failed. Previous connection data was retained."));
				return;
			}

			try
			{
				var sheet = _mdbx.Sheet.GetById(context.Sheet.Id)
					?? throw new InvalidOperationException("The sheet no longer exists.");
				var inputs = new List<(Guid SymbolId, int SymbolVersion, string Tag,
					Guid TerminalId, int? NetworkNumber)>();
				var represented = new HashSet<(string Block, int Slot)>();
				var membership = new Dictionary<(string Block, int Slot), int>();

				foreach (var network in analysis.Networks)
					foreach (var terminal in network.Terminals)
					{
						var key = (terminal.BlockId.ToString(), terminal.Number);
						if (membership.TryGetValue(key, out var previous) && previous != network.Number)
							throw new InvalidOperationException(
								$"Block {terminal.BlockId.Handle}, UTTAG{terminal.Number}: the same slot belongs to multiple local networks.");
						membership[key] = network.Number;
					}

				foreach (var item in context.Items.Where(x => !x.IsReferenceContact))
				{
					// TODO: Add explicit adapters for terminal sides, potentials, signal references and cable ends.
					// Do not reinterpret their legacy object-type codes as apparatus terminals.
					if (item.Terminals.Count > 0 && item.Block.ObjectType.HasValue && item.Block.ObjectType.Value != 0)
						throw new InvalidOperationException(
							$"Block {item.Block.ObjectId.Handle}, BET1 '{item.Attribute.Value}': connection import for object type {item.Block.ObjectType} is not implemented.");

					var symbol = _mdbx.CadSymbolLink.GetByBlock(context.Document.Id, item.Block.ObjectId.Handle.Value);
					if (symbol == null || symbol.IsDetached || symbol.SheetId != sheet.Id || !symbol.LoadedVersion.HasValue)
						throw new InvalidOperationException($"Block {item.Block.ObjectId.Handle}: the active sheet symbol link is missing.");

					var slots = _mdbx.CadSymbolLink.GetTerminals(symbol.Id);
					var objectTerminals = _mdbx.CadSymbolLink.GetObjectTerminals(symbol.EngineeringObjectId)
						.ToDictionary(x => x.Id);
					if (slots.Count != item.Terminals.Count)
						throw new InvalidOperationException($"Block {item.Block.ObjectId.Handle}: terminal mappings changed during analysis.");
					foreach (var slot in slots)
					{
						var expected = item.Terminals.SingleOrDefault(x =>
							string.Equals(x.Tag, slot.Tag, StringComparison.OrdinalIgnoreCase));
						if (expected.Tag == null || !objectTerminals.TryGetValue(slot.EngineeringObjectTerminalId, out var storedTerminal) ||
							!string.Equals(expected.Designation, storedTerminal.Designation, StringComparison.OrdinalIgnoreCase))
							throw new InvalidOperationException($"Block {item.Block.ObjectId.Handle}: terminal values changed during analysis.");
						// TODO: Replace the legacy UTTAG prefix when symbol definitions are migrated.
						if (!slot.Tag.StartsWith("UTTAG", StringComparison.OrdinalIgnoreCase) ||
							!int.TryParse(slot.Tag.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
							throw new InvalidOperationException($"Block {item.Block.ObjectId.Handle}: unsupported terminal slot '{slot.Tag}'.");
						var key = (item.Block.ObjectId.ToString(), number);
						if (!represented.Add(key))
							throw new InvalidOperationException($"Block {item.Block.ObjectId.Handle}: duplicate terminal slot '{slot.Tag}'.");
						inputs.Add((symbol.Id, symbol.LoadedVersion.Value, slot.Tag, slot.EngineeringObjectTerminalId,
							membership.TryGetValue(key, out var net) ? net : (int?)null));
					}
				}

				var missing = membership.Keys.Where(x => !represented.Contains(x)).ToList();
				// Avkommentera efter test
				//if (missing.Count > 0)
				//	throw new InvalidOperationException("Some analyzed network terminals have no imported physical symbol slot. Connection storage was cancelled.");

				#region För test
				if (missing.Count > 0)
				{
					var details = missing.Select(key =>
					{
						var block = analysis.Blocks.FirstOrDefault(
							x => x.ObjectId.ToString() == key.Block);

						var item = context.Items.FirstOrDefault(
							x => x.Block.ObjectId.ToString() == key.Block);

						var handle = block == null
							? key.Block
							: block.ObjectId.Handle.ToString();

						var tags = block?.Attributes == null
							? "(not loaded)"
							: string.Join(", ", block.Attributes
								.Where(x => x.Tag.StartsWith(
									"UTTAG",
									StringComparison.OrdinalIgnoreCase))
								.Select(x => $"{x.Tag}='{x.Value}'"));

						return
							$"Block {handle}, name '{block?.BlockName}', " +
							$"BET1 '{block?.GetAttribute(LocationAttributeTag)?.Value}', " +
							$"network {membership[key]}, slot UTTAG{key.Slot}: " +
							$"import item present={item != null}, " +
							$"reference contact={item?.IsReferenceContact}. " +
							$"Terminal attributes: [{tags}]";
					});

					throw new InvalidOperationException(
						"Network terminals could not be matched to imported symbol slots. " +
						string.Join(" | ", details));
				}
				#endregion

				var result = _mdbx.ConnectionAnalysis.Replace(sheet,
					analysis.Networks.Select(x => x.Number).ToArray(), inputs);
				if (!result.IsValid)
					throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.ErrorMessage)));
			}
			catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
			{
				context.Errors.Add((analysis.TitleBlock.ObjectId,
					"Connection data was not saved: " + ex.Message));
			}
		}
	}
}
