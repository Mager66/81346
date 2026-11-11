using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("Functions")]
	[OptimisticLocking(true)]
	internal sealed class Function : AspectNode, IFunction
	{
	}
}
