using DevExpress.Xpo;
using System;

namespace ELPROCAD.CAD.Data.Model
{
	[Persistent("Functions")]
	public class Function : PersistentEntity
	{
		private string _name = string.Empty;
		private Function? _parent;
		private EngineeringObject? _representingObject;

		public Function(Session session) : base(session) { }

		[Size(50)]
		public string Name
		{
			get => _name;
			set => SetPropertyValue(nameof(Name), ref _name, value);
		}

		[Association("Function-Children")]
		public Function? Parent
		{
			get => _parent;
			set => SetPropertyValue(nameof(Parent), ref _parent, value);
		}

		[Association("Function-Children")]
		public XPCollection<Function> Children => GetCollection<Function>(nameof(Children));

		[Association("Function-Objects")]
		public XPCollection<EngineeringObject> Objects => GetCollection<EngineeringObject>(nameof(Objects));

		public EngineeringObject? RepresentingObject
		{
			get => _representingObject;
			set => SetPropertyValue(
				nameof(RepresentingObject),
				ref _representingObject,
				value);
		}
	}

}