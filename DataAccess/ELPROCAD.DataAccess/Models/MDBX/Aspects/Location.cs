using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	[Persistent("Locations")]
	[OptimisticLocking(true)]
	internal sealed class Location : AspectNode, ILocation
	{
	}
}
