using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model.Classification;

namespace ELPROCAD.CAD.Data.Interface
{
	public interface IMainClass : IClassification
	{
		string Abbrevation { get; set; }
		List<SubClass>  SubClasses { get; }
	}
}
