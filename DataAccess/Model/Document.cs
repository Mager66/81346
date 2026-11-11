using DevExpress.Xpo;
using System;

namespace ELPROCAD.API.Model
{
	[Persistent("Documents")]
	public class Document : PersistentEntity
	{
		private string _name = string.Empty;
		private Project _project = null;

		public Document(Session session) : base(session) { }

		[Size(50)]
		public string Name
		{
			get => _name;
			set => SetPropertyValue(nameof(Name), ref _name, value);
		}

		[Association("Project-Documents")]
		public Project Project
		{
			get => _project;
			set => SetPropertyValue(nameof(Project), ref _project, value);
		}

		[Association("Document-Sheets")]
		public XPCollection<Sheet> Sheets => GetCollection<Sheet>(nameof(Sheets));

		protected override void OnSaving()
		{
			if (!IsDeleted && Project == null)
				throw new InvalidOperationException("Ett dokument måste tillhöra ett projekt.");

			base.OnSaving();
		}
	}

}