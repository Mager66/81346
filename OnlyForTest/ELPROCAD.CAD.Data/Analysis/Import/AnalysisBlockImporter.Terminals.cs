using System;
using System.Collections.Generic;
using System.Linq;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{
		/// <summary>Reads attribute values, including terminals without a wire or geometric definition.</summary>
		private static IReadOnlyList<(string Tag, string Designation)> ReadTerminalAttributes(CadBlock block)
		{
			if (block.Attributes == null)
				throw new InvalidOperationException("Block attributes have not been loaded.");

			return TerminalImportRules.Read(block.Attributes.Select(x => (x.Tag, x.Value)),
				TerminalImportRules.IsReferenceContact(block.BlockName));
		}

		/// <summary>Rejects terminal conflicts before any location or object changes are made.</summary>
		private void ValidateTerminalPresentations(AcAp.Document doc, ImportContext context)
		{
			var inputs = context.Items.ToDictionary(x => x.Block.ObjectId.ToString());
			var presentations = new List<TerminalImportRules.Presentation>();
			var objectIds = new HashSet<Guid>();
			foreach (var item in context.Items)
			{
				var objectId = FindTerminalObject(item, context);
				if (objectId.HasValue) objectIds.Add(objectId.Value);
				presentations.Add(new TerminalImportRules.Presentation
				{
					Key = item.Block.ObjectId.ToString(),
					ObjectKey = objectId.HasValue ? objectId.Value.ToString("D") :
						"path:" + string.Join("", item.RequestedLevels.Select(x => x.Length + ":" + x.ToUpperInvariant())),
					RegisteredLinkId = item.RegisteredLink?.Id,
					Terminals = item.IsReferenceContact
					? Array.Empty<(string Tag, string Designation)>()
					: item.Terminals
				});
			}

			// Ignore obsolete physical mappings on reference symbols in this analysis.
			var referenceLinks = new HashSet<Guid>(
				context.Items
					.Where(x => x.IsReferenceContact && x.RegisteredLink != null)
					.Select(x => x.RegisteredLink!.Id));

			var claims = ReadTerminalOccupancy(objectIds)
				.Where(x => !referenceLinks.Contains(x.LinkId))
				.ToList();

			var storedClaims = claims.ToArray();
			// Bootstrap existing registered symbols whose terminal records have not yet been imported.
			// Only an unchanged, active presentation can establish an original claim.
			foreach (var presentation in presentations)
			{
				var item = inputs[presentation.Key];

				if (item.IsReferenceContact ||
					item.RegisteredLink == null ||
					item.RegisteredLink.IsDetached ||
					item.OriginalXDataId != item.RegisteredLink.EngineeringObjectId ||
					!SamePath(item.RequestedLevels, item.OriginalLevels)) 
					continue;

				foreach (var terminal in item.Terminals)
				{
					if (!storedClaims.Any(x => x.ObjectKey == presentation.ObjectKey &&
						string.Equals(x.Designation, terminal.Designation, StringComparison.OrdinalIgnoreCase)))
					{
						claims.Add(new TerminalImportRules.Occupancy
						{
							ObjectKey = presentation.ObjectKey,
							LinkId = item.RegisteredLink.Id,
							Designation = terminal.Designation
						});
					}
				}
			}

			var terminalErrors = TerminalImportRules.Check(presentations, claims);
			var invalidSymbols = new HashSet<string>(terminalErrors.Select(x => x.Key));
			foreach (var error in terminalErrors)
			{
				var item = inputs[error.Key];
				if (context.FailedBlocks.Contains(item.Block.ObjectId)) continue;
				var message = error.Message;
				try
				{
					if (error.ExistingLinkId.HasValue &&
						ClearConfirmedCopyLink(doc, context, item, error.ExistingLinkId.Value, invalidSymbols))
						message += " The copied symbol's object GUID was cleared. Correct its designation or terminal values and analyze again.";
				}
				catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
				{
					message += " The object GUID was not cleared: " + ex.Message;
				}
				context.Fail(item.Block.ObjectId, message);
			}
		}

		private static Guid? FindTerminalObject(ImportBlock item, ImportContext context)
		{
			if (item.LinkedObjectId.HasValue) return item.LinkedObjectId;
			foreach (var location in context.OriginalLocations.Values)
				if (SamePath(item.RequestedLevels, context.OriginalPaths[location.Id]))
					return location.RepresentedByObjectId;
			return null;
		}

		private List<TerminalImportRules.Occupancy> ReadTerminalOccupancy(IEnumerable<Guid> objectIds)
		{
			var claims = new List<TerminalImportRules.Occupancy>();
			foreach (var objectId in objectIds)
			{
				var terminals = _mdbx.CadSymbolLink.GetObjectTerminals(objectId).ToDictionary(x => x.Id);
				foreach (var link in _mdbx.CadSymbolLink.GetByObjectId(objectId).Where(x => !x.IsDetached))
					foreach (var slot in _mdbx.CadSymbolLink.GetTerminals(link.Id))
					{
						if (!terminals.TryGetValue(slot.EngineeringObjectTerminalId, out var terminal))
							throw new InvalidOperationException($"Symbol link '{link.Id}' references a missing object terminal.");
						claims.Add(new TerminalImportRules.Occupancy
						{
							ObjectKey = objectId.ToString("D"),
							LinkId = link.Id,
							Designation = terminal.Designation
						});
					}
			}
			return claims;
		}

		/// <summary>Clears only a new copy with a verified original in this analysis. Ambiguous identities remain untouched.</summary>
		private bool ClearConfirmedCopyLink(AcAp.Document doc, ImportContext context,
			ImportBlock copy, Guid originalLinkId, ISet<string> invalidSymbols)
		{
			if (copy.RegisteredLink != null || !copy.OriginalXDataId.HasValue) return false;
			var original = context.Items.SingleOrDefault(x => x.RegisteredLink?.Id == originalLinkId);
			if (original == null || original.RegisteredLink!.IsDetached ||
				invalidSymbols.Contains(original.Block.ObjectId.ToString()) ||
				original.RegisteredLink.EngineeringObjectId != copy.OriginalXDataId ||
				original.OriginalXDataId != copy.OriginalXDataId ||
				original.Block.ObjectId == copy.Block.ObjectId ||
				!SamePath(original.OriginalLevels, original.RequestedLevels) ||
				!SamePath(original.RequestedLevels, copy.RequestedLevels) ||
				!original.Terminals.Any(x => copy.Terminals.Any(y =>
					string.Equals(x.Designation, y.Designation, StringComparison.OrdinalIgnoreCase)))) return false;

			// Recheck the live original immediately before clearing the copy's inherited identifier.
			if (EngineeringObjectLink.Read(doc.Database, original.Block.ObjectId) != copy.OriginalXDataId)
				return false;
			using (var transaction = doc.Database.TransactionManager.StartOpenCloseTransaction())
			{
				foreach (var item in new[] { original, copy })
				{
					var attribute = transaction.GetObject(item.Attribute.ObjectId, AcDb.OpenMode.ForRead)
						as AcDb.AttributeReference;

					if (!MatchesCapturedLocation(transaction, item)) 
						return false;
				}
			}
			EngineeringObjectLink.ClearExpected(doc.Database, copy.Block.ObjectId, copy.OriginalXDataId.Value);
			return true;
		}

		/// <summary>Saves a new link and its terminal mappings in one database commit.</summary>
		private void SaveSymbolTerminals(ImportContext context, ImportBlock item, Guid objectId, SheetAnalysisData analysis)
		{
			var point = item.Block.Position;
			var grid = analysis.GetGridCoordinate(point);

			(double X, double Y, double Z, string? Column, string? Row) position = (point.X, point.Y, point.Z, grid?.X, grid?.Y);

			var existing = _mdbx.CadSymbolLink.GetByBlock(context.Document.Id, item.Block.ObjectId.Handle.Value);

			if (existing != null && (existing.EngineeringObjectId != objectId || existing.SheetId != context.Sheet.Id))
				throw new InvalidOperationException("The symbol link belongs to another object or sheet.");

			if (item.IsReferenceContact)
			{
				var placement = item.Block.GetAttribute(_extractor.PlacementAttributeTag)?.Value ?? "";
				var referenceText = item.Block.GetAttribute("XREF")?.Value ?? "";

				var result = existing == null
					? _mdbx.CadSymbolLink.CreateReferenceContact(
						context.Document.Id,
						context.Sheet.Id,
						item.Block.ObjectId.Handle.Value,
						objectId,
						item.Block.BlockName,
						item.Attribute.Value,
						placement,
						referenceText,
						item.Terminals,
						position)
					: _mdbx.CadSymbolLink.ReplaceReferenceContact(
						existing,
						item.Block.BlockName,
						item.Attribute.Value,
						placement,
						referenceText,
						item.Terminals,
						position);

				Require(result);
				return;
			}
			if (existing == null)
			{
				Require(_mdbx.CadSymbolLink.CreateWithTerminals(context.Document.Id, context.Sheet.Id,
					item.Block.ObjectId.Handle.Value, objectId, item.Terminals, position));
				return;
			}

			if (existing.IsDetached)
			{
				var validation = _mdbx.CadSymbolLink.Reattach(existing);
				if (!validation.IsValid)
					throw new InvalidOperationException(string.Join(Environment.NewLine,
						validation.Errors.Select(x => x.ErrorMessage).Distinct()));
				existing = _mdbx.CadSymbolLink.GetByBlock(context.Document.Id, item.Block.ObjectId.Handle.Value)
					?? throw new InvalidOperationException("The reattached symbol link no longer exists.");
			}
			Require(_mdbx.CadSymbolLink.ReplaceTerminals(existing, item.Terminals, position));
		}
	}
}
