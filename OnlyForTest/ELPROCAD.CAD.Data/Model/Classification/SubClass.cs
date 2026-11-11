using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Interface;
using ELPROCAD.CAD.Data.Validation.Classification;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.Model.Classification
{
	[Persistent("SubClassifications")]
	[OptimisticLocking(true)]
	public sealed class SubClass : ClassificationEntity, ISubClass
	{
		[Size(3)]
		[Indexed(nameof(MainClass), Unique = true)]
		public string Abbrevation
		{
			get => _abbrevation;
			set => SetProperty(ref _abbrevation, value);
		}
		private string _abbrevation = "";

		private static readonly SubClassValidator _validator = new();		

		[Persistent("MainClassGuid")]
		public MainClass MainClass
		{
			get => _mainClass;
			set => SetProperty(ref _mainClass, value);
		}
		private MainClass _mainClass = null;

		//[Association("MainClass-SubClass")]
		//[Persistent("MainClassGuid")]
		//public MainClass MainClass
		//{
		//	get => _mainClass;
		//	set => SetPropertyValue(nameof(MainClass), ref _mainClass, value);
		//}
		//private MainClass _mainClass = null;

		protected override ValidationResult ValidateCore() => _validator.Validate(this);
	}
}
