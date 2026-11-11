using DevExpress.Xpo;
using ELPROCAD.API.Model;
using System;

namespace ELPROCAD.API.Provider
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
				typeof(Function));
		}
	}

}