#if ARX_APP
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP || PROGECAD_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.CadEntity
{
	public static class EngineeringObjectLink
	{
		private const string ApplicationName = "ELPROCAD_OBJECT";

		/// <summary>
		/// Reads the linked engineering object identifier.
		/// Returns null when no link exists.
		/// Malformed data is reported instead of treated as a missing link.
		/// </summary>
		public static Guid? Read(AcDb.Database database, AcDb.ObjectId blockId)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			using var transaction = database.TransactionManager
				.StartOpenCloseTransaction();

			var block = GetBlock(transaction, blockId);

			return Read(block);
		}

		/// <summary>
		/// Adds an engineering object link.
		/// An existing link to a different object is never overwritten.
		/// </summary>
		public static void Write(AcDb.Database database, AcDb.ObjectId blockId,	Guid objectId)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			if (objectId == Guid.Empty)
				throw new ArgumentException("An engineering object identifier is required.", nameof(objectId));

			using var transaction = database.TransactionManager.StartTransaction();

			var block = GetBlock(transaction, blockId);
			var existingId = Read(block);

			if (existingId.HasValue)
			{
				if (existingId.Value != objectId)				
					throw new InvalidOperationException($"Block {blockId.Handle} is already linked to another engineering object.");				

				return;
			}

			var applications = (AcDb.RegAppTable)transaction.GetObject(database.RegAppTableId, AcDb.OpenMode.ForRead);

			if (!applications.Has(ApplicationName))
			{
				applications.UpgradeOpen();

				using var registration = new AcDb.RegAppTableRecord
				{
					Name = ApplicationName
				};

				applications.Add(registration);
				transaction.AddNewlyCreatedDBObject(registration, true);
			}

			block.UpgradeOpen();

			using var data = new AcDb.ResultBuffer(
				new AcDb.TypedValue(
					(int)AcDb.DxfCode.ExtendedDataRegAppName,
					ApplicationName),
				new AcDb.TypedValue(
					(int)AcDb.DxfCode.ExtendedDataAsciiString,
					objectId.ToString("D")));

			block.XData = data;

			transaction.Commit();
		}


		/// <summary>
		/// Replaces an inherited object link on a copied symbol.
		/// The existing identifier must match the identifier captured before import.
		/// </summary>
		public static void ReassignCopy(AcDb.Database database, AcDb.ObjectId blockId, Guid expectedObjectId, Guid newObjectId)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			if (expectedObjectId == Guid.Empty)
				throw new ArgumentException(
					"The inherited object identifier is required.",
					nameof(expectedObjectId));

			if (newObjectId == Guid.Empty || newObjectId == expectedObjectId)
				throw new ArgumentException(
					"A different, non-empty object identifier is required.",
					nameof(newObjectId));

			using var transaction = database.TransactionManager
				.StartTransaction();

			var block = GetBlock(transaction, blockId);
			var currentId = Read(block);

			if (currentId == newObjectId)
				return;

			if (currentId != expectedObjectId)
			{
				throw new InvalidOperationException(
					"The copied symbol's object link changed during import.");
			}

			// The existing XData guarantees that the application is registered.
			block.UpgradeOpen();

			using var data = new AcDb.ResultBuffer(
				new AcDb.TypedValue(
					(int)AcDb.DxfCode.ExtendedDataRegAppName,
					ApplicationName),
				new AcDb.TypedValue(
					(int)AcDb.DxfCode.ExtendedDataAsciiString,
					newObjectId.ToString("D")));

			block.XData = data;

			transaction.Commit();
		}

		private static AcDb.BlockReference GetBlock(AcDb.Transaction transaction, AcDb.ObjectId blockId)
		{
			if (transaction.GetObject(blockId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)			
				throw new ArgumentException("The identifier must reference a block.", nameof(blockId));			

			return block;
		}

		private static Guid? Read(AcDb.BlockReference block)
		{
			using var data = block.GetXDataForApplication(ApplicationName);

			if (data == null)
				return null;

			var values = data.AsArray();

			if (values.Length != 2 ||
				values[0].TypeCode !=
					(int)AcDb.DxfCode.ExtendedDataRegAppName ||
				!string.Equals(
					values[0].Value as string,
					ApplicationName,
					StringComparison.Ordinal) ||
				values[1].TypeCode !=
					(int)AcDb.DxfCode.ExtendedDataAsciiString ||
				!Guid.TryParse(values[1].Value as string, out var objectId) ||
				objectId == Guid.Empty)
			{
				throw new InvalidOperationException($"Block {block.Handle} contains an invalid engineering object link.");
			}

			return objectId;
		}
	}
}
