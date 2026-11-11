using DevExpress.Xpo;
using System;

namespace ELPROCAD.API.Model
{
	[Persistent("Locations")]
	public class Location : PersistentEntity
	{
		private string _name = string.Empty;
		private Location? _parent;
		private EngineeringObject? _representingObject;

		public Location(Session session) : base(session) { }

		[Size(50)]
		public string Name
		{
			get => _name;
			set => SetPropertyValue(nameof(Name), ref _name, value);
		}

		[Association("Location-Children")]
		public Location? Parent
		{
			get => _parent;
			set => SetPropertyValue(nameof(Parent), ref _parent, value);
		}

		[Association("Location-Children")]
		public XPCollection<Location> Children => GetCollection<Location>(nameof(Children));

		[Association("Location-Objects")]
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