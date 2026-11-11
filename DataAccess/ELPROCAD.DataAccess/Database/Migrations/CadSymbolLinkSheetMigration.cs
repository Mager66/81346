using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;

namespace ELPROCAD.DataAccess.Database.Migrations
{
	internal static class CadSymbolLinkSheetMigration
	{
		/// <summary>
		/// Copies legacy object sheet references to existing CAD symbol links.
		/// The caller must provide a dedicated unit of work.
		/// </summary>
		internal static void Apply(UnitOfWork unitOfWork)
		{
			if (unitOfWork == null)
				throw new ArgumentNullException(nameof(unitOfWork));

			// Materialize before changing any records.
			var links = new XPQuery<CadSymbolLink>(unitOfWork).ToList();

			var pending = new List<(CadSymbolLink Link, Guid SheetId)>();

			foreach (var link in links)
			{
				if (link.SheetId != Guid.Empty)
					continue;

				var entity = unitOfWork.GetObjectByKey<EngineeringObject>(
					link.EngineeringObjectId);

				if (entity?.LegacySheetId is not Guid sheetId)
				{
					throw new InvalidOperationException(
						$"Cannot migrate CAD symbol link '{link.Id}': " +
						"the previous sheet reference is missing.");
				}

				var sheet = unitOfWork.GetObjectByKey<Sheet>(sheetId);

				if (sheet == null || sheet.DocumentId != link.DocumentId)
				{
					throw new InvalidOperationException(
						$"Cannot migrate CAD symbol link '{link.Id}': " +
						"the previous sheet does not belong to the linked document.");
				}

				pending.Add((link, sheetId));
			}

			// Write only after every pending reference has been validated.
			foreach (var item in pending)
			{
				item.Link.SheetId = item.SheetId;
				unitOfWork.Save(item.Link);
			}

			if (pending.Count > 0)
				unitOfWork.CommitChanges();
		}
	}
}