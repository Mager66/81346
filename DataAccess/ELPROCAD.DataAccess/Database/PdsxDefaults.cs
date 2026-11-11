using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models.PDSX.Documents;

namespace ELPROCAD.DataAccess.Database
{
	internal static class PdsxDefaults
	{
		/// <summary>
		/// Adds initial configuration to a newly created PDSX database.
		/// This method must not run when reopening an existing database.
		/// </summary>
		internal static void Initialize(UnitOfWork uow)
		{
			AddFileFormat(
				uow,
				"34219f47-a27a-41ed-bc65-baf2ee4e6bd1",
				"DWG",
				"DWG drawing",
				".dwg");

			AddFileFormat(
				uow,
				"e5a0dc5d-3786-4c84-81cb-3d8219bc37d6",
				"DXF",
				"Drawing exchange format",
				".dxf");

			AddFileFormat(
				uow,
				"10376912-446b-42aa-990a-53b1a629cb09",
				"PDF",
				"Portable Document Format",
				".pdf");

			AddFileFormat(
				uow,
				"bc854b45-6320-4f40-9a4e-b53b323191fb",
				"DOCX",
				"Office Open XML word processing document",
				".docx");

			AddFileFormat(
				uow,
				"c7c28d4e-b392-44a7-b9b2-760e4ba1f734",
				"XLSX",
				"Office Open XML spreadsheet",
				".xlsx");

			AddFileFormat(
				uow,
				"c51b47ca-27ca-4121-a987-94452f8c37eb",
				"TXT",
				"Plain text document",
				".txt");
		}

		private static void AddFileFormat(UnitOfWork uow, string id, string name, string description, string extension)
		{
			uow.Save(new FileFormat
			{
				Id = Guid.Parse(id),
				Name = name,
				Description = description,
				Extension = extension
			});
		}
	}
}
