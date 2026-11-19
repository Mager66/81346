using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

namespace ELPROCAD.DataAccess.Models.MDBX.Terminals
{
	[Persistent("EngineeringObjectTerminals")]
	[OptimisticLocking(true)]
	internal sealed class EngineeringObjectTerminal : VersionedEntity, IEngineeringObjectTerminal
	{
		private int? _configuredMaxWireConnections;

		[Indexed]
		public Guid EngineeringObjectId { get; set; }

		/// <summary>The actual terminal designation, for example A1, rather than UTTAG1.</summary>
		[Size(255)]
		[Indexed(nameof(EngineeringObjectId), Unique = true)]
		public string Designation { get; set; } = string.Empty;

		/// <summary>
		/// Null uses the default of two conductors, including existing records.
		/// </summary>
		public int? ConfiguredMaxWireConnections
		{
			get => _configuredMaxWireConnections;
			set
			{
				if (value.HasValue && value.Value < 1)
				{
					throw new ArgumentOutOfRangeException(
						nameof(value),
						"The conductor limit must be positive.");
				}

				_configuredMaxWireConnections = value;
			}
		}

		[NonPersistent]
		public int MaxWireConnections => ConfiguredMaxWireConnections ?? 2;

		// TODO: Enforce the limit across physical conductor ends for this terminal,
		// not across network members, CAD presentations or terminal-side points.
	}
}
