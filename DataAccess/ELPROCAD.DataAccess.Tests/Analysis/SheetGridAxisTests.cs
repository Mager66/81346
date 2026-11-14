using System;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Analysis
{
	public sealed class SheetGridAxisTests
	{
		[Fact(DisplayName =	"Two markers define equal intervals with numeric labels")]
		public void NumericLabels_AreResolved()
		{
			var axis = new SheetGridAxis(
				10, 60, new[] { "1", "2", "3" });

			Assert.Equal("1", axis.GetLabel(35));
			Assert.Equal("2", axis.GetLabel(85));
			Assert.Equal("3", axis.GetLabel(135));
		}

		[Fact(DisplayName =	"Rows can increase downwards with custom labels")]
		public void DescendingAxis_UsesConfiguredLabels()
		{
			var axis = new SheetGridAxis(
				300, 250, new[] { "AA", "AC", "Zone 3" });

			Assert.Equal("AA", axis.GetLabel(275));
			Assert.Equal("AC", axis.GetLabel(225));
			Assert.Equal("Zone 3", axis.GetLabel(175));

			Assert.Null(axis.GetLabel(301));
			Assert.Null(axis.GetLabel(149));
		}

		[Fact(DisplayName =	"Internal boundaries select the next interval and the outer end selects the last")]
		public void Boundaries_AreDeterministic()
		{
			foreach (var direction in new[] { -1, 1 })
			{
				var axis = new SheetGridAxis(
					0, direction * 50, new[] { "A", "B" });

				Assert.Equal("A", axis.GetLabel(0));
				Assert.Equal("B", axis.GetLabel(direction * 50));
				Assert.Equal("B", axis.GetLabel(direction * 100));

				Assert.Null(axis.GetLabel(direction * 101));
				Assert.Null(axis.GetLabel(-direction));
			}
		}

		[Fact(DisplayName =	"Axis labels cannot change through the input collection")]
		public void Labels_AreCopied()
		{
			var labels = new[] { "01", "02" };
			var axis = new SheetGridAxis(0, 10, labels);

			labels[0] = "Changed";

			Assert.Equal("01", axis.GetLabel(5));
		}

		[Fact(DisplayName =	"Invalid geometry and ambiguous labels are rejected")]
		public void InvalidConfiguration_IsRejected()
		{
			Assert.Throws<ArgumentException>(() =>
				new SheetGridAxis(0, 0, new[] { "1" }));

			Assert.Throws<ArgumentException>(() =>
				new SheetGridAxis(0, double.NaN, new[] { "1" }));

			Assert.Throws<ArgumentException>(() =>
				new SheetGridAxis(0, 1, Array.Empty<string>()));

			Assert.Throws<ArgumentException>(() =>
				new SheetGridAxis(0, 1, new[] { "A", "a" }));

			Assert.Throws<ArgumentException>(() =>
				new SheetGridAxis(0, 1, new[] { " " }));

			Assert.Throws<ArgumentNullException>(() =>
				new SheetGridAxis(0, 1, null!));

			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new SheetGridAxis(0, 1, new[] { "A" })
					.GetLabel(double.NaN));
		}

		[Fact(DisplayName =	"Ruler labels support numeric, alphabetic and explicit sequences")]
		public void ConfiguredLabels_AreResolved()
		{
			var settings = new Dictionary<string, string>
			{
				["MODE"] = "NUMBER",
				["START"] = "3",
				["COUNT"] = "3"
			};

			Assert.Equal(
				new[] { "3", "4", "5" },
				SheetGridLabels.Read(settings));

			settings["MODE"] = "LETTER";
			settings["START"] = "26";

			Assert.Equal(
				new[] { "Z", "AA", "AB" },
				SheetGridLabels.Read(settings));

			settings["MODE"] = "LIST";
			settings["LABELS"] = "01 | 03 | Zone A";

			Assert.Equal(
				new[] { "01", "03", "Zone A" },
				SheetGridLabels.Read(settings));
		}

		[Fact(DisplayName =	"Ruler configuration rejects empty slots, duplicate labels and invalid counts")]
		public void InvalidLabels_AreRejected()
		{
			var settings = new Dictionary<string, string>
			{
				["MODE"] = "LIST"
			};

			foreach (var labels in new[] { "", "A||C", "A|a" })
			{
				settings["LABELS"] = labels;

				Assert.Throws<ArgumentException>(() =>
					SheetGridLabels.Read(settings));
			}

			settings["MODE"] = "NUMBER";

			foreach (var count in new[] { "", "0", "-1", "10001", "abc" })
			{
				settings["COUNT"] = count;

				Assert.Throws<ArgumentException>(() =>
					SheetGridLabels.Read(settings));
			}

			settings["COUNT"] = "2";
			settings["START"] = int.MaxValue.ToString();

			Assert.Throws<ArgumentException>(() =>
				SheetGridLabels.Read(settings));
		}

		[Fact(DisplayName =	"Ruler numbering starts at one when START is empty")]
		public void DefaultStart_IsOne()
		{
			var settings = new Dictionary<string, string>
			{
				["MODE"] = "LETTER",
				["COUNT"] = "2"
			};

			Assert.Equal(
				new[] { "A", "B" },
				SheetGridLabels.Read(settings));
		}
	}
}