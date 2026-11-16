using System.Globalization;

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	/// <summary>Formats persisted reference locations without CAD or database dependencies.</summary>
	internal sealed class ReferenceTextFormatter
	{
		// Replace these defaults from configuration when project formatting settings are introduced.
		internal string CoordinatePattern { get; set; } = "({0}.{1})";
		internal string SheetPattern { get; set; } = "{0} {1}";
		internal string DocumentPattern { get; set; } = @"{0}\{1}";
		internal string Separator { get; set; } = "; ";

		internal string Format(Guid sourceDocumentId,
			IEnumerable<(Guid DocumentId, string DocumentName, string SheetNo,
				string? Column, string? Row)> targets)
		{
			return string.Join(Separator, targets
				.OrderBy(x => x.DocumentName, StringComparer.OrdinalIgnoreCase)
				.ThenBy(x => x.SheetNo, StringComparer.OrdinalIgnoreCase)
				.ThenBy(x => x.Column, StringComparer.OrdinalIgnoreCase)
				.ThenBy(x => x.Row, StringComparer.OrdinalIgnoreCase)
				.Select(target =>
				{
					var text = target.DocumentId == sourceDocumentId
					? @"\" + target.SheetNo
					: target.SheetNo;

					if (target.Column != null && target.Row != null)
						text = string.Format(CultureInfo.InvariantCulture, SheetPattern, text,
							string.Format(CultureInfo.InvariantCulture, CoordinatePattern, target.Column, target.Row));
					if (target.DocumentId != sourceDocumentId)
						text = string.Format(CultureInfo.InvariantCulture, DocumentPattern, target.DocumentName, text);
					return text;
				}).Distinct(StringComparer.Ordinal));
		}
	}
}
