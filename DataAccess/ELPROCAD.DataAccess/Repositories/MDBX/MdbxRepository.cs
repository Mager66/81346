using ELPROCAD.DataAccess.Repositories.MDBX.Aspects;
using ELPROCAD.DataAccess.Repositories.MDBX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX.Objects;

namespace ELPROCAD.DataAccess.Repositories.MDBX
{
	internal sealed class MdbxRepository : IMdbxRepository
	{
		internal MdbxRepository(
			IDocumentRepository document, 
			ISheetRepository sheet, 
			ICadDocumentSpaceRepository spaceRepository, 
			IDocumentPropertiesRepository documentProperties, 
			ICadDocumentCustomPropertyRepository cadDocumentCustom, 
			ILocationRepository location,
			IEngineeringObjectRepository engineeringObject,
			ICadSymbolLinkRepository cadSymbolLink)
		{
			Document = document ?? throw new ArgumentNullException(nameof(document));
			Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
			CadDocumentSpace = spaceRepository ?? throw new ArgumentNullException(nameof(spaceRepository));
			DocumentProperties = documentProperties ?? throw new ArgumentNullException(nameof(documentProperties));
			CadDocumentCustomProperty = cadDocumentCustom ?? throw new ArgumentNullException(nameof(cadDocumentCustom));
			Location = location ?? throw new ArgumentNullException(nameof(location));
			EngineeringObject = engineeringObject ?? throw new ArgumentNullException(nameof(engineeringObject));
			CadSymbolLink = cadSymbolLink ?? throw new ArgumentNullException(nameof(cadSymbolLink));
		}

		public IDocumentRepository Document { get; }

		public ISheetRepository Sheet { get; }

		public ICadDocumentSpaceRepository CadDocumentSpace { get; }

		public IDocumentPropertiesRepository DocumentProperties { get; }

		public ICadDocumentCustomPropertyRepository CadDocumentCustomProperty { get; }

		public ILocationRepository Location { get; }

		public IEngineeringObjectRepository EngineeringObject { get; }

		public ICadSymbolLinkRepository CadSymbolLink { get; }
	}

}
