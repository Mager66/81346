using ELPROCAD.DataAccess.Repositories.MDBX.Aspects;
using ELPROCAD.DataAccess.Repositories.MDBX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX.Objects;

namespace ELPROCAD.DataAccess.Repositories.MDBX
{
	/// <summary>
	/// Provides access to data stored in an MDBX database.
	/// </summary>
	public interface IMdbxRepository
	{
		IDocumentRepository Document { get; }

		ISheetRepository Sheet { get; }

		ICadDocumentSpaceRepository CadDocumentSpace { get; }

		IDocumentPropertiesRepository DocumentProperties { get; }

		ICadDocumentCustomPropertyRepository CadDocumentCustomProperty { get; }

		ILocationRepository Location { get; }

		IEngineeringObjectRepository EngineeringObject { get; }

		ICadSymbolLinkRepository CadSymbolLink { get; }
	}
}
