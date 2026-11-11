using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;

namespace ELPROCAD.CAD.Data.CRUD.Analisys
{
	public sealed class QueryDocument
	{
		private readonly UnitOfWork _unitOfWork;

		public QueryDocument(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public Document? GetDocumentById(Guid id) => _unitOfWork.GetObjectByKey<Document>(id);

		public Document? GetOrCreate(Project project, string docName, string docPath)
		{
			var docs = GetAll();
			var doc = docs.FirstOrDefault(x => x.Name == docName && x.Path == docPath);
			if (doc == null)
			{
				doc = new Document(_unitOfWork)
				{
					Name = docName,
					Path = docPath,
					Project = project
				};
				project.Documents.Add(doc);
				_unitOfWork.CommitChanges();
			}
			return doc;
		}

		public IReadOnlyList<Document> GetAll()
		{
			return new XPQuery<Document>(_unitOfWork)
			.OrderBy(d => d.Name)
			.ToList()
			.AsReadOnly();
		}

		public Document? Add(Guid projId, string name, string path, string description)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(projId);
			if (project == null)
				throw new ArgumentNullException("Kan inte skapa dokument utan projekt");

			var doc = new Document(_unitOfWork)
			{
				Name = name,
				Project = project
			};
			project.Documents.Add(doc);

			_unitOfWork.CommitChanges();

			return doc;
		}

		public Document Update(Guid id, string name, string path, string description)
		{
			var doc = GetDocumentById(id)
				?? throw new KeyNotFoundException(
					$"Projektet '{id}' finns inte.");

			doc.Name = name;

			_unitOfWork.CommitChanges();

			return doc;
		}

		public bool Delete(Guid id)
		{
			var doc = GetDocumentById(id);

			if (doc == null)
				return false;

			var hasSheets = new XPQuery<Sheet>(_unitOfWork)
				.Any(sheet => sheet.Document == doc);

			if (hasSheets)
			{
				throw new InvalidOperationException(
					"Dokumentet innehåller blad.");
			}

			_unitOfWork.Delete(doc);
			_unitOfWork.CommitChanges();

			return true;
		}
	}
}
