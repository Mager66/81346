using ELPROCAD.CAD.Data.Model.Classification;
using System.ComponentModel;

namespace ELPROCAD.CAD.TestWPF
{
	internal sealed class MainClassVM : VMBase<MainClass>
	{
		private string _originalAbbrevation = "";
		private string _originalTooltip = "";
		private string _originalImage = "";

		private MainClass _observedEntity;

		public MainClassVM() : this(new MainClass()) { }

		public MainClassVM(MainClass entity) : base(entity)
		{
			_observedEntity = entity;
			_observedEntity.PropertyChanged += Entity_PropertyChanged;

			AcceptChanges();
			TheEntity.Validate();
		}	

		public MainClass TheMainClass
		{
			get => TheEntity;
			set => TheEntity = value;
		}

		public new bool IsModified =>
			TheEntity.Abbrevation != _originalAbbrevation ||
			TheEntity.Tooltip != _originalTooltip ||
			TheEntity.Image != _originalImage;

		public void AcceptChanges()
		{
			_originalAbbrevation = TheEntity.Abbrevation;
			_originalTooltip = TheEntity.Tooltip;
			_originalImage = TheEntity.Image;

			OnPropertyChanged(nameof(IsModified));
		}

		private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			throw new NotImplementedException();
		}

		protected override void OnEntityChanged()
		{
			_observedEntity.PropertyChanged -= Entity_PropertyChanged;

			_observedEntity = TheEntity;
			_observedEntity.PropertyChanged += Entity_PropertyChanged;

			OnPropertyChanged(nameof(TheMainClass));
			OnPropertyChanged(nameof(IsModified));

			TheEntity.Validate();
		}
	}
}
