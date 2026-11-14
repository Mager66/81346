using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.References;

namespace ELPROCAD.DataAccess.Models.MDBX.References
{
	[Persistent("CadReferenceContactTerminals")]
	[OptimisticLocking(true)]
	internal sealed class CadReferenceContactTerminal :	VersionedEntity, ICadReferenceContactTerminal
	{
		[Indexed]
		public Guid ReferenceContactId { get; set; }

		/// <summary>
		/// Gets or sets the attribute slot name, with no fixed slot count.
		/// </summary>
		[Size(255)]
		[Indexed(nameof(ReferenceContactId), Unique = true)]
		public string Tag { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the displayed terminal designation
		/// without occupying that terminal.
		/// </summary>
		[Size(255)]
		public string Value { get; set; } = string.Empty;
	}
}