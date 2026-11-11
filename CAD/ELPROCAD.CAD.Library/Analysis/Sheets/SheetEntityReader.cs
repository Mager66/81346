#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetEntityReader
	{
		private const string BlockPrefix = "EPCB";

		public SheetEntities Read(AcDb.Transaction transaction, AcDb.ObjectId spaceId, string layerName)
		{
			if (string.IsNullOrWhiteSpace(layerName))
				throw new ArgumentException(
					"Bladets lagernamn måste anges.", nameof(layerName));

			var space = (AcDb.BlockTableRecord)transaction.GetObject(
				spaceId, AcDb.OpenMode.ForRead);

			var layers = (AcDb.LayerTable)transaction.GetObject(
				space.Database.LayerTableId, AcDb.OpenMode.ForRead);

			if (!layers.Has(layerName))
				throw new InvalidOperationException(
					$"Lagret '{layerName}' finns inte.");

			var layerId = layers[layerName];
			var blockIds = new List<AcDb.ObjectId>();
			var polylineIds = new List<AcDb.ObjectId>();

			foreach (AcDb.ObjectId entityId in space)
			{
				if (transaction.GetObject(entityId, AcDb.OpenMode.ForRead) is not AcDb.Entity entity)
					continue;				

				if (entity.LayerId != layerId)
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

			return new SheetEntities(layerName, blockIds, polylineIds);
		}		
	}
}
