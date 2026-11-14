using ELPROCAD.CAD.Data.Analysis.Import;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	// The verification harness compiles the CAD-independent rule source with these tests.
	public sealed class TerminalImportRulesTests
	{
		[Fact(DisplayName = "Terminal attributes are read independently of geometric definitions and wire connections")]
		public void Read_AllSlots()
		{
			var attributes = Enumerable.Range(1, 12).Select(x => ("UTTAG" + x, "T" + x));
			var values = TerminalImportRules.Read(attributes.Append(("BET1", "+AS1")));
			Assert.Equal(12, values.Count);
			Assert.Equal("T12", values.Last().Designation);
		}

		[Fact(DisplayName = "Duplicate tags on the same symbol are not hidden by a dictionary lookup")]
		public void Read_DuplicateTags()
		{
			Assert.Throws<InvalidOperationException>(() => TerminalImportRules.Read(
				new[] { ("UTTAG1", "A1"), ("uttag1", "A2") }));
		}

		[Fact(DisplayName = "An empty terminal designation is reported rather than silently discarded")]
		public void Read_EmptyDesignation()
		{
			Assert.Throws<InvalidOperationException>(() => TerminalImportRules.Read(new[] { ("UTTAG1", " ") }));
		}

		[Fact(DisplayName = "Different tags cannot represent the same terminal on one symbol")]
		public void Read_DuplicateValues()
		{
			Assert.Throws<InvalidOperationException>(() => TerminalImportRules.Read(
				new[] { ("UTTAG1", "A1"), ("UTTAG2", " a1 ") }));
		}

		[Fact(DisplayName = "The registered original is retained and the overlapping new presentation is rejected")]
		public void Check_OriginalWinsRegardlessOfOrder()
		{
			var id = Guid.NewGuid();
			var original = Symbol("original", "object", "A1", id);
			var copy = Symbol("copy", "object", "A1");
			var stored = new[] { Claim("object", id, "A1") };
			foreach (var order in new[] { new[] { original, copy }, new[] { copy, original } })
			{
				var error = Assert.Single(TerminalImportRules.Check(order, stored));
				Assert.Equal("copy", error.Key);
				Assert.Equal(id, error.ExistingLinkId);
			}
		}

		[Fact(DisplayName = "Two presentations with disjoint terminals on the same object are valid")]
		public void Check_DisjointPresentations()
		{
			var id = Guid.NewGuid();
			Assert.Empty(TerminalImportRules.Check(new[]
			{
				Symbol("original", "object", "A1", id), Symbol("copy", "object", "13")
			}, new[] { Claim("object", id, "A1") }));
		}

		[Fact(DisplayName = "The same terminal designation is valid on different engineering objects")]
		public void Check_DifferentObjects()
		{
			Assert.Empty(TerminalImportRules.Check(new[]
			{
				Symbol("first", "object1", "A1"), Symbol("second", "object2", "A1")
			}, Array.Empty<TerminalImportRules.Occupancy>()));
		}

		[Fact(DisplayName = "Ambiguous unregistered duplicates are both rejected without choosing an original")]
		public void Check_AmbiguousCopies()
		{
			var errors = TerminalImportRules.Check(new[]
			{
				Symbol("first", "object", "A1"), Symbol("second", "object", "A1")
			}, Array.Empty<TerminalImportRules.Occupancy>());
			Assert.Equal(2, errors.Count);
			Assert.All(errors, x => Assert.Null(x.ExistingLinkId));
		}

		private static TerminalImportRules.Presentation Symbol(string key, string objectKey, string value, Guid? linkId = null)
			=> new() { Key = key, ObjectKey = objectKey, RegisteredLinkId = linkId, Terminals = new[] { ("UTTAG1", value) } };
		private static TerminalImportRules.Occupancy Claim(string key, Guid linkId, string value)
			=> new() { ObjectKey = key, LinkId = linkId, Designation = value };
	}
}
