using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model.Classification;
using ELPROCAD.CAD.Data.Validation;
using FluentValidation.Results;

namespace ELPROCAD.CAD.Data.CRUD
{
	public sealed partial class Create
	{
		public (MainClass? Value, ValidationResult Validation) New(MainClass entity)
		{
			return New<MainClass>(entity, CopyMainClass, ValidateMainClass);
		}

		private static void CopyMainClass(UnitOfWork uow, MainClass source, MainClass target)
		{
			target.Abbrevation = source.Abbrevation;
			target.Tooltip = source.Tooltip;
			target.Image = source.Image;
		}

		private static ValidationResult ValidateMainClass(UnitOfWork uow, MainClass entity)
		{ 
			var duplicate = new XPQuery<MainClass>(uow)
				.Any(x =>
				x.Abbrevation == entity.Abbrevation &&
				x.Id != entity.Id);

			if(duplicate)
				return ValidationResults.Failure(nameof(MainClass.Abbrevation), "Förkortningen används redan av en annan huvudklass.");

			return new ValidationResult();
		}
	}
}
