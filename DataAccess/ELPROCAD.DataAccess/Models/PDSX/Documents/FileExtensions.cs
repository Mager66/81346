namespace ELPROCAD.DataAccess.Models.PDSX.Documents
{
	internal static class FileExtensions
	{
		/// <summary>
		/// Normalizes an extension to lowercase with a leading period.
		/// </summary>
		internal static string Normalize(string? extension)
		{
			if (string.IsNullOrWhiteSpace(extension))
				return "";

			var normalized = extension.Trim().ToLowerInvariant();

			return normalized.StartsWith(".", StringComparison.Ordinal)
				? normalized
				: "." + normalized;
		}
	}
}
