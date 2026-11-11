using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models;

namespace ELPROCAD.DataAccess.Database
{
	internal static class DependencyVersions
	{
		/// <summary>
		/// Includes referenced records in the operation's optimistic concurrency checks.
		/// </summary>
		internal static void Touch(UnitOfWork unitOfWork, IEnumerable<VersionedEntity> entities)
		{
			foreach (var entity in entities)
			{
				entity.DependencyRevision =
					checked(entity.DependencyRevision + 1);

				unitOfWork.Save(entity);
			}
		}
	}
}
