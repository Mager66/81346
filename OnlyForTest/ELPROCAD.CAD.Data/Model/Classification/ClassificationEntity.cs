using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Interface;

namespace ELPROCAD.CAD.Data.Model.Classification
{
	[NonPersistent]
	public abstract class ClassificationEntity : PocoEntity, IClassification
	{
	

		public string Tooltip
		{
			get => _tooltip;
			set => SetProperty(ref _tooltip, value);
		}
		private string _tooltip = "";

		public string Image
		{
			get => _image;
			set => SetProperty(ref _image, value);
		}
		private string _image = "";
	}
}
