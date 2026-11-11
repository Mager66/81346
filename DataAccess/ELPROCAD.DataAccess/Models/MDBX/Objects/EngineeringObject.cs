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
		/// <summary>
		/// Retains the previous sheet reference until existing symbol links
		/// have been migrated. New operations must not populate this property.
		/// </summary>
		[Persistent("SheetId")]
		public Guid? LegacySheetId { get; set; }

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

		/// <summary>
		/// Stores the deletion marker without removing the object or its relations.
		/// </summary>
		public DateTime? DeletedAtUtc { get; set; }

		[NonPersistent]
		public bool IsMarkedForDeletion => DeletedAtUtc.HasValue;
	}
}
