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
		private void ResolveMemberSymbols(ImportContext context)
		{
			// Member symbols refer to an object; they do not rename that object.
			foreach (var item in context.Items.Where(item => item.IsMember))
			{
				if (context.FailedBlocks.Contains(item.Block.ObjectId))
					continue;
				try
				{
					if (item.LinkedObjectId.HasValue)
					{
						var currentLocations = _mdbx.Location
							.GetByProjectId(context.ProjectId)
							.ToDictionary(location => location.Id);

						var currentPath = ReadPath(
							item.SourceLocationId!.Value,
							currentLocations);

						var explicitlyChanged = !SamePath(
							item.RequestedLevels,
							item.OriginalLevels);

						if (explicitlyChanged &&
							!SamePath(
								RebaseParentPath(item.RequestedLevels, context.OriginalPaths),
								currentPath))
						{
							throw new InvalidOperationException(
								"The member symbol's designation conflicts with its linked object.");
						}

						context.ResolvedObjects[item.Block.ObjectId] =
							item.LinkedObjectId.Value;
					}
					else
					{
						var levels = RebaseParentPath(
							item.RequestedLevels,
							context.OriginalPaths);

						var location = ResolveLocationPath(
							context.ProjectId,
							levels,
							createMissing: false);

						var entity = GetContainingObject(location.Id);

						item.SourceLocationId = location.Id;
						context.ResolvedObjects[item.Block.ObjectId] = entity.Id;
					}
				}
				catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
				{
					context.Fail(item.Block.ObjectId, exception.Message);
				}
			}

		}
	}
}

