using ELPROCAD.CAD.Library.AppModel.Observable;

namespace ELPROCAD.CAD.TestWPF
{
	internal abstract class VMBase<TEntity> : ObservableObject where TEntity : class
	{
		private TEntity _entity;

		protected VMBase(TEntity entity) => _entity = entity ?? throw new ArgumentNullException(nameof(entity));

		public TEntity TheEntity
		{
			get => _entity;
			set
			{
				if (value == null)
					throw new ArgumentNullException(nameof(value));

				if (ReferenceEquals(_entity, value))
					return;

				_entity = value;

				OnPropertyChanged();
				OnEntityChanged();
			}
		}

		protected virtual void OnEntityChanged() { }

				
		public bool IsNew
		{
			get => _isNew;
			set => SetProperty(ref _isNew, value);			
		}
		private bool _isNew = true;

		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);			
		}
		private bool _isSelected;
		
		public bool IsDeleted
		{
			get => _isDeleted;
			set => SetProperty(ref _isDeleted, value);
		}
		private bool _isDeleted = false;

		public bool IsModified
		{
			get => _isModified;
			set=>SetProperty(ref _isModified, value);
		}
		private bool _isModified = false;
	}
}
