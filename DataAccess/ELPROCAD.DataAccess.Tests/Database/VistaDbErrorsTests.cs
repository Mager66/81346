using ELPROCAD.DataAccess.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class VistaDbErrorsTests
	{
		[Fact(DisplayName =	"A missing exception is not classified as a duplicate-key error")]
		public void IsDuplicateKey_Null_ReturnsFalse()
		{
			Assert.False(VistaDbErrors.IsDuplicateKey(null));
		}

		[Fact(DisplayName =	"Exception messages do not determine duplicate-key classification")]
		public void IsDuplicateKey_MatchingMessage_ReturnsFalse()
		{
			var exception = new InvalidOperationException(
				"Duplicate key found in index.");

			Assert.False(VistaDbErrors.IsDuplicateKey(exception));
		}

		[Fact(DisplayName =	"Unrelated nested exceptions are not classified as duplicate-key errors")]
		public void IsDuplicateKey_UnrelatedNestedExceptions_ReturnsFalse()
		{
			var exception = new InvalidOperationException(
				"The operation failed.",
				new IOException("The file could not be read."));

			Assert.False(VistaDbErrors.IsDuplicateKey(exception));
		}

		[Fact(DisplayName =	"An aggregate containing only unrelated failures is not a duplicate-key error")]
		public void IsDuplicateKey_UnrelatedAggregate_ReturnsFalse()
		{
			var exception = new AggregateException(
				new IOException("The file could not be read."),
				new InvalidOperationException("The operation failed."));

			Assert.False(VistaDbErrors.IsDuplicateKey(exception));
		}
	}
}
