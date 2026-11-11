using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;

namespace ELPROCAD.CAD.Data.CRUD.Analisys
{
	public sealed class QuerySheet
	{
		private readonly UnitOfWork _unitOfWork;

		public QuerySheet(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public Sheet? GetSheetById(Guid id) => _unitOfWork.GetObjectByKey<Sheet>(id);

		public Sheet? GetOrCreate(Document doc, string sheetNo)
		{
			var sheets = GetAll();
			var sheet = sheets.FirstOrDefault(x => x.Document == doc && x.SheetNo == sheetNo);
			if (sheet == null)
			{
				sheet = new Sheet(_unitOfWork)
				{
					SheetNo = sheetNo,
					Document = doc				
				};
				doc.Sheets.Add(sheet);
				_unitOfWork.CommitChanges();
			}			

			return sheet;
		}

		public IReadOnlyList<Sheet> GetAll()
		{
			return new XPQuery<Sheet>(_unitOfWork)
			.OrderBy(d => d.SheetNo)
			.ToList()
			.AsReadOnly();
		}

		public Sheet? Add(Guid docId, string sheetNo)
		{
			var doc = _unitOfWork.GetObjectByKey<Document>(docId);
			if (doc == null)
				throw new ArgumentNullException("Kan inte skapa blad utan dokument");

			var sheet = new Sheet(_unitOfWork)
			{
				SheetNo = sheetNo,
				Document = doc
			};
			doc.Sheets.Add(sheet);

			_unitOfWork.CommitChanges();

			return sheet;
		}

		public Sheet Update(Guid id, string sheetNo)
		{
			var sheet = GetSheetById(id)
				?? throw new KeyNotFoundException(
					$"Projektet '{id}' finns inte.");

			sheet.SheetNo = sheetNo;

			_unitOfWork.CommitChanges();

			return sheet;
		}

		public bool Delete(Guid id)
		{
			var sheet = GetSheetById(id);

			if (sheet == null)
				return false;

			var hasObjects = new XPQuery<EngineeringObject>(_unitOfWork)
				.Any(o => o.Sheet == sheet);

			if (hasObjects)
				throw new InvalidOperationException("Bladet innehåller objekt.");

			_unitOfWork.Delete(sheet);
			_unitOfWork.CommitChanges();

			return true;
		}
	}
}
