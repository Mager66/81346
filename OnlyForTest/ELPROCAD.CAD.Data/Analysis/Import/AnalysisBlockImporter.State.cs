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
		/// <summary>State belonging to one import call; never retained on the importer instance.</summary>
		private sealed class ImportContext
		{
			internal Guid ProjectId { get; }

			internal IDocument Document { get; }

			internal ISheet Sheet { get; }

			internal IReadOnlyDictionary<Guid, ILocation> OriginalLocations { get; }

			internal IReadOnlyDictionary<Guid, string[]> OriginalPaths { get; }

			internal List<ImportBlock> Items { get; } = new();

			internal List<(AcDb.ObjectId BlockId, string Message)> Errors { get; } = new();

			internal HashSet<AcDb.ObjectId> FailedBlocks { get; } = new();

			internal Dictionary<AcDb.ObjectId, Guid> ResolvedObjects { get; } = new();

			internal Dictionary<AcDb.ObjectId, string> Designations { get; } = new();

			internal ImportContext(Guid projectId, IDocument document, ISheet sheet,
				IReadOnlyDictionary<Guid, ILocation> locations)
			{
				ProjectId = projectId;
				Document = document;
				Sheet = sheet;
				OriginalLocations = locations;
				OriginalPaths = locations.Values.ToDictionary(location => location.Id,
					location => ReadPath(location.Id, locations));
			}

			internal void Fail(AcDb.ObjectId blockId, string message)
			{
				if (!FailedBlocks.Add(blockId))
					return;

				var designation = Designations.TryGetValue(blockId, out var value)
					? value
					: "<missing>";

				Errors.Add((blockId, $"BET1='{designation}': {message}"));
			}
		}

		private sealed class ImportBlock
		{
			internal ImportBlock(CadBlock block, CadAttribute attribute, string[] requestedLevels, bool isMember)
			{
				Block = block;
				Attribute = attribute;
				RequestedLevels = requestedLevels;
				IsMember = isMember;
			}

			internal CadBlock Block { get; }

			internal CadAttribute Attribute { get; }

			internal bool IsReferenceContact => TerminalImportRules.IsReferenceContact(Block.BlockName);

			internal string[] RequestedLevels { get; }

			internal bool IsMember { get; }

			internal Guid? LinkedObjectId { get; set; }

			internal Guid? OriginalXDataId { get; set; }
			internal ICadSymbolLink? RegisteredLink { get; set; }
			internal IReadOnlyList<(string Tag, string Designation)> Terminals { get; set; }
				= Array.Empty<(string Tag, string Designation)>();

			/// <summary>
			/// Gets or sets the object identifier inherited by a copied main symbol.
			/// Null indicates that the block has not been classified as a copy.
			/// </summary>
			internal Guid? CopiedObjectId { get; set; }

			internal Guid? SourceLocationId { get; set; }

			internal string[] OriginalLevels { get; set; } = Array.Empty<string>();
		}
	}
}

