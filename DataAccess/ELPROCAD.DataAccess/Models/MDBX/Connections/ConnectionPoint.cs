using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Models.MDBX.Connections
{
	internal enum ConnectionPointKind
	{
		ApparatusTerminal = 1,
		TerminalSide = 2,
		SignalReference = 3,
		Potential = 4,
		CableEnd = 5,
		Junction = 6
	}

	/// <summary>
	/// A sheet-local connection point. Different terminal sides have different IDs.
	/// Matching names, terminal IDs or kinds never establish electrical continuity.
	/// </summary>
	[Persistent("ConnectionPoints")]
	[OptimisticLocking(true)]
	internal sealed class ConnectionPoint : VersionedEntity
	{
		[Indexed]
		public Guid SheetId { get; set; }

		public ConnectionPointKind Kind { get; set; }

		[Indexed]
		public Guid? CadSymbolLinkId { get; set; }

		[Indexed]
		public Guid? EngineeringObjectTerminalId { get; set; }

		[Indexed]
		public Guid? ElectricalPotentialId { get; set; }

		// Describes the point or side; this is not its identity or a matching rule.
		[Size(255)]
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// Source slot for analysis-owned terminal points; empty for other points.
		/// </summary>
		[Size(255)]
		public string AnalysisTag { get; set; } = string.Empty;
	}
}
