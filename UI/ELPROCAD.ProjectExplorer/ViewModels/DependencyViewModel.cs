using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace ELPROCAD.ProjectExplorer.ViewModels
{
	/// <summary>
	/// Provides property change notifications for WPF view models.
	/// Instances and bound state should be managed on the UI thread.
	/// </summary>
	internal abstract class DependencyViewModel : DependencyObject, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Updates a field and notifies bindings when its value changes.
		/// </summary>
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

		/// <summary>
		/// Raises a property change notification on the owning dispatcher.
		/// This does not marshal the property assignment itself.
		/// </summary>
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

		/// <summary>
		/// Notifies bindings that all property values may have changed.
		/// </summary>
		protected void OnPropertyChangedAll()
		{
			OnPropertyChanged(string.Empty);
		}
	}
}