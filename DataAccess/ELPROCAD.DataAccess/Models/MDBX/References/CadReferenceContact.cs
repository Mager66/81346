using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.References;

namespace ELPROCAD.DataAccess.Models.MDBX.References
{
	[Persistent("CadReferenceContacts")]
	[OptimisticLocking(true)]
	internal sealed class CadReferenceContact : VersionedEntity, ICadReferenceContact
	{
		/// <summary>
		/// Identifies this presentation, including its object, document and sheet.
		/// </summary>
		[Indexed(Unique = true)]
		public Guid CadSymbolLinkId { get; set; }

		/// <summary>
		/// Identifies the contact-picture definition used by this presentation.
		/// </summary>
		[Size(255)]
		public string BlockName { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the BET1 attribute snapshot.
		/// </summary>
		[Size(SizeAttribute.Unlimited)]
		public string Designation { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the PLAC attribute snapshot.
		/// </summary>
		[Size(SizeAttribute.Unlimited)]
		public string Placement { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the last captured XREF text.
		/// This text is not the identity of the reference target.
		/// </summary>
		[Size(SizeAttribute.Unlimited)]
		public string ReferenceText { get; set; } = string.Empty;
	}
}