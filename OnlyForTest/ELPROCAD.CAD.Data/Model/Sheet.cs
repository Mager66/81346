using DevExpress.Xpo;
using System;

namespace ELPROCAD.CAD.Data.Model
{
	[Persistent("Sheets")]
	public class Sheet : PersistentEntity
	{
		private string _sheetNo = string.Empty;
		private Document _document = null;

		public Sheet(Session session) : base(session) { }

		[Size(10)]
		public string SheetNo
		{
			get => _sheetNo;
			set => SetPropertyValue(nameof(SheetNo), ref _sheetNo, value);
		}

		[Association("Document-Sheets")]
		public Document Document
		{
			get => _document;
			set => SetPropertyValue(nameof(_document), ref _document, value);
		}

		[Association("Sheet-Objects")]
		public XPCollection<EngineeringObject> Objects => GetCollection<EngineeringObject>(nameof(Objects));

		protected override void OnSaving()
		{
			if (!IsDeleted && Document == null)
				throw new InvalidOperationException("Ett blad måste tillhöra ett dokement.");

			base.OnSaving();
		}
	}

}