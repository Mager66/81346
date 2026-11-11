using ELPROCAD.CAD.Library.AppModel.Observable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace ELPROCAD.CAD.TestWPF
{
	internal abstract class DependencyViewModel :
		DependencyObject,
		IViewModel,
		INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected bool SetProperty<T>(
			ref T storage,
			T value,
			[CallerMemberName] string? propertyName = null)
		{
			if (Equals(storage, value))
				return false;

			storage = value;
			OnPropertyChanged(propertyName);

			return true;
		}

		protected void OnPropertyChanged(
			[CallerMemberName] string? propertyName = null)
		{
			var dispatcher = Dispatcher;

			if (dispatcher.HasShutdownStarted ||
				dispatcher.HasShutdownFinished)
			{
				return;
			}

			if (dispatcher.CheckAccess())
			{
				PropertyChanged?.Invoke(
					this,
					new PropertyChangedEventArgs(propertyName));

				return;
			}

			dispatcher.BeginInvoke(
				DispatcherPriority.Normal,
				new Action(() =>
				{
					if (dispatcher.HasShutdownStarted ||
						dispatcher.HasShutdownFinished)
					{
						return;
					}

					PropertyChanged?.Invoke(
						this,
						new PropertyChangedEventArgs(propertyName));
				}));
		}

		protected void OnPropertyChangedAll()
		{
			OnPropertyChanged(string.Empty);
		}
	}
}

