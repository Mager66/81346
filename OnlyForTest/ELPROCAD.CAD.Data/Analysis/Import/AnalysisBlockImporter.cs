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
		private readonly IPdsxRepository _pdsx;
		private readonly IMdbxRepository _mdbx;
		private readonly CadSymbolLinkReconciler _symbolLinkReconciler;

		// TODO: Replace legacy BET1 with DES1 when the symbols are migrated.
		private const string LocationAttributeTag = "BET1";
		// TODO: Load designation syntax from project configuration.
		private const string LocationPrefix = "+";
		private const char LevelSeparator = '.';

		// Retained for compatibility; per-block copy state lives in ImportBlock.
		internal Guid? CopiedObjectId { get; set; }
		public AnalysisBlockImporter(IPdsxRepository pdsx, IMdbxRepository mdbx, CadSymbolLinkReconciler symbolLinkReconciler)
		{
			_pdsx = pdsx ?? throw new ArgumentNullException(nameof(pdsx));
			_mdbx = mdbx ?? throw new ArgumentNullException(nameof(mdbx));
			_symbolLinkReconciler = symbolLinkReconciler ?? throw new ArgumentNullException(nameof(symbolLinkReconciler));
		}
		/// <summary>Imports one sheet, applies CAD presentation changes and reconciles missing links.</summary>
		public (int CreatedLocations, int ProcessedBlocks, int RemovedSymbolLinks,
			IReadOnlyList<(AcDb.ObjectId BlockId, string Message)> Errors)
			Import(AcAp.Document doc, SheetAnalysisData result)
		{
			ValidateImportInput(doc, result);
			var context = CreateImportContext(doc, result);

			ReadImportBlocks(doc, result, context);
			ValidateTerminalPresentations(doc, context);
			var groups = GroupLinkedMainSymbols(context);
			ValidateMainSymbolGroups(groups, context);

			ApplyLinkedMainSymbols(groups, context);
			ImportUnlinkedMainSymbols(context);
			ResolveMemberSymbols(context);

			var finalLocations = _mdbx.Location.GetByProjectId(context.ProjectId)
				.ToDictionary(location => location.Id);
			var processedBlocks = UpdateCadPresentations(doc, context, finalLocations);
			var removedLinks = ReconcileSymbolLinks(doc, context);
			var createdLocations = finalLocations.Keys.Count(id => !context.OriginalLocations.ContainsKey(id));

			return (createdLocations, processedBlocks, removedLinks, context.Errors.AsReadOnly());
		}
	}
}

