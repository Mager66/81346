using ELPROCAD.CAD.Data.Interface;
using FluentValidation;

namespace ELPROCAD.CAD.Data.Validation.Classification
{
	public sealed class SubClassValidator : ClassificationValidator<ISubClass>
	{
		public SubClassValidator()
		{
			RuleFor(x => x.Abbrevation)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("Förkortning måste anges.")
				.Length(2, 3)
					.WithMessage("Förkortningen måste innehålla 2–3 tecken.");

			RuleFor(x=>x.MainClass)
				.NotNull()
				.WithMessage("Underklassen måste tillhöra en huvudklass.");			
		}
	}
}
