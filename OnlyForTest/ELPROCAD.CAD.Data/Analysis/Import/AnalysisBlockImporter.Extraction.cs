using ELPROCAD.Analysis.Extraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		/// <summary>Consumes the extractor's levels verbatim; never parses punctuation as hierarchy.</summary>
		private string[] ExtractLocationLevels(IEnumerable<(string Tag, string? Value)> attributes)
		{
			var result = _extractor.Extract(attributes);
			if (!result.IsValid)
				throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Message)));
			if (result.Aspects.Count != 1 || result.Aspects[0].Kind != AspectKind.Location ||
				result.Aspects[0].Structure != AspectStructure.Flat || result.Aspects[0].Levels.Count != 1 ||
				string.IsNullOrWhiteSpace(result.Aspects[0].Levels[0]))
				throw new InvalidOperationException("The ELPROCAD extractor must return exactly one flat Location entry.");
			return result.Aspects[0].Levels.ToArray();
		}

		/// <summary>
		/// ELPROCAD retains its separate placement and designation attributes.
		/// Verify the live values instead of writing the combined database designation into BET1.
		/// </summary>
		private void VerifyLocationAttributes(AcAp.Document doc, ImportBlock item, IReadOnlyList<string> levels)
		{
			if (!SamePath(item.RequestedLevels, levels))
				throw new InvalidOperationException("The stored Location differs from the extracted symbol designation. Analyze again.");
			using var transaction = doc.Database.TransactionManager.StartOpenCloseTransaction();
			if (!MatchesCapturedLocation(transaction, item))
				throw new InvalidOperationException("The placement or designation attributes changed during analysis. Analyze again.");
		}

		private bool MatchesCapturedLocation(AcDb.Transaction transaction, ImportBlock item)
		{
			var block = transaction.GetObject(item.Block.ObjectId, AcDb.OpenMode.ForRead) as AcDb.BlockReference;
			if (block == null) return false;
			var tags = new[] { _extractor.PlacementAttributeTag, _extractor.DesignationAttributeTag };
			var current = new List<AcDb.AttributeReference>();
			foreach (AcDb.ObjectId id in block.AttributeCollection)
			{
				if (id.IsErased || !id.IsValid) continue;
				if (transaction.GetObject(id, AcDb.OpenMode.ForRead) is AcDb.AttributeReference attribute &&
					tags.Contains(attribute.Tag, StringComparer.OrdinalIgnoreCase))
					current.Add(attribute);
			}
			if (current.Count != 2) return false;
			foreach (var tag in tags)
			{
				var captured = item.Block.GetAttribute(tag);
				var matches = current.Where(x => string.Equals(x.Tag, tag, StringComparison.OrdinalIgnoreCase)).ToArray();
				if (captured == null || matches.Length != 1 || matches[0].ObjectId != captured.ObjectId ||
					!string.Equals(matches[0].TextString, captured.Value, StringComparison.Ordinal)) return false;
			}
			return true;
		}
	}
}
