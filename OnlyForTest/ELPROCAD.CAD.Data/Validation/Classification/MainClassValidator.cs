using ELPROCAD.CAD.Data.Interface;
using FluentValidation;

namespace ELPROCAD.CAD.Data.Validation.Classification
{
	public sealed class MainClassValidator : ClassificationValidator<IMainClass>
	{
		public MainClassValidator()
		{
			RuleFor(x => x.Abbrevation)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithMessage("Förkortning måste anges.")
				.Length(2, 3)
					.WithMessage("Förkortningen måste innehålla 2–3 tecken.");

			RuleSet("Delete", () =>
			{
				RuleFor(x => x.SubClasses)
					.Must(x => x.Count == 0)
						.WithMessage("Huvudklassen kan inte raderas eftersom den har underklasser.");
			});
		}
	}
}
