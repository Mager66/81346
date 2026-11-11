using DevExpress.Xpo;
using ELPROCAD.CAD.Data.CRUD.Classification;
using ELPROCAD.CAD.Data.Model.Classification;
using System.Windows;

namespace ELPROCAD.CAD.TestWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow(QueryMainClassification queryMain)
		{
			InitializeComponent();
			this.DataContext = new MainWindowViewModel(queryMain);
		}
	}
}
