#if BRX_APP
using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetEntityReader
	{
		private const string BlockPrefix = "EPCB";

		public SheetEntities Read(AcDb.Transaction transaction, AcDb.ObjectId spaceId, TitleBlockInfo titleBlock)
		{
			if (titleBlock == null)
				throw new ArgumentNullException(nameof(titleBlock));

			var space = (AcDb.BlockTableRecord)transaction.GetObject(spaceId, AcDb.OpenMode.ForRead);
			var blockIds = new List<AcDb.ObjectId>();
			var polylineIds = new List<AcDb.ObjectId>();

			foreach (AcDb.ObjectId entityId in space)
			{
				if (transaction.GetObject(entityId, AcDb.OpenMode.ForRead) is not AcDb.Entity entity)
					continue;

				if (!titleBlock.IncludesLayer(entity.Layer))
					continue;

				if (entity is AcDb.BlockReference block)
				{
					var definitionId = block.IsDynamicBlock
						? block.DynamicBlockTableRecord
						: block.BlockTableRecord;

					var definition = (AcDb.BlockTableRecord)transaction.GetObject(
						definitionId, AcDb.OpenMode.ForRead);

					if (definition.Name.StartsWith(
						BlockPrefix, StringComparison.OrdinalIgnoreCase))
					{
						blockIds.Add(entityId);
					}
				}
				else if (entity is AcDb.Polyline ||
						 entity is AcDb.Polyline2d ||
						 entity is AcDb.Polyline3d)
				{
					polylineIds.Add(entityId);
				}
			}

			return new SheetEntities(titleBlock.SheetLayer, blockIds, polylineIds);
		}		
	}
}
