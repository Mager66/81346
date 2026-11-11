namespace ELPROCAD.CAD.Data.Interface
{
	public interface IClassification
	{
		public Guid Id { get; set; }

		string Tooltip {  get; set; }

		string Image {  get; set; }
	}
}
