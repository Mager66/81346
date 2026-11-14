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
		private static void ValidateImportInput(AcAp.Document doc, SheetAnalysisData result)
		{
			if (doc == null) throw new ArgumentNullException(nameof(doc));
			if (result == null) throw new ArgumentNullException(nameof(result));
			if (!Path.IsPathRooted(doc.Name))
				throw new InvalidOperationException("Save the drawing before importing.");
			if (string.IsNullOrWhiteSpace(result.TitleBlock.SheetLayer))
				throw new InvalidOperationException("The title block does not contain a sheet number.");
		}

		private ImportContext CreateImportContext(AcAp.Document doc, SheetAnalysisData result)
		{
			var projects = _pdsx.Project.GetAll();
			if (projects.Count != 1)
				throw new InvalidOperationException("The analysis database must contain exactly one project.");
			var project = projects[0];
			var document = GetOrCreateDocument(project.Id, doc.Name);
			var sheet = GetOrCreateSheet(document.Id, result.TitleBlock.SheetLayer);
			var locations = _mdbx.Location.GetByProjectId(project.Id).ToDictionary(location => location.Id);
			return new ImportContext(project.Id, document, sheet, locations);
		}

		private IDocument GetOrCreateDocument(Guid projectId, string drawingPath)
		{
			var path = Path.GetFullPath(drawingPath);
			var name = Path.GetFileName(path);

			var existing = _mdbx.Document.GetAll()
				.SingleOrDefault(document =>
					document.ProjectId == projectId &&
					string.Equals(
						document.Name,
						name,
						StringComparison.OrdinalIgnoreCase) &&
					string.Equals(
						document.Path,
						path,
						StringComparison.OrdinalIgnoreCase));

			if (existing != null)
				return existing;

			var format = _pdsx.FileFormat.GetByExtension(
				Path.GetExtension(path));

			if (format == null)
			{
				throw new InvalidOperationException(
					$"No file format is registered for '{Path.GetExtension(path)}'.");
			}

			return Require(_mdbx.Document.Create(
				projectId,
				format.Id,
				name,
				path));
		}

		private ISheet GetOrCreateSheet(Guid documentId, string sheetNo)
		{
			var existing = _mdbx.Sheet.GetByDocumentId(documentId)
				.SingleOrDefault(sheet =>
					string.Equals(
						sheet.SheetNo,
						sheetNo,
						StringComparison.OrdinalIgnoreCase));

			return existing ?? Require(
				_mdbx.Sheet.Create(documentId, sheetNo));
		}
	}
}

