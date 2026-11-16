#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.TitleBlocks
{
	public sealed class TitleBlockReader
	{
		// TODO: Make "EPCB000" and "BLAD" user setting
		private const string BlockName = "EPCB000";
		private const string SheetTag = "BLAD";

		public IReadOnlyList<TitleBlockInfo> Read(AcDb.Transaction transaction, AcDb.ObjectId spaceId)
		{
			var results = new List<TitleBlockInfo>();
			var space = (AcDb.BlockTableRecord)transaction.GetObject(spaceId, AcDb.OpenMode.ForRead);

			foreach (AcDb.ObjectId entityId in space)
			{
				if (transaction.GetObject(entityId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)
					continue;

				var definitionId = block.IsDynamicBlock
			   ? block.DynamicBlockTableRecord
			   : block.BlockTableRecord;

				var definition = (AcDb.BlockTableRecord)transaction.GetObject(definitionId, AcDb.OpenMode.ForRead);

				if (!string.Equals(
			   definition.Name,
			   BlockName,
			   StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				results.Add(new TitleBlockInfo
				{
					ObjectId = block.ObjectId,
					SheetLayer = block.Layer,
					SheetNo = ReadSheetNo(block, transaction).Trim()
				});
			}
			return results;
		}

		/// <summary>
		/// Determines drawing type from title blocks in all drawing spaces.
		/// </summary>
		public TitleBlockInfo ReadSelected(AcDb.Transaction transaction, AcDb.ObjectId titleBlockId)
		{
			var database = titleBlockId.Database;

			var layouts = (AcDb.DBDictionary)transaction.GetObject(
				database.LayoutDictionaryId,
				AcDb.OpenMode.ForRead);

			var titles = new List<TitleBlockInfo>();

			foreach (AcDb.DBDictionaryEntry entry in layouts)
			{
				var layout = (AcDb.Layout)transaction.GetObject(
					entry.Value,
					AcDb.OpenMode.ForRead);

				titles.AddRange(Read(transaction, layout.BlockTableRecordId));
			}

			var selected = titles.SingleOrDefault(
				title => title.ObjectId == titleBlockId)
				?? throw new InvalidOperationException(
					"The selected block is not a title block.");

			foreach (var title in titles)
			{
				if (string.IsNullOrWhiteSpace(title.SheetNo))
				{
					throw new InvalidOperationException(
						$"Title block {title.ObjectId.Handle}: BLAD is required.");
				}

				title.IsMultiSheet = titles.Count > 1;

				if (title.IsMultiSheet &&
					!string.Equals(
						title.SheetLayer,
						title.SheetNo,
						StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException(
						$"Title block {title.ObjectId.Handle}: " +
						$"layer '{title.SheetLayer}' must match BLAD '{title.SheetNo}'.");
				}
			}

			if (titles
				.GroupBy(title => title.SheetNo, StringComparer.OrdinalIgnoreCase)
				.Any(group => group.Count() > 1))
			{
				throw new InvalidOperationException(
					"Sheet numbers must be unique within the drawing.");
			}

			return selected;
		}

		private static string ReadSheetLayer(AcDb.BlockReference block, AcDb.Transaction transaction)
		{
			foreach (AcDb.ObjectId attributeId in block.AttributeCollection)
			{
				if (transaction.GetObject(attributeId, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference attribute)
				{
					continue;
				}

				if (string.Equals(
					attribute.Tag,
					SheetTag,
					StringComparison.OrdinalIgnoreCase))
				{
					return attribute.TextString;
				}
			}

			return string.Empty;
		}

		private static string ReadSheetNo(AcDb.BlockReference block, AcDb.Transaction transaction)
		{
			foreach (AcDb.ObjectId attributeId in block.AttributeCollection)
			{
				if (transaction.GetObject(attributeId, AcDb.OpenMode.ForRead) is not AcDb.AttributeReference attribute)
				{
					continue;
				}

				if (string.Equals(
					attribute.Tag,
					SheetTag,
					StringComparison.OrdinalIgnoreCase))
				{
					return attribute.TextString;
				}
			}

			return string.Empty;
		}
	}
}
