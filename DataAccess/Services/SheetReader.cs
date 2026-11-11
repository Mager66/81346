using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Services
{
	public sealed class SheetReader : ISheetReader
	{
		private readonly UnitOfWork _unitOfWork;

		public SheetReader(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public IReadOnlyList<SheetDto>? GetByDocument(Guid documentId)
		{
			var document = _unitOfWork.GetObjectByKey<Document>(documentId);

			if (document == null)
				return null;

			var sheets = new XPQuery<Sheet>(_unitOfWork)
				.Where(sheet => sheet.Document == document)
				.OrderBy(sheet => sheet.SheetNo)
				.ToList();

			return sheets.Select(sheet => new SheetDto
			{
				Id = sheet.Id,
				DocumentId = document.Id,
				SheetNo = sheet.SheetNo
			}).ToList().AsReadOnly();
		}
	}
}