using ELPROCAD.CAD.Data.Model.Classification;

namespace ELPROCAD.CAD.TestWPF
{
	internal sealed class SubClassVM : VMBase<SubClass>
	{
		public SubClassVM() : this(new SubClass())
		{
		}

		public SubClassVM(SubClass entity) : base(entity)
		{
			TheEntity.Validate();
		}

		public SubClass TheSubClass
		{
			get => TheEntity;
			set => TheEntity = value;
		}

		protected override void OnEntityChanged()
		{
			OnPropertyChanged(nameof(TheSubClass));
			TheEntity.Validate();
		}
	}
}
