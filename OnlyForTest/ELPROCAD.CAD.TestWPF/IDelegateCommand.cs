using System.Windows.Input;

namespace ELPROCAD.CAD.TestWPF
{
	internal interface IDelegateCommand : ICommand
	{
		void OnCanExecuteChanged();
	}
}
