using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Classification;

namespace ELPROCAD.DataAccess.Models.PDSX.Classification
{
	[Persistent("SubClassifications")]
	internal sealed class SubClass : PocoEntity, ISubClass
	{
		// The abbreviation must be unique within its main classification.
		[Size(3)]
		[Indexed(nameof(MainClass), Unique = true)]
		public string Abbrevation { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Tooltip { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Image { get; set; } = "";

		[Persistent("MainClassGuid")]
		public MainClass MainClass { get; set; } = null!;

		// Expose the parent through its read-only contract.
		IMainClass ISubClass.MainClass => MainClass;
	}
}
