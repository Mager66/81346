using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#if BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public static class SheetSelectionStore
	{
		private sealed class Selection
		{
			public AcDb.ObjectId TitleBlockId { get; }

			public Selection(AcDb.ObjectId titleBlockId)
			{
				TitleBlockId = titleBlockId;
			}
		}

		private static readonly Dictionary<string, List<AcDb.ObjectId>> selections = new(StringComparer.OrdinalIgnoreCase);

		public static void Set(AcAp.Document doc, AcDb.ObjectId titleBlockId)
		{
			if (doc == null)
				throw new ArgumentNullException(nameof(doc));

			if (doc.Database == null)
				throw new ArgumentNullException(nameof(doc.Database));			

			if (titleBlockId.IsNull ||
				!titleBlockId.IsValid ||
				titleBlockId.IsErased)
			{
				throw new ArgumentException(
					"Blankettens objekt-ID är inte giltigt.", nameof(titleBlockId));
			}

			if (doc.Database != titleBlockId.Database)
				throw new ArgumentException("Blanketten tillhör inte den angivna ritningen.", nameof(titleBlockId));

			var fingerprint = doc.Database.FingerprintGuid;

			if (!selections.TryGetValue(fingerprint, out var titleBlocks))
			{
				titleBlocks = new List<AcDb.ObjectId>();
				selections.Add(fingerprint, titleBlocks);
			}

			if (!titleBlocks.Contains(titleBlockId))
				titleBlocks.Add(titleBlockId);
		}
		

		public static bool TryGet(AcAp.Document doc, out AcDb.ObjectId titleBlockId)
		{
			if (doc == null)
				throw new ArgumentNullException(nameof(doc));

			if (doc.Database == null)
				throw new ArgumentNullException(nameof(doc.Database));

			//var titleBlockIds = Array.Empty<AcDb.ObjectId>();
			var fingerprint = doc.Database.FingerprintGuid;

			if (!selections.TryGetValue(fingerprint, out var storedIds))
			{
				titleBlockId = AcDb.ObjectId.Null;
				return false;
			}

			storedIds.RemoveAll(id =>
			id.IsNull ||
			!id.IsValid ||
			id.IsErased);

			if (storedIds.Count == 0)
			{
				selections.Remove(fingerprint);
				titleBlockId = AcDb.ObjectId.Null;
				return false;
			}

			if (storedIds.Count == 1)
			{
				titleBlockId = storedIds[0];
				return true;
			}

			var sheetId = FindTitleBlockOnCurrentLayer(doc, storedIds);
			if (sheetId != AcDb.ObjectId.Null)
			{
				titleBlockId = sheetId;
				return true;
			}

			titleBlockId = AcDb.ObjectId.Null;
			return false;
		}
		
		public static void Clear(AcDb.Database database)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			selections.Remove(database.FingerprintGuid);
		}

		private static AcDb.ObjectId FindTitleBlockOnCurrentLayer(AcAp.Document doc, List<AcDb.ObjectId> storedIds)
		{
			using(var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
			{
				var clayer = (AcDb.LayerTableRecord)tr.GetObject(doc.Database.Clayer, AcDb.OpenMode.ForRead);

				foreach(AcDb.ObjectId candidateId in storedIds)
				{
					var blk = (AcDb.BlockReference)tr.GetObject(candidateId, AcDb.OpenMode.ForRead);
					if (blk.IsDynamicBlock || blk.IsErased)
						continue;

					foreach(AcDb.ObjectId attOid in blk.AttributeCollection)
					{
						if (tr.GetObject(attOid, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference attRef)
							continue;

						if(string.Equals(
							attRef.Tag, "BLAD", StringComparison.OrdinalIgnoreCase)
							&&
							string.Equals(
								attRef.TextString, clayer.Name, StringComparison.OrdinalIgnoreCase))
						{
							return candidateId;
						}
					}
				}			
				
			}
			return AcDb.ObjectId.Null;
		}
	}
}
