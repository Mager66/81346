using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	/// <summary>CAD-independent extraction and preflight rules. Never mutates drawings or databases.</summary>
	internal static class TerminalImportRules
	{
		internal static IReadOnlyList<(string Tag, string Designation)> Read(IEnumerable<(string Tag, string Value)> attributes, bool isReference = false)
		{
			if (attributes == null) throw new ArgumentNullException(nameof(attributes));
			var result = new List<(string Tag, string Designation)>();
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var attribute in attributes)
			{
				var tag = (attribute.Tag ?? "").Trim();
				// TODO: Replace legacy UTTAG slot recognition with configured attribute mapping.
				if (!tag.StartsWith("UTTAG", StringComparison.OrdinalIgnoreCase) ||
					!int.TryParse(tag.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1)
					continue;

				if (!tags.Add(tag))
					throw new InvalidOperationException($"Terminal tag '{tag}' occurs more than once on the symbol.");

				// Reference attributes retain their displayed values.
				// They do not participate in physical terminal occupancy.
				var value = isReference
					? attribute.Value ?? ""
					: (attribute.Value ?? "").Trim();

				if (value.Length > 255 || (!isReference && value.Length == 0))
				{
					throw new InvalidOperationException(
						$"Terminal tag '{tag}' contains an invalid value.");
				}

				if (!isReference && !values.Add(value))
				{
					throw new InvalidOperationException(
						$"Terminal '{value}' is represented more than once on the symbol.");
				}

				result.Add((tag, value));
			}
			return result.AsReadOnly();
		}

		internal sealed class Presentation
		{
			internal string Key = "";
			internal string ObjectKey = "";
			internal Guid? RegisteredLinkId;
			internal IReadOnlyList<(string Tag, string Designation)> Terminals =
				Array.Empty<(string Tag, string Designation)>();
		}

		internal sealed class Occupancy
		{
			internal string ObjectKey = "";
			internal Guid LinkId;
			internal string Designation = "";
		}

		/// <summary>Existing claims take precedence. Without an existing claim, all competing inputs fail.</summary>
		internal static IReadOnlyList<(string Key, Guid? ExistingLinkId, string Message)> Check(
			IReadOnlyList<Presentation> symbols, IReadOnlyList<Occupancy> stored)
		{
			var errors = new List<(string Key, Guid? ExistingLinkId, string Message)>();
			foreach (var symbol in symbols)
				foreach (var terminal in symbol.Terminals)
				{
					var claims = stored.Where(x => x.ObjectKey == symbol.ObjectKey &&
						string.Equals(x.Designation, terminal.Designation, StringComparison.OrdinalIgnoreCase)).ToArray();
					var other = claims.FirstOrDefault(x => x.LinkId != symbol.RegisteredLinkId);
					if (other != null)
					{
						errors.Add((symbol.Key, other.LinkId,
							$"Terminal '{terminal.Designation}' is already represented by symbol link '{other.LinkId}'."));
					}
					else if (claims.Length == 0 && symbols.Any(x => x.Key != symbol.Key &&
						x.ObjectKey == symbol.ObjectKey && x.Terminals.Any(t =>
							string.Equals(t.Designation, terminal.Designation, StringComparison.OrdinalIgnoreCase))))
					{
						errors.Add((symbol.Key, null,
							$"Terminal '{terminal.Designation}' is repeated on multiple analyzed symbols. No registered claim identifies an original."));
					}
				}
			return errors.AsReadOnly();
		}

		internal static bool IsReferenceContact(string blockName) =>
			blockName.Length > 4 &&
			(blockName[4] == 'I' || blockName[4] == 'i');
	}
}

