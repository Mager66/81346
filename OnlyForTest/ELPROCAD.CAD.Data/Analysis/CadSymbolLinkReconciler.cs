using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;

#if ARX_APP
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP || PROGECAD_APP
using AcDb = Teigha.DatabaseServices;
#endif


namespace ELPROCAD.CAD.Data.Analysis
{
	/// <summary>
	/// Removes persisted symbol links whose CAD objects no longer exist.
	/// Engineering objects and aspect relationships are preserved.
	/// </summary>
	public sealed class CadSymbolLinkReconciler
	{
		private readonly IMdbxRepository _mdbx;

		internal CadSymbolLinkReconciler(IMdbxRepository mdbx) => _mdbx = mdbx ?? throw new ArgumentNullException(nameof(mdbx));

		internal(int RemovedLinks, IReadOnlyList<string> Errors) Reconcile(AcDb.Database database, Guid documentId, Guid sheetId)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			var links = _mdbx.CadSymbolLink.GetBySheetId(sheetId);
			var candidates = new List<ICadSymbolLink>();
			var errors = new List<string>();

			using(var tr = database.TransactionManager.StartOpenCloseTransaction())
			{
				foreach (var link in links)
				{
					if(link.DocumentId != documentId)
					{
						errors.Add($"Symbol link '{link.Id}' references another document. " +
							"The link was preserved.");

						continue;
					}

					var found =database.TryGetObjectId(new AcDb.Handle(link.BlockHandle), out var objectId);

					if(!found ||  objectId.IsNull)
					{
						errors.Add($"Block {link.BlockHandle:X}: " +
							"the CAD object identifier could not be validated. " +
							"The link was preserved.");

						continue;
					}

					if(objectId.IsErased || objectId.IsEffectivelyErased)
					{
						candidates.Add(link);
						continue;
					}

					var cadObject = tr.GetObject(objectId, AcDb.OpenMode.ForRead);

					if(cadObject is not AcDb.BlockReference)
						errors.Add($"Handle {link.BlockHandle:X} no longer identifies " +
							"a block reference. The link was preserved.");
				}				
			}

			var removedLinks = 0;

			foreach (var link in candidates)
			{
				// Delete uses the captured version to detect concurrent changes.
				var validation = _mdbx.CadSymbolLink.Delete(link);

				if (!validation.IsValid)
				{
					var message = string.Join(
						Environment.NewLine,
						validation.Errors
							.Select(error => error.ErrorMessage)
							.Distinct());

					errors.Add($"Block {link.BlockHandle:X}: {message}");

					continue;
				}

				removedLinks++;
			}

			return (removedLinks, errors.AsReadOnly());
		}
	}
}
