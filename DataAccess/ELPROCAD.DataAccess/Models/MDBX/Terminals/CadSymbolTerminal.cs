using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

namespace ELPROCAD.DataAccess.Models.MDBX.Terminals
{
	[Persistent("CadSymbolTerminals")]
	[OptimisticLocking(true)]
	internal sealed class CadSymbolTerminal : VersionedEntity, ICadSymbolTerminal
	{
		[Indexed]
		public Guid CadSymbolLinkId { get; set; }

		// Not globally unique: detached links may retain their previous terminal mappings.
		[Indexed]
		public Guid EngineeringObjectTerminalId { get; set; }

		/// <summary>The attribute slot name. No fixed naming pattern or slot count is imposed.</summary>
		[Size(255)]
		[Indexed(nameof(CadSymbolLinkId), Unique = true)]
		public string Tag { get; set; } = string.Empty;
	}
}
