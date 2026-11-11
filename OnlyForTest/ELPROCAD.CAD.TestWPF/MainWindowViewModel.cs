using ELPROCAD.CAD.Data.CRUD.Classification;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static DevExpress.Data.Helpers.ExpressiveSortInfo;

namespace ELPROCAD.CAD.TestWPF
{
	internal class MainWindowViewModel : DependencyViewModel
	{
		private readonly QueryMainClassification? _queryMain;

		public ObservableCollection<MainClassVM> MainClasses 
		{
			get => _mainClasses;
		}
		private ObservableCollection<MainClassVM> _mainClasses = new();

		public MainClassVM EditVM
		{
			get => _editVM;
			set
			{
				if (value  == null)
					throw new ArgumentNullException(nameof(value));

				SetProperty(ref _editVM, value);
			}
			
		}
		private MainClassVM _editVM = new();

		public ICommand SaveCmd { get; private set; }

		/// <summary>
		/// Used for designer
		/// </summary>
		protected MainWindowViewModel() { }

		public MainWindowViewModel(QueryMainClassification queryMain)
		{
			_queryMain = queryMain ?? throw new ArgumentNullException(nameof(queryMain));

			SaveCmd = new DelegateCommand((d) => SaveItems());

			LoadExistingMainClasses();
		}

		private void SaveItems()
		{
			foreach(var item in MainClasses.Where(x=>(x.IsNew || x.IsModified) && !x.IsDeleted).ToList())
			{
				var entity = item.TheMainClass;

				if (!entity.Validate().IsValid)
					continue;

				var result = item.IsNew
					? _queryMain.Create(
						entity.Abbrevation,
						entity.Tooltip,
						entity.Image)
					: _queryMain.Update(
						entity.Id,
						entity.Abbrevation,
						entity.Tooltip,
						entity.Image,
						entity.LoadedVersion);

				entity.ApplyValidationResult(result.Validation);

				if(!result.Validation.IsValid)
				{
					MessageBox.Show(
						string.Join(Environment.NewLine, result.Validation.Errors.Select(x => x.ErrorMessage).Distinct()),
						"Kunde inte spara",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);

					continue;
				}
				if (result.Value != null)
				{
					item.TheMainClass = result.Value;
					item.IsNew = false;
					item.AcceptChanges();
				}
			}
		}

		private void LoadExistingMainClasses()
		{
			var entities = _queryMain.GetAll();

			MainClasses.Clear();

			foreach (var entity in entities)
				MainClasses.Add(new MainClassVM(entity) { IsNew = false });

			OnPropertyChanged(nameof(MainClasses));
		}
	}
}
