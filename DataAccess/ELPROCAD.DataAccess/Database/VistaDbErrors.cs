using VistaDB.Diagnostic;

namespace ELPROCAD.DataAccess.Database
{
	internal static class VistaDbErrors
	{
		/// <summary>
		/// Determines whether an exception contains a VistaDB duplicate-key error.
		/// Inspects nested exceptions because providers may wrap database errors.
		/// </summary>
		internal static bool IsDuplicateKey(Exception? exception)
		{
			if (exception == null)
				return false;

			if (exception is VistaDBException databaseException &&
				databaseException.ErrorId == Errors.dda_DuplicateKey)
			{
				return true;
			}

			if (exception is AggregateException aggregateException)
			{
				return aggregateException.InnerExceptions
					.Any(IsDuplicateKey);
			}

			return IsDuplicateKey(exception.InnerException);
		}
	}
}
