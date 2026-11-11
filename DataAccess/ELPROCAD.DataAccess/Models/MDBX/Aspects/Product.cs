using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("Products")]
	[OptimisticLocking(true)]
	internal sealed class Product : AspectNode, IProduct
	{
	}
}
