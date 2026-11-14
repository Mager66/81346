using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{
		private static List<IGrouping<Guid, ImportBlock>> GroupLinkedMainSymbols(ImportContext context)
		{
			return context.Items
				.Where(item => !context.FailedBlocks.Contains(item.Block.ObjectId) &&
					!item.IsMember && item.LinkedObjectId.HasValue)
				.GroupBy(item => item.SourceLocationId!.Value)
				.OrderBy(group => group.First().OriginalLevels.Length)
				.ThenBy(group => group.Key)
				.ToList();

		}

		private static void ValidateMainSymbolGroups(List<IGrouping<Guid, ImportBlock>> groups, ImportContext context)
		{
			// Reject different requested designations within one registered object group.
			foreach (var group in groups)
			{
				var first = group.First();

				var hasDifferentDesignations = group.Any(item =>
						!SamePath(
							item.RequestedLevels,
							first.RequestedLevels));

				if (!hasDifferentDesignations)
					continue;

				foreach (var item in group)
				{
					context.Fail(
						item.Block.ObjectId,
							"Main symbols share the same engineering object identifier " +
							"but have different designations. A copied symbol may require " +
							"a new engineering object. No change was applied to this group.");
				}

			}

			// Reject competing explicit requests at the same depth.
			var requests = groups
				.Where(group => !group.Any(item =>
					context.FailedBlocks.Contains(item.Block.ObjectId)))
				.Select(group => new
				{
					Group = group,
					Request = group.FirstOrDefault(item =>
						!SamePath(item.RequestedLevels, item.OriginalLevels))
				})
				.Where(item => item.Request != null)
				.ToList();

			// Compare requests within the same parent path.
			foreach (var parentGroup in requests.GroupBy(item => string.Join(
					LevelSeparator.ToString(),
					item.Request!.RequestedLevels.Take(item.Request.RequestedLevels.Length - 1)), StringComparer.OrdinalIgnoreCase))
			{
				foreach (var collision in parentGroup.GroupBy(
					item => item.Request!.RequestedLevels.Last(),
					StringComparer.OrdinalIgnoreCase)
					.Where(group => group.Count() > 1))
				{
					foreach (var request in collision)
					{
						foreach (var item in request.Group)
						{
							context.Fail(
								item.Block.ObjectId,
								"Different objects request the same designation under the same parent.");
						}
					}
				}
			}

		}
		private void ApplyLinkedMainSymbols(List<IGrouping<Guid, ImportBlock>> groups, ImportContext context)
		{
			// Pass 2: apply explicit changes, processing ancestors first.
			foreach (var group in groups)
			{
				if (group.Any(item => context.FailedBlocks.Contains(item.Block.ObjectId)))
					continue;

				try
				{
					var changed = group.FirstOrDefault(item => !SamePath(item.RequestedLevels, item.OriginalLevels));

					if (changed != null)
					{
						var targetLevels = RebaseParentPath(
							changed.RequestedLevels,
							context.OriginalPaths);

						var update = UpdateLinkedLocation(
							changed.LinkedObjectId!.Value,
							context.ProjectId,
							targetLevels);

						if (!update.Validation.IsValid)
						{
							var message = string.Join(
								Environment.NewLine,
								update.Validation.Errors
									.Select(error => error.ErrorMessage)
									.Distinct());

							foreach (var item in group)
								context.Fail(item.Block.ObjectId, message);

							continue;
						}
					}

					foreach (var item in group)
					{
						context.ResolvedObjects[item.Block.ObjectId] =
							item.LinkedObjectId!.Value;
					}
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					foreach (var item in group)
						context.Fail(item.Block.ObjectId, exception.Message);
				}
			}

		}
		private void ImportUnlinkedMainSymbols(ImportContext context)
		{
			// Import previously unlinked main symbols after existing identities.
			foreach (var item in context.Items
				.Where(item => !item.IsMember && !item.LinkedObjectId.HasValue)
				.OrderBy(item => item.RequestedLevels.Length)
				.ThenBy(item => item.Block.ObjectId.Handle.Value))
			{
				if (context.FailedBlocks.Contains(item.Block.ObjectId))
					continue;
				try
				{
					var levels = RebaseParentPath(
						item.RequestedLevels,
						context.OriginalPaths);

					var location = ResolveLocationPath(
						context.ProjectId,
						levels,
						createMissing: true);

					// Reuse the object for this designation, or create it when absent.
					var entity = GetOrCreateObject(context.ProjectId, location.Id);

					item.SourceLocationId = location.Id;
					context.ResolvedObjects[item.Block.ObjectId] = entity.Id;
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					context.Fail(item.Block.ObjectId, exception.Message);
				}
			}

		}
	}
}

