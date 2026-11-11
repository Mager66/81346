#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.TitleBlocks
{
	public sealed class TitleBlockReader
	{
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
					SheetLayer = ReadSheetLayer(block, transaction)
				});
			}
			return results;
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
	}
}
