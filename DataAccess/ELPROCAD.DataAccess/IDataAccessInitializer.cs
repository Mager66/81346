using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;

namespace ELPROCAD.DataAccess
{
	public interface IDataAccessInitializer
	{
		/// <summary>
		/// Initializes a configuration database and returns its repositories.
		/// Existing data is preserved.
		/// </summary>
		IPdsxRepository InitializePdsx(string databasePath);

		/// <summary>
		/// Initializes a document database and returns its repositories.
		/// Existing data is preserved.
		/// </summary>
		IMdbxRepository InitializeMdbx(string databasePath);
	}
}
