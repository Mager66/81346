using DevExpress.Xpo;
using Microsoft.AspNetCore.Identity;
using System;

namespace ELPROCAD.API.Model
{
	[Persistent("Objects")]
	public class EngineeringObject : PersistentEntity
	{
		private Project _project = null;
		private Location? _location;
		private Function _function;

		public EngineeringObject(Session session) : base(session) { }

		[Association("Project-Objects")]
		public Project Project
		{
			get => _project;
			set => SetPropertyValue(nameof(Project), ref _project, value);
		}

		[Association("Location-Objects")]
		public Location? Location
		{
			get => _location;
			set => SetPropertyValue(nameof(Location), ref _location, value);
		}

		[Association("Function-Objects")]
		public Function Function
		{
			get => _function;
			set => SetPropertyValue(nameof(Function), ref _function, value);
		}

		protected override void OnSaving()
		{
			if (!IsDeleted && Project == null)
				throw new InvalidOperationException("Ett objekt måste tillhöra ett projekt.");

			base.OnSaving();
		}
	}

}