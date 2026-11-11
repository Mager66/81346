using VistaDB.Diagnostic;

namespace ELPROCAD.CAD.Data.Validation.VistDb
{
	public static class VistaDbErrors
	{
		public static bool IsDuplicateKey(Exception exception)
		{
			if(exception is AggregateException aggregate)
			{
				foreach(var inner in aggregate.InnerExceptions) 
					if(IsDuplicateKey(inner))
						return true;
			}

			if(exception is VistaDBException vistaDBException && vistaDBException.ErrorId == Errors.dda_DuplicateKey)
				return true;

			return exception.InnerException != null && IsDuplicateKey(exception.InnerException);
		}
	}
}
