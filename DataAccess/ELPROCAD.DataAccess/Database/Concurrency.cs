using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models;

namespace ELPROCAD.DataAccess.Database
{
	internal static class Concurrency
	{
		internal static int GetVersion(UnitOfWork unitOfWork, VersionedEntity entity)
		{
			var member = unitOfWork.GetClassInfo(entity.GetType())
				.GetMember(OptimisticLockingAttribute.DefaultFieldName);

			if (member == null)
			{
				throw new InvalidOperationException(
					$"Entity '{entity.GetType().Name}' requires optimistic locking.");
			}

			return Convert.ToInt32(member.GetValue(entity));
		}

		/// <summary>
		/// Captures the current version for entities that support version tracking.
		/// </summary>
		internal static void CaptureVersion(UnitOfWork unitOfWork, object entity)
		{
			if (entity is VersionedEntity versioned)
				versioned.LoadedVersion = GetVersion(unitOfWork, versioned);
		}
	}
}
