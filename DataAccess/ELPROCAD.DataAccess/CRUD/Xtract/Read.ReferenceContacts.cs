using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.References;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;

namespace ELPROCAD.DataAccess.CRUD
{
	internal sealed partial class Read
	{
		internal IReadOnlyList<(Guid DocumentId, string DocumentName, string SheetNo, string? Column, string? Row)> GetReferenceTargets(Guid symbolLinkId)
		{
			return _database.Execute(uow =>
			{
				var source = uow.GetObjectByKey<CadSymbolLink>(symbolLinkId)
					?? throw new InvalidOperationException("The reference symbol link no longer exists.");

				if (source.IsDetached)
					throw new InvalidOperationException("The reference symbol is disconnected.");

				var entity = uow.GetObjectByKey<EngineeringObject>(source.EngineeringObjectId);

				if (entity == null || entity.IsMarkedForDeletion)
					throw new InvalidOperationException("The engineering object is missing or marked for deletion.");

				var reference = new XPQuery<CadReferenceContact>(uow).SingleOrDefault(x => x.CadSymbolLinkId == symbolLinkId);
				var catalog = new XPQuery<EngineeringObjectTerminal>(uow).Where(x => x.EngineeringObjectId == entity.Id).ToList().ToDictionary(x => x.Id);

				HashSet<string> ReadValues(Guid linkId, CadReferenceContact? contact)
				{
					if (contact != null)
						return new HashSet<string>(new XPQuery<CadReferenceContactTerminal>(uow)
							.Where(x => x.ReferenceContactId == contact.Id).ToList()
							.Select(x => x.Value.Trim()).Where(x => x.Length != 0),
							StringComparer.OrdinalIgnoreCase);

					var physicalValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

					foreach (var slot in new XPQuery<CadSymbolTerminal>(uow).Where(x => x.CadSymbolLinkId == linkId).ToList())
					{
						if (!catalog.TryGetValue(slot.EngineeringObjectTerminalId, out var terminal))
							throw new InvalidOperationException("A symbol references an invalid object terminal.");

						physicalValues.Add(terminal.Designation);
					}
					return physicalValues;
				}

				var values = ReadValues(source.Id, reference);
				var claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var result = new List<(Guid DocumentId, string DocumentName, string SheetNo, string? Column, string? Row)>();

				foreach (var link in new XPQuery<CadSymbolLink>(uow).Where(x => x.EngineeringObjectId == entity.Id && x.DetachedAtUtc == null).ToList())
				{
					var targetReference = new XPQuery<CadReferenceContact>(uow)
						.SingleOrDefault(x => x.CadSymbolLinkId == link.Id);

					// Match only opposite roles: reference to physical, or physical to reference.
					if ((reference != null) == (targetReference != null))
						continue;

					var matches = ReadValues(link.Id, targetReference);
					matches.IntersectWith(values);

					if (matches.Count == 0)
						continue;

					if (targetReference == null && matches.Any(value => !claims.Add(value)))
						throw new InvalidOperationException("A referenced terminal is occupied by multiple active symbols.");

					var document = uow.GetObjectByKey<Document>(link.DocumentId);
					var sheet = uow.GetObjectByKey<Sheet>(link.SheetId);

					if (document == null || sheet == null || sheet.DocumentId != document.Id ||	document.ProjectId != entity.ProjectId)
						throw new InvalidOperationException("A target symbol has invalid document or sheet references.");

					if ((link.GridColumn == null) != (link.GridRow == null))
						throw new InvalidOperationException("A target symbol has an incomplete grid coordinate.");

					result.Add((document.Id, document.Name, sheet.SheetNo, link.GridColumn, link.GridRow));
				}
				return (IReadOnlyList<(Guid DocumentId, string DocumentName, string SheetNo, string? Column, string? Row)>)result.AsReadOnly();
			});
		}
	}
}
