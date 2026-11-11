using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Contracts.MDBX.Documents
{
	/// <summary>
	/// Describes a document belonging to a project in a separate PDSX database.
	/// </summary>
	public interface IDocument : IEntity
	{
		Guid ProjectId { get; }

		string Name { get; set; }

		string Path { get; set; }

		int? LoadedVersion { get; }

		// References PDSX configuration without a cross-database XPO association.
		[Indexed]
		public Guid FileFormatId { get; set; }
	}
}
