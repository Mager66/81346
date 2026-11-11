using ELPROCAD.CAD.Data.Model.Classification;

namespace ELPROCAD.CAD.TestWPF
{
	internal class MainWindowDesignModel : MainWindowViewModel
	{
		public static MainWindowDesignModel Instance { get; } = new MainWindowDesignModel();

		public MainWindowDesignModel()
		{
			var main = new MainClassVM(new MainClass
			{
				Abbrevation = "MC",
				Tooltip = "Main Component",
				Image = ""
			})
			{
				IsNew = false
			};

			MainClasses.Add(main);
			EditVM = main;
		}
	}	
}
