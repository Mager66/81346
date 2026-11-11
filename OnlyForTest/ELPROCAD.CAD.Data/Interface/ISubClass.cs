using ELPROCAD.CAD.Data.Model.Classification;

namespace ELPROCAD.CAD.Data.Interface
{
	public interface ISubClass : IClassification
	{
		string Abbrevation { get; set; }
		MainClass MainClass {  get; }
	}
}
