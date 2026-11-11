using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using ELPROCAD.DataAccess.Models.PDSX.Classification;
using ELPROCAD.DataAccess.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Validation
{
	public sealed class ClassificationValidationTests
	{
		private readonly IValidationService _validation = new ValidationService();

		[Theory(DisplayName = "Invalid abbreviations produce one property-specific validation error")]
		[InlineData("")]
		[InlineData(" ")]
		[InlineData("A")]
		[InlineData("ABCD")]
		public void InvalidAbbreviation_ReturnsSingleError(string abbrevation)
		{
			var main = new MainClass
			{
				Abbrevation = abbrevation,
				Tooltip = "Main component"
			};

			var result = _validation.Validate(main);

			Assert.False(result.IsValid);

			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(IMainClass.Abbrevation), error.PropertyName);
		}

		[Theory(DisplayName =
			"Two- and three-character abbreviations are accepted")]
		[InlineData("MC")]
		[InlineData("MCB")]
		public void ValidAbbreviation_IsAccepted(string abbrevation)
		{
			var main = new MainClass
			{
				Abbrevation = abbrevation,
				Tooltip = "Main component"
			};

			Assert.True(_validation.Validate(main).IsValid);
		}

		[Theory(DisplayName = "An empty or whitespace tooltip is rejected")]
		[InlineData("")]
		[InlineData("   ")]
		public void MissingTooltip_IsRejected(string tooltip)
		{
			var main = new MainClass
			{
				Abbrevation = "MC",
				Tooltip = tooltip
			};

			var result = _validation.Validate(main);
			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(IMainClass.Tooltip), error.PropertyName);
			Assert.Equal("A tooltip is required.", error.ErrorMessage);
		}

		[Fact(DisplayName =	"A sub-classification requires a main classification")]
		public void SubClass_WithoutParent_IsRejected()
		{
			var sub = new SubClass
			{
				Abbrevation = "AA",
				Tooltip = "Test sub-classification"
			};

			var result = _validation.Validate(sub);
			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(ISubClass.MainClass), error.PropertyName);
			Assert.Equal(
				"A main classification is required.",
				error.ErrorMessage);
		}

		[Fact(DisplayName =	"Sub-classifications use the common classification rules")]
		public void SubClass_InvalidCommonValues_IsRejected()
		{
			var sub = new SubClass
			{
				Abbrevation = "",
				Tooltip = "",
				MainClass = new MainClass
				{
					Abbrevation = "MC",
					Tooltip = "Main component"
				}
			};

			var result = _validation.Validate(sub);

			Assert.Equal(2, result.Errors.Count);

			Assert.Contains(result.Errors,
				error => error.PropertyName == nameof(ISubClass.Abbrevation));

			Assert.Contains(result.Errors,
				error => error.PropertyName == nameof(ISubClass.Tooltip));
		}

		[Fact(DisplayName =	"Corrected values pass validation without retaining previous errors")]
		public void CorrectedEntity_HasNoPreviousErrors()
		{
			var main = new MainClass();

			Assert.False(_validation.Validate(main).IsValid);

			main.Abbrevation = "MC";
			main.Tooltip = "Main component";

			var result = _validation.Validate(main);

			Assert.True(result.IsValid);
			Assert.Empty(result.Errors);
		}

		[Fact(DisplayName =	"Validation can be resolved and used without initializing a database")]
		public void DependencyInjection_ProvidesIndependentValidation()
		{
			var services = new ServiceCollection();
			services.AddElprocadDataAccess();

			using var provider = services.BuildServiceProvider();

			var validation = provider
				.GetRequiredService<IValidationService>();

			var sub = new SubClass
			{
				Abbrevation = "AA",
				Tooltip = "Test sub-classification",
				MainClass = new MainClass
				{
					Abbrevation = "MC",
					Tooltip = "Main component"
				}
			};

			Assert.True(validation.Validate(sub).IsValid);
		}
	}
}
