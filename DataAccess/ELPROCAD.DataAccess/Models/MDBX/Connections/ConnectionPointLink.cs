using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.DataAccess.Models.MDBX.Connections
{
	internal enum ConnectionPointLinkKind
	{
		SignalContinuation = 1,
		TerminalBridge = 2,
		CableCore = 3,
		PotentialConnection = 4
	}

	/// <summary>
	/// Explicit continuity between two points, possibly on different sheets/documents.
	/// It preserves the local nets rather than merging or renumbering them in storage.
	/// </summary>
	[Persistent("ConnectionPointLinks")]
	[OptimisticLocking(true)]
	internal sealed class ConnectionPointLink : VersionedEntity
	{
		[Indexed(nameof(SecondPointId), Unique = true)]
		public Guid FirstPointId { get; set; }

		[Indexed]
		public Guid SecondPointId { get; set; }

		public ConnectionPointLinkKind Kind { get; set; }

		/// <summary>Stores an unordered pair in canonical order. It does not imply signal direction.</summary>
		internal void SetEndpoints(Guid first, Guid second)
		{
			FirstPointId = first.CompareTo(second) < 0 ? first : second;
			SecondPointId = first.CompareTo(second) < 0 ? second : first;
		}
	}
}
