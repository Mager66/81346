using DevExpress.Xpo;
using ELPROCAD.CAD.Library.AppModel.Observable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELPROCAD.CAD.Data.Model.Tests
{
	[Persistent("PocoTests")]
	[OptimisticLocking(true)]
	public sealed class PocoTest : ObservableObject
	{
		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		public string Name
		{
			get => _name;
			set=>SetProperty(ref _name, value);
		}
		private string _name = string.Empty;

		public string Description
		{
			get => _description;
			set => SetProperty(ref _description, value);
		}
		private string _description = string.Empty;
	}
}
