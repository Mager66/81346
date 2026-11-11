using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

namespace ELPROCAD.DataAccess.Models.MDBX.Objects
{
	[Persistent("EngineeringObjects")]
	[OptimisticLocking(true)]
	internal sealed class EngineeringObject
		: VersionedEntity, IEngineeringObject
	{
		// References the project stored in PDSX.
		[Indexed]
		public Guid ProjectId { get; set; }

		// Objects may exist before they are represented on a sheet.
		[Indexed]
		public Guid? SheetId { get; set; }

		[Indexed]
		public Guid? PowerFunctionId { get; set; }

		[Indexed]
		public Guid? ControlFunctionId { get; set; }

		[Indexed]
		public Guid? TopographicStructureId { get; set; }

		[Indexed]
		public Guid? LocationId { get; set; }

		[Indexed]
		public Guid? FunctionId { get; set; }

		[Indexed]
		public Guid? ProductId { get; set; }
	}
}
