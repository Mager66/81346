using DevExpress.Xpo;

namespace ELPROCAD.CAD.Data.Model.Tests
{
	[Persistent("PocoTestChildren")]
	public sealed class PocoTestChild
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		[Size(50)]
		public string Name { get; set; } = "";

		public PocoTest Parent { get; set; } = null!;
	}
}

