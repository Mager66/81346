using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;
using ELPROCAD.CAD.Data.Model.Classification;
using System;

namespace ELPROCAD.CAD.Data.Provider
{
	public sealed class DatabaseInitializer
	{
		private readonly UnitOfWork _unitOfWork;

		public DatabaseInitializer(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public void Initialize()
		{
			_unitOfWork.UpdateSchema(
				typeof(Project),
				typeof(Document),
				typeof(Sheet),
				typeof(EngineeringObject),
				typeof(Location),
				typeof(Function),
				// Classification
				typeof(MainClass),
				typeof(SubClass));
		}
	}
}
