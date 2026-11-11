using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Tests.Models
{
	[Persistent("InitializationTestEntities")]
	public sealed class InitializationTestEntity
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		[Size(100)]
		public string Name { get; set; } = "";
	}
}
