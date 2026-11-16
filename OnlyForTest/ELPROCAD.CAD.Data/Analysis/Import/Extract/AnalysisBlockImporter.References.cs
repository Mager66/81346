using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.Library.CadEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP || PROGECAD_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
    public sealed partial class AnalysisBlockImporter
    {
		/// <summary>Updates XREF on registered reference contacts on the selected sheet.</summary>
		public (int Updated, int Unchanged, IReadOnlyList<string> Errors) UpdateReferenceContacts(AcAp.Document doc, AcDb.ObjectId titleBlockId)
		{
			if (doc == null)
				throw new ArgumentNullException(nameof(doc));

			if (!Path.IsPathRooted(doc.Name))
				throw new InvalidOperationException("Save the drawing before updating references.");

			var project = _pdsx.Project.GetAll().Single();

			var document = _mdbx.Document.GetAll().SingleOrDefault(x => x.ProjectId == project.Id &&
				string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(doc.Name), StringComparison.OrdinalIgnoreCase))
				?? throw new InvalidOperationException("Analyze this document before updating references.");

			using var documentLock = doc.LockDocument();
			TitleBlockInfo selectedTitle;
			AcDb.ObjectId spaceId;

			using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
			{
				var title = tr.GetObject(titleBlockId, AcDb.OpenMode.ForRead) as AcDb.BlockReference
					?? throw new InvalidOperationException("Select a title block first.");

				selectedTitle = new TitleBlockReader().ReadSelected(tr, titleBlockId);				
				
				spaceId = title.OwnerId;
			}
			var sheet = _mdbx.Sheet.GetByDocumentId(document.Id).SingleOrDefault(x =>
				string.Equals(x.SheetNo, selectedTitle.SheetNo, StringComparison.OrdinalIgnoreCase))
				?? throw new InvalidOperationException("Analyze the selected sheet before updating references.");

			var formatter = new ReferenceTextFormatter();
			var errors = new List<string>();
			var updated = 0;
			var unchanged = 0;

			foreach (var link in _mdbx.CadSymbolLink.GetBySheetId(sheet.Id))
			{
				var reference = _mdbx.CadSymbolLink.GetReferenceContact(link.Id);
				var designation = reference?.Designation ?? "<unread>";

				try
				{
					var id = doc.Database.GetObjectId(false, new AcDb.Handle(link.BlockHandle), 0);
					if (id.IsNull || !id.IsValid || id.IsErased)
						throw new InvalidOperationException("The reference symbol is missing. Analyze the sheet again.");

					if (EngineeringObjectLink.Read(doc.Database, id) != link.EngineeringObjectId)
						throw new InvalidOperationException("The symbol's object GUID differs from the database. Analyze again.");

					using var tr = doc.Database.TransactionManager.StartTransaction();

					var block = tr.GetObject(id, AcDb.OpenMode.ForRead) as AcDb.BlockReference 
						?? throw new InvalidOperationException("The handle no longer identifies a block.");

					if (block.OwnerId != spaceId || !selectedTitle.IncludesLayer(block.Layer))
						throw new InvalidOperationException("The reference symbol has moved to another sheet. Analyze again.");

					var definition = (AcDb.BlockTableRecord)tr.GetObject(block.IsDynamicBlock
						? block.DynamicBlockTableRecord : block.BlockTableRecord, AcDb.OpenMode.ForRead);

					var attributes = new Dictionary<string, AcDb.AttributeReference>(StringComparer.OrdinalIgnoreCase);

					foreach (AcDb.ObjectId attributeId in block.AttributeCollection)
					{
						if (attributeId.IsErased || !attributeId.IsValid) continue;
						if (tr.GetObject(attributeId, AcDb.OpenMode.ForRead) is AcDb.AttributeReference attribute)
						{
							if (attributes.ContainsKey(attribute.Tag))
								throw new InvalidOperationException($"Duplicate attribute '{attribute.Tag}'.");
							attributes.Add(attribute.Tag, attribute);
						}
					}

					string Value(string tag) => attributes.TryGetValue(tag, out var attribute) ? attribute.TextString : string.Empty;

					designation = Value(LocationAttributeTag);

					if (!attributes.TryGetValue("XREF", out var xref))
					{
						// Physical symbols without XREF do not need reference presentation.
						if (reference == null)
							continue;

						throw new InvalidOperationException(
							"The reference symbol has no writable XREF attribute.");
					}

					if (TerminalImportRules.IsReferenceContact(definition.Name) !=
						(reference != null))
					{
						throw new InvalidOperationException(
							"The symbol role has changed. Analyze again.");
					}

					if (reference != null &&
						(!string.Equals(
							definition.Name,
							reference.BlockName,
							StringComparison.OrdinalIgnoreCase) ||
						 designation != reference.Designation ||
						 Value("PLAC") != reference.Placement))
					{
						throw new InvalidOperationException(
							"The reference symbol differs from its stored analysis. Analyze again.");
					}

					var currentSlots = TerminalImportRules.Read(
							attributes.Values.Select(x => (x.Tag, x.TextString)),
							reference != null)
						.ToDictionary(
							x => x.Tag,
							x => x.Designation,
							StringComparer.OrdinalIgnoreCase);

					Dictionary<string, string> storedSlots;

					if (reference != null)
					{
						storedSlots = _mdbx.CadSymbolLink.GetReferenceTerminals(link.Id)
							.ToDictionary(
								x => x.Tag,
								x => x.Value,
								StringComparer.OrdinalIgnoreCase);
					}
					else
					{
						var catalog = _mdbx.CadSymbolLink
							.GetObjectTerminals(link.EngineeringObjectId)
							.ToDictionary(x => x.Id);

						storedSlots = _mdbx.CadSymbolLink.GetTerminals(link.Id)
							.ToDictionary(
								x => x.Tag,
								x => catalog[x.EngineeringObjectTerminalId].Designation,
								StringComparer.OrdinalIgnoreCase);

						if (storedSlots.Count == 0 && currentSlots.Count == 0)
							continue;

						var entity = _mdbx.EngineeringObject.GetById(link.EngineeringObjectId)
							?? throw new InvalidOperationException(
								"The engineering object no longer exists.");

						EnsureObjectIsActive(entity);

						var locations = _mdbx.Location
							.GetByProjectId(project.Id)
							.ToDictionary(x => x.Id);

						if (entity.ProjectId != project.Id ||
							!entity.LocationId.HasValue ||
							!SamePath(
								ParseLocation(designation),
								ReadPath(entity.LocationId.Value, locations)))
						{
							throw new InvalidOperationException(
								"The symbol designation differs from the database. Analyze again.");
						}
					}

					if (currentSlots.Count != storedSlots.Count ||
						storedSlots.Any(x =>
							!currentSlots.TryGetValue(x.Key, out var value) ||
							value != x.Value))
					{
						throw new InvalidOperationException(
							"Reference terminal values have changed. Analyze again.");
					}


					var targets = _mdbx.CadSymbolLink.GetReferenceTargets(link.Id);
					var text = formatter.Format(document.Id, targets);
					if (xref.TextString == text) { unchanged++; continue; }
					// An unused contact has no targets; clear any obsolete reference text.
					xref.UpgradeOpen();
					xref.TextString = text;
					tr.Commit();
					updated++;
				}
				catch (Exception exception)
				{
					errors.Add($"Block {link.BlockHandle:X}, BET1='{designation}': {exception.Message}");
				}
			}
			return (updated, unchanged, errors.AsReadOnly());
		}

	}
}
