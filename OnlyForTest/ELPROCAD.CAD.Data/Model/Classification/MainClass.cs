using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Interface;
using ELPROCAD.CAD.Data.Validation.Classification;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.Model.Classification
{
	[Persistent("MainClassifications")]
	[OptimisticLocking(true)]
	public sealed class MainClass : ClassificationEntity, IMainClass
	{
		private static readonly MainClassValidator _validator = new();

		[Size(3)]
		[Indexed(Unique = true)]
		public string Abbrevation
		{
			get => _abbrevation;
			set => SetProperty(ref _abbrevation, value);
		}
		private string _abbrevation = "";

		[NonPersistent]
		public List<SubClass> SubClasses { get; } = new();

		protected override ValidationResult ValidateCore() => _validator.Validate(this);

		///// <summary>
		///// The collection of <see cref="SubClass"/>
		///// </summary>
		//[Association("MainClass-SubClass"), Aggregated]
		//public XPCollection<SubClass> SubClasses => GetCollection<SubClass>(nameof(SubClass));
	}
}
