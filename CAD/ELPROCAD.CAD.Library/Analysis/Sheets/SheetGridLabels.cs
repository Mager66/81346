using System.Globalization;

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	internal static class SheetGridLabels
	{
		// START is a one-based ordinal: 1 means A in LETTER mode.
		internal static string[] Read(IReadOnlyDictionary<string, string> attributes)
		{
			string Value(string tag) =>
				attributes.TryGetValue(tag, out var value)
					? value.Trim()
					: string.Empty;

			var mode = Value("MODE").ToUpperInvariant();

			if (mode == "LIST")
			{
				var labels = Value("LABELS")
					.Split('|')
					.Select(value => value.Trim())
					.ToArray();

				// Reuse the coordinate resolver's label validation.
				_ = new SheetGridAxis(0, 1, labels);

				return labels;
			}

			if (mode != "NUMBER" && mode != "LETTER")
				throw new ArgumentException(
					"MODE must be NUMBER, LETTER or LIST.");

			if (!int.TryParse(
					Value("COUNT"),
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out var count) ||
				count < 1 || count > 10000)
			{
				throw new ArgumentException(
					"COUNT must be between 1 and 10000.");
			}

			var startText = Value("START");
			var start = 1;

			if (startText.Length != 0 &&
				(!int.TryParse(
					startText,
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out start) || start < 1))
			{
				throw new ArgumentException(
					"START must be a positive integer.");
			}

			if ((long)start + count - 1 > int.MaxValue)
				throw new ArgumentException(
					"The final label exceeds the supported range.");

			return Enumerable.Range(start, count)
				.Select(number => mode == "NUMBER"
					? number.ToString(CultureInfo.InvariantCulture)
					: Letters(number))
				.ToArray();
		}

		private static string Letters(int number)
		{
			var result = string.Empty;

			while (number > 0)
			{
				number--;
				result = (char)('A' + number % 26) + result;
				number /= 26;
			}

			return result;
		}
	}
}