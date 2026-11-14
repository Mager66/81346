using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Validation.MDBX.Terminals
{
	/// <summary>
	/// Validates a complete replacement of one symbol's terminal mappings.
	/// The caller must supply all relevant persisted mappings and links, including detached links.
	/// This check does not persist data or provide transaction/concurrency protection.
	/// </summary>
	internal static class CadSymbolTerminalsValidation
	{
		internal static ValidationResult Validate(
		   ICadSymbolLink symbol,
		   IReadOnlyList<ICadSymbolTerminal> proposed,
		   IReadOnlyList<IEngineeringObjectTerminal> terminals,
		   IReadOnlyList<ICadSymbolLink> links,
		   IReadOnlyList<ICadSymbolTerminal> stored)
		{
			if (symbol == null) throw new ArgumentNullException(nameof(symbol));
			if (proposed == null) throw new ArgumentNullException(nameof(proposed));
			if (terminals == null) throw new ArgumentNullException(nameof(terminals));
			if (links == null) throw new ArgumentNullException(nameof(links));
			if (stored == null) throw new ArgumentNullException(nameof(stored));

			var errors = new List<ValidationFailure>();
			void Fail(string property, string message) => errors.Add(new ValidationFailure(property, message));
			var byTerminal = terminals.ToDictionary(x => x.Id);
			var byLink = links.ToDictionary(x => x.Id);
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var designations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var mapping in proposed)
			{
				if (mapping.CadSymbolLinkId != symbol.Id)
					Fail(nameof(mapping.CadSymbolLinkId), "The terminal mapping belongs to another symbol.");
				var tag = (mapping.Tag ?? string.Empty).Trim();
				if (tag.Length == 0 || tag.Length > 255)
					Fail(nameof(mapping.Tag), "A terminal tag containing 1 to 255 characters is required.");
				else if (!tags.Add(tag))
					Fail(nameof(mapping.Tag), $"Terminal tag '{tag}' occurs more than once on the symbol.");

				if (!byTerminal.TryGetValue(mapping.EngineeringObjectTerminalId, out var terminal))
				{
					Fail(nameof(mapping.EngineeringObjectTerminalId), "The engineering object terminal does not exist.");
					continue;
				}
				if (terminal.EngineeringObjectId != symbol.EngineeringObjectId)
				{
					Fail(nameof(mapping.EngineeringObjectTerminalId), "The terminal belongs to another engineering object.");
					continue;
				}
				var designation = (terminal.Designation ?? string.Empty).Trim();
				if (designation.Length == 0 || designation.Length > 255)
				{
					Fail(nameof(terminal.Designation), "A terminal designation containing 1 to 255 characters is required.");
					continue;
				}
				if (!designations.Add(designation))
					Fail(nameof(mapping.EngineeringObjectTerminalId), $"Terminal '{designation}' is represented more than once on the symbol.");

				if (symbol.IsDetached) continue;
				foreach (var existing in stored.Where(x => x.CadSymbolLinkId != symbol.Id &&
					x.EngineeringObjectTerminalId == terminal.Id))
				{
					if (!byLink.TryGetValue(existing.CadSymbolLinkId, out var other))
					{
						Fail(nameof(mapping.CadSymbolLinkId), "A stored terminal mapping references a missing symbol link.");
						continue;
					}
					if (!other.IsDetached)
					{
						Fail(nameof(mapping.EngineeringObjectTerminalId),
							$"Terminal '{designation}' is already represented by symbol link '{other.Id}'.");
						break;
					}
				}
			}
			return new ValidationResult(errors);
		}
	}
}
