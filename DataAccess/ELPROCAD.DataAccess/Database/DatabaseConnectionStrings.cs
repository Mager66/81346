using System.Data.Common;

namespace ELPROCAD.DataAccess.Database
{
	internal static class DatabaseConnectionStrings
	{
		internal static string Create(string databasePath)
		{
			if (string.IsNullOrWhiteSpace(databasePath))
			{
				throw new ArgumentException(
					"A database path is required.",
					nameof(databasePath));
			}

			return new DbConnectionStringBuilder
			{
				["XpoProvider"] = "VistaDB6",
				["Data Source"] = Path.GetFullPath(databasePath),
				["Open Mode"] = "MultiProcessReadWrite",
				["Pooling"] = false
			}.ConnectionString;
		}
	}
}
