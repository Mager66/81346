using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace ELPROCAD.CAD.Data.Provider
{
	public static class XpoDataLayerFactory
	{
		public static IDataLayer Create(string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentException("En anslutningssträng måste anges.", nameof(connectionString));

			return XpoDefault.GetDataLayer(connectionString, AutoCreateOption.DatabaseAndSchema);
		}

	}
}
