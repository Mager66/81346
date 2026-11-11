using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts;

namespace ELPROCAD.DataAccess.Models
{
	/// <summary>
	/// Provides identity without requiring a database session.
	/// </summary>
	[NonPersistent]
	internal abstract class PocoEntity : IEntity
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();
	}
}
