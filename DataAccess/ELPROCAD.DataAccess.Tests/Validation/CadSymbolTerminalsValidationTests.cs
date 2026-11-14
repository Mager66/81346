using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Validation.MDBX.Objects;
using ELPROCAD.DataAccess.Validation.MDBX.Terminals;
using FluentValidation.Results;
using System;
using System.Linq;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Validation
{
	public sealed class CadSymbolTerminalsValidationTests
	{
		private readonly Guid _objectId = Guid.NewGuid();
		private CadSymbolLink Symbol() => new CadSymbolLink { EngineeringObjectId = _objectId };
		private EngineeringObjectTerminal Terminal(string value) =>
			new EngineeringObjectTerminal { EngineeringObjectId = _objectId, Designation = value };
		private static CadSymbolTerminal Map(CadSymbolLink symbol, EngineeringObjectTerminal terminal, string tag) =>
			new CadSymbolTerminal { CadSymbolLinkId = symbol.Id, EngineeringObjectTerminalId = terminal.Id, Tag = tag };
		private static ValidationResult Check(CadSymbolLink symbol, CadSymbolTerminal[] proposed,
			EngineeringObjectTerminal[] terminals, CadSymbolLink[]? links = null, CadSymbolTerminal[]? stored = null) =>
			CadSymbolTerminalsValidation.Validate(symbol, proposed, terminals,
				links ?? new[] { symbol }, stored ?? Array.Empty<CadSymbolTerminal>());

		[Fact(DisplayName = "Unconnected terminals are registered without connection data")]
		public void UnconnectedTerminals_AreAccepted()
		{
			var symbol = Symbol();
			var a1 = Terminal("A1"); var a2 = Terminal("A2");
			Assert.True(Check(symbol, new[] { Map(symbol, a1, "UTTAG1"), Map(symbol, a2, "UTTAG2") }, new[] { a1, a2 }).IsValid);
		}

		[Fact(DisplayName = "Repeated attribute tags on one symbol are rejected even with different values")]
		public void DuplicateTag_IsRejected()
		{
			var symbol = Symbol(); var a1 = Terminal("A1"); var a2 = Terminal("A2");
			var result = Check(symbol, new[] { Map(symbol, a1, "UTTAG1"), Map(symbol, a2, "UTTAG1") }, new[] { a1, a2 });
			Assert.Equal("Tag", Assert.Single(result.Errors).PropertyName);
		}

		[Fact(DisplayName = "Different tags cannot represent the same terminal twice on one symbol")]
		public void DuplicateTerminal_IsRejected()
		{
			var symbol = Symbol(); var a1 = Terminal("A1");
			Assert.False(Check(symbol, new[] { Map(symbol, a1, "UTTAG1"), Map(symbol, a1, "UTTAG2") }, new[] { a1 }).IsValid);
		}

		[Fact(DisplayName = "Different presentations may reuse tags when they represent different terminals")]
		public void DifferentTerminals_ReuseTags()
		{
			var first = Symbol(); var second = Symbol();
			var a1 = Terminal("A1"); var contact = Terminal("13");
			Assert.True(Check(second, new[] { Map(second, contact, "UTTAG1") }, new[] { a1, contact },
				new[] { first, second }, new[] { Map(first, a1, "UTTAG1") }).IsValid);
		}

		[Fact(DisplayName = "Another active presentation cannot represent an already used terminal")]
		public void ActivePresentation_Conflicts()
		{
			var first = Symbol(); var second = Symbol(); var a1 = Terminal("A1");
			var result = Check(second, new[] { Map(second, a1, "UTTAG2") }, new[] { a1 },
				new[] { first, second }, new[] { Map(first, a1, "UTTAG1") });
			Assert.Contains("already represented", Assert.Single(result.Errors).ErrorMessage);
		}

		[Fact(DisplayName = "Reanalysis replaces a symbol's own mappings without self-conflicts")]
		public void Reanalysis_DoesNotConflictWithItself()
		{
			var symbol = Symbol(); var a1 = Terminal("A1");
			Assert.True(Check(symbol, new[] { Map(symbol, a1, "UTTAG1") }, new[] { a1 },
				stored: new[] { Map(symbol, a1, "UTTAG1") }).IsValid);
		}

		[Fact(DisplayName = "Detached presentations do not occupy terminals")]
		public void DetachedPresentation_DoesNotConflict()
		{
			var first = Symbol(); first.DetachedAtUtc = DateTime.UtcNow;
			var second = Symbol(); var a1 = Terminal("A1");
			Assert.True(Check(second, new[] { Map(second, a1, "UTTAG1") }, new[] { a1 },
				new[] { first, second }, new[] { Map(first, a1, "UTTAG1") }).IsValid);
		}

		[Fact(DisplayName = "Symbols are not limited to six terminal slots or legacy tag names")]
		public void MoreThanSixTerminals_AreAccepted()
		{
			var symbol = Symbol();
			var terminals = Enumerable.Range(1, 12).Select(n => Terminal("T" + n)).ToArray();
			var mappings = terminals.Select((t, i) => Map(symbol, t, "PIN_" + i)).ToArray();
			Assert.True(Check(symbol, mappings, terminals).IsValid);
		}

		[Fact(DisplayName = "A mapping cannot point to another engineering object's terminal")]
		public void WrongObject_IsRejected()
		{
			var symbol = Symbol(); var terminal = Terminal("A1"); terminal.EngineeringObjectId = Guid.NewGuid();
			Assert.False(Check(symbol, new[] { Map(symbol, terminal, "UTTAG1") }, new[] { terminal }).IsValid);
		}

		[Fact(DisplayName = "Missing terminal references are rejected")]
		public void MissingTerminal_IsRejected()
		{
			var symbol = Symbol(); var terminal = Terminal("A1");
			Assert.False(Check(symbol, new[] { Map(symbol, terminal, "UTTAG1") }, Array.Empty<EngineeringObjectTerminal>()).IsValid);
		}

		[Fact(DisplayName = "Terminal tag comparison ignores case and surrounding whitespace")]
		public void TagComparison_IsConsistent()
		{
			var symbol = Symbol(); var a1 = Terminal("A1"); var a2 = Terminal("A2");
			Assert.False(Check(symbol, new[] { Map(symbol, a1, "UTTAG1"), Map(symbol, a2, " uttag1 ") }, new[] { a1, a2 }).IsValid);
		}
	}
}

