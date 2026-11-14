using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.ProjectExplorer.Services;
using ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.ViewModel;
using System.Windows;
using System.Windows.Controls;

namespace ELPROCAD.ProjectExplorer.UIs.ProjectExplorerWindow.View
{
	/// <summary>
	/// Interaction logic for ProjectExplorerWindow.xaml
	/// </summary>
	public partial class ProjectExplorerWindow : Window
	{
		public ProjectExplorerWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Forwards tree selection to the view model.
		/// TreeView.SelectedItem cannot be bound directly using TwoWay binding.
		/// </summary>
		private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (sender is not TreeView tree || tree.DataContext is not ProjectExplorerViewModel viewModel)
				return;

			viewModel.SelectedNode = e.NewValue as ProjectTreeNodeViewModel;
        }

		/// <summary>
		/// Loads a fresh project snapshot into the window.
		/// Must be called on the window's UI thread.
		/// </summary>
		public void Load(IMdbxRepository mdbx, Guid projectId)
		{
			var reader = new ProjectExplorerReader(mdbx, projectId);
			DataContext = reader.CreateViewModel();
		}
	}
}
