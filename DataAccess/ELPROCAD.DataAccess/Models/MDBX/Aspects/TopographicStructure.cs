using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("TopographicStructures")]
	[OptimisticLocking(true)]
	internal sealed class TopographicStructure
		: AspectNode, ITopographicStructure
	{
	}
}
