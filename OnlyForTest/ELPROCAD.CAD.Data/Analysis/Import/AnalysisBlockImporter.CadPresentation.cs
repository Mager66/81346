using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{
		private int UpdateCadPresentations(AcAp.Document doc, ImportContext context, IReadOnlyDictionary<Guid, ILocation> finalLocations, SheetAnalysisData analysis)
		{
			var processedBlocks = 0;

			// Release obsolete physical mappings on reference symbols first.
			foreach (var item in context.Items.OrderByDescending(x => x.IsReferenceContact))
			{
				if (context.FailedBlocks.Contains(item.Block.ObjectId) || !context.ResolvedObjects.TryGetValue(item.Block.ObjectId, out var objectId))
					continue;

				try
				{
					var finalPath = ReadPath(
						item.SourceLocationId!.Value,
						finalLocations);

					VerifyLocationAttributes(doc, item, finalPath);
					SaveSymbolTerminals(context, item, objectId, analysis);

					// Replace inherited XData only when the resolved object has a different GUID.
					if (item.CopiedObjectId.HasValue &&	item.CopiedObjectId.Value != objectId)
					{
						EngineeringObjectLink.ReassignCopy(
							doc.Database,
							item.Block.ObjectId,
							item.CopiedObjectId.Value,
							objectId);
					}
					else
					{
						EngineeringObjectLink.Write(
							doc.Database,
							item.Block.ObjectId,
							objectId);
					}

					processedBlocks++;
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					context.Fail(item.Block.ObjectId, exception.Message);
				}
			}

			return processedBlocks;
		}
		private int ReconcileSymbolLinks(AcAp.Document doc, ImportContext context)
		{
			var reconciliation = _symbolLinkReconciler.Reconcile(doc.Database,
				context.Document.Id, context.Sheet.Id);
			// Missing CAD objects have no usable ObjectId; their handles remain in the messages.
			foreach (var message in reconciliation.Errors)
				context.Errors.Add((AcDb.ObjectId.Null, message));
			return reconciliation.RemovedLinks;
		}
	}
}

