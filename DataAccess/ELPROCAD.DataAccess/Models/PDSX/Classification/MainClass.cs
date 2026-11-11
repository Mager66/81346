using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Classification;

namespace ELPROCAD.DataAccess.Models.PDSX.Classification
{
	[Persistent("MainClassifications")]
	internal sealed class MainClass : PocoEntity, IMainClass
	{
		[Size(3)]
		[Indexed(Unique = true)]
		public string Abbrevation { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Tooltip { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Image { get; set; } = "";
	}
}
