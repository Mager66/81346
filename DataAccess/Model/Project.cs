using DevExpress.Xpo;
using System;

namespace ELPROCAD.API.Model
{
	[Persistent("Project")]
	public sealed class Project : PersistentEntity
	{
		private string _name = string.Empty;
		private string _path = string.Empty;
		private string _description = string.Empty;

		public Project(Session session) : base(session) { }

		[Size(50)]
		public string Name
		{
			get => _name;
			set => SetPropertyValue(nameof(Name), ref _name, value);
		}

		[Size(255)]
		public string Path
		{
			get => _path;
			set => SetPropertyValue(nameof(Path), ref _path, value);
		}

		[Size(255)]
		public string Description
		{
			get => _description;
			set => SetPropertyValue(nameof(Description), ref _description, value);
		}

		[Association("Project-Documents")]
		public XPCollection<Document> Documents => GetCollection<Document>(nameof(Documents));

		[Association("Project-Objects")]
		public XPCollection<EngineeringObject> Objects => GetCollection<EngineeringObject>(nameof(Objects));
	}

}