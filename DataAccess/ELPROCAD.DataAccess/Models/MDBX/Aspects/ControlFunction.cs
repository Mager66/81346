using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("ControlFunctions")]
	[OptimisticLocking(true)]
	internal sealed class ControlFunction : AspectNode, IControlFunction
	{
	}
}
