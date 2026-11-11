using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;

namespace ELPROCAD.CAD.Data.Validation.Conflict
{
	internal class Concurrency
	{
		public static int? GetVersion(UnitOfWork uow, PocoEntity entity)
		{
			var member = uow.GetClassInfo(entity.GetType())
				.GetMember("OptimisticLockField");

			return member == null
				? null
				: Convert.ToInt32(member.GetValue(entity));
		}

		public static void CaptureVersion(UnitOfWork uow, PocoEntity entity) => entity.LoadedVersion = GetVersion(uow, entity);
	}
}
