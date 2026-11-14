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
		private int UpdateCadPresentations(AcAp.Document doc, ImportContext context, IReadOnlyDictionary<Guid, ILocation> finalLocations)
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

					SaveSymbolTerminals(context, item, objectId);

					WriteLocationAttribute(doc, item, finalPath);

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

		private static void WriteLocationAttribute(AcAp.Document doc, ImportBlock item, IReadOnlyList<string> levels)
		{
			var originalText = item.Attribute.Value;
			var trimmed = originalText.Trim();

			var prefix = trimmed.StartsWith(
				LocationPrefix,
				StringComparison.Ordinal)
					? LocationPrefix
					: string.Empty;

			var newText =
				prefix +
				string.Join(LevelSeparator.ToString(), levels) +
				(item.IsMember ? LevelSeparator.ToString() : string.Empty);

			if (string.Equals(originalText, newText, StringComparison.Ordinal))
				return;

			using var transaction = doc.Database.TransactionManager
				.StartTransaction();

			var attribute = transaction.GetObject(
				item.Attribute.ObjectId,
				AcDb.OpenMode.ForRead) as AcDb.AttributeReference
				?? throw new InvalidOperationException(
					"The location attribute no longer exists.");

			if (!string.Equals(
				attribute.TextString,
				originalText,
				StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					"The location attribute changed during analysis. " +
					"The database may already have been updated; run the analysis again.");
			}

			attribute.UpgradeOpen();
			attribute.TextString = newText;

			transaction.Commit();
		}
	}
}

