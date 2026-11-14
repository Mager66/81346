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
		/// <summary>Reads each block independently and collects recoverable errors.</summary>
		private void ReadImportBlocks(AcAp.Document doc, SheetAnalysisData result, ImportContext context)
		{
			foreach (var block in result.Blocks)
			{
				try
				{
					// Capture BET1 before validation or presentation changes.
					context.Designations[block.ObjectId] = block.GetAttribute(LocationAttributeTag)?.Value ?? "<missing>";

					var item = ReadImportBlock(doc, block, context);
					if (item != null)
						context.Items.Add(item);
				}
				catch (Exception exception) when (
					exception is ArgumentException || exception is InvalidOperationException)
				{
					context.Fail(block.ObjectId, exception.Message);
				}
			}
		}

		private ImportBlock? ReadImportBlock(AcAp.Document doc, CadBlock block, ImportContext context)
		{
			var xdataId = EngineeringObjectLink.Read(doc.Database, block.ObjectId);
			var registered = _mdbx.CadSymbolLink.GetByBlock(
				context.Document.Id, block.ObjectId.Handle.Value);

			ValidateRegisteredSymbol(registered, xdataId, context.Sheet.Id);

			var linkedId = registered?.EngineeringObjectId ?? xdataId;
			var attribute = block.GetAttribute(LocationAttributeTag);

			if (attribute == null || string.IsNullOrWhiteSpace(attribute.Value))
			{
				if (linkedId.HasValue)
					context.Fail(block.ObjectId, "The linked block has no location designation.");
				return null;
			}

			var item = new ImportBlock(
				block, attribute, ParseLocation(attribute.Value),
				attribute.Value.Trim().EndsWith(LevelSeparator.ToString(), StringComparison.Ordinal));

			item.OriginalXDataId = xdataId;
			item.RegisteredLink = registered;
			item.Terminals = ReadTerminalAttributes(block);

			item.LinkedObjectId = ResolveInitialObjectLink(item, registered, xdataId);
			ReadOriginalLocation(item, context);
			return item;
		}

		private static void ValidateRegisteredSymbol(ICadSymbolLink? registered, Guid? xdataId, Guid sheetId)
		{
			if (registered != null && registered.SheetId != sheetId)
				throw new InvalidOperationException("The CAD symbol is registered on another sheet.");

			if (registered != null && xdataId.HasValue &&
				registered.EngineeringObjectId != xdataId.Value)
			{
				throw new InvalidOperationException(
					"The registered symbol link and XData reference different objects.");
			}
		}

		/// <summary>
		/// Resolves unregistered symbols by designation instead of an inherited GUID.
		/// </summary>
		private static Guid? ResolveInitialObjectLink(ImportBlock item, ICadSymbolLink? registered, Guid? xdataId)
		{
			if (registered != null)
				return registered.EngineeringObjectId;

			// Retain the old GUID only for checked replacement after persistence.
			item.CopiedObjectId = xdataId;
			return null;
		}

		private void ReadOriginalLocation(ImportBlock item, ImportContext context)
		{
			if (!item.LinkedObjectId.HasValue)
				return;

			var entity = _mdbx.EngineeringObject.GetById(item.LinkedObjectId.Value)
				?? throw new InvalidOperationException("The linked engineering object no longer exists.");
			var location = GetRepresentedLocation(entity, context.ProjectId);

			if (!context.OriginalPaths.TryGetValue(location.Id, out var originalPath))
				throw new InvalidOperationException(
					"The object's location was not found in the project snapshot.");

			item.SourceLocationId = location.Id;
			item.OriginalLevels = originalPath;
		}
	}
}

