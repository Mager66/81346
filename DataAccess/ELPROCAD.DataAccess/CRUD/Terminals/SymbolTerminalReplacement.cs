using DevExpress.Xpo;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Terminals;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.CRUD.Terminals
{
	/// <summary>Prepares and stages a complete replacement; the owning Update operation commits.</summary>
	internal sealed class SymbolTerminalReplacement
	{
		private readonly List<EngineeringObjectTerminal> _newTerminals = new();
		private readonly List<CadSymbolTerminal> _proposed = new();
		private List<CadSymbolTerminal> _previous = new();

		internal ValidationResult Prepare(UnitOfWork uow, CadSymbolLink symbol,	IReadOnlyList<(string Tag, string Designation)> values)
		{
			if (symbol.IsDetached)
				return ValidationResults.Failure(nameof(symbol.IsDetached),
					"Restore the symbol link before replacing its terminals.");

			// Read this record BEFORE occupancy queries. Every claim and reattachment
			// updates its version, preventing concurrent claims from both committing.
			var owner = uow.GetObjectByKey<EngineeringObject>(symbol.EngineeringObjectId);
			if (owner == null || owner.IsMarkedForDeletion)
				return ValidationResults.Failure(nameof(symbol.EngineeringObjectId),
					"The engineering object is missing or marked for deletion.");
			DependencyVersions.Touch(uow, new VersionedEntity[] { owner });

			var terminals = new XPQuery<EngineeringObjectTerminal>(uow)
			  .Where(x => x.EngineeringObjectId == owner.Id).ToList();
			var byDesignation = new Dictionary<string, EngineeringObjectTerminal>(StringComparer.OrdinalIgnoreCase);
			foreach (var terminal in terminals)
			{
				if (byDesignation.ContainsKey(terminal.Designation.Trim()))
					return ValidationResults.Failure(nameof(terminal.Designation),
						"The object contains duplicate terminal designations. Correct the stored data first.");
				byDesignation.Add(terminal.Designation.Trim(), terminal);
			}

			foreach (var value in values)
			{
				var designation = value.Designation.Trim();
				if (!byDesignation.TryGetValue(designation, out var terminal))
				{
					terminal = new EngineeringObjectTerminal
					{
						EngineeringObjectId = owner.Id,
						Designation = designation
					};
					byDesignation.Add(designation, terminal);
					terminals.Add(terminal);
					_newTerminals.Add(terminal);
				}
				_proposed.Add(new CadSymbolTerminal
				{
					CadSymbolLinkId = symbol.Id,
					Tag = value.Tag.Trim(),
					EngineeringObjectTerminalId = terminal.Id
				});
			}

			var links = new XPQuery<CadSymbolLink>(uow)
				.Where(x => x.EngineeringObjectId == owner.Id).ToList();
			var linkIds = links.Select(x => x.Id).ToArray();
			var stored = new XPQuery<CadSymbolTerminal>(uow)
				.Where(x => Enumerable.Contains(linkIds, x.CadSymbolLinkId)).ToList();
			_previous = stored.Where(x => x.CadSymbolLinkId == symbol.Id).ToList();

			return CadSymbolTerminalsValidation.Validate(symbol, _proposed, terminals, links, stored);
		}


		internal void Stage(UnitOfWork uow, CadSymbolLink symbol)
		{
			foreach (var terminal in _newTerminals) uow.Save(terminal);

			// Match by slot so changing/swapping terminal values never swaps unique tag keys.
			var remaining = new List<CadSymbolTerminal>(_previous);
			foreach (var proposed in _proposed)
			{
				var stored = remaining.FirstOrDefault(x =>
					string.Equals(x.Tag.Trim(), proposed.Tag, StringComparison.OrdinalIgnoreCase));
				if (stored == null)
					uow.Save(proposed);
				else
				{
					remaining.Remove(stored);
					stored.EngineeringObjectTerminalId = proposed.EngineeringObjectTerminalId;
					uow.Save(stored);
				}
			}
			foreach (var removed in remaining) uow.Delete(removed);

			// Also advance the symbol version when the mapping list becomes empty.
			DependencyVersions.Touch(uow, new VersionedEntity[] { symbol });
		}

		internal static ValidationResult ValidateInput(IReadOnlyList<(string Tag, string Designation)> values)
		{
			var errors = new List<ValidationFailure>();
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var designations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var value in values)
			{
				var tag = (value.Tag ?? string.Empty).Trim();
				var designation = (value.Designation ?? string.Empty).Trim();
				if (tag.Length == 0 || tag.Length > 255)
					errors.Add(new ValidationFailure("Tag", "A terminal tag containing 1 to 255 characters is required."));
				else if (!tags.Add(tag))
					errors.Add(new ValidationFailure("Tag", $"Terminal tag '{tag}' occurs more than once on the symbol."));
				if (designation.Length == 0 || designation.Length > 255)
					errors.Add(new ValidationFailure("Designation", "A terminal designation containing 1 to 255 characters is required."));
				else if (!designations.Add(designation))
					errors.Add(new ValidationFailure("Designation", $"Terminal '{designation}' is represented more than once on the symbol."));
			}
			return new ValidationResult(errors);
		}

		/// <summary>Checks retained mappings as ACTIVE before reattaching a symbol.</summary>
		internal static ValidationResult ValidateReattachment(UnitOfWork uow, CadSymbolLink symbol)
		{
			var owner = uow.GetObjectByKey<EngineeringObject>(symbol.EngineeringObjectId);
			if (owner == null || owner.IsMarkedForDeletion)
				return ValidationResults.Failure(nameof(symbol.EngineeringObjectId),
					"The engineering object is missing or marked for deletion.");
			DependencyVersions.Touch(uow, new VersionedEntity[] { owner });
			var proposed = new XPQuery<CadSymbolTerminal>(uow)
				.Where(x => x.CadSymbolLinkId == symbol.Id).ToList();
			var terminalIds = proposed.Select(x => x.EngineeringObjectTerminalId).ToArray();
			var terminals = new XPQuery<EngineeringObjectTerminal>(uow)
				.Where(x => Enumerable.Contains(terminalIds, x.Id)).ToList();
			// Query by terminal IDs to include even malformed cross-object claims.
			var stored = new XPQuery<CadSymbolTerminal>(uow)
				.Where(x => Enumerable.Contains(terminalIds, x.EngineeringObjectTerminalId)).ToList();
			var linkIds = stored.Select(x => x.CadSymbolLinkId).ToArray();
			var links = new XPQuery<CadSymbolLink>(uow)
				.Where(x => Enumerable.Contains(linkIds, x.Id)).ToList();
			var active = new CadSymbolLink { Id = symbol.Id, EngineeringObjectId = symbol.EngineeringObjectId };
			return CadSymbolTerminalsValidation.Validate(active, proposed, terminals, links, stored);
		}
	}
}
