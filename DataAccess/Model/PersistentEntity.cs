using DevExpress.Xpo;
using System;

namespace ELPROCAD.API.Model
{
	[NonPersistent]
	public class PersistentEntity : XPCustomObject
	{
		private Guid id;

		protected PersistentEntity(Session session):base(session) { }

		[Key(AutoGenerate = true)]
		public Guid Id
		{
			get => id;
			set=>SetPropertyValue(nameof(Id), ref id, value);
		}
	}
}