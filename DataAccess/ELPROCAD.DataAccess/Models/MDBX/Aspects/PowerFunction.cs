using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("PowerFunctions")]
	[OptimisticLocking(true)]
	internal sealed class PowerFunction : AspectNode, IPowerFunction
	{
	}
}
