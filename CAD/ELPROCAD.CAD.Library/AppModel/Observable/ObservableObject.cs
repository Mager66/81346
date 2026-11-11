using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ELPROCAD.CAD.Library.AppModel.Observable
{

	/// <summary>
	/// Core base class for objects that implement <see cref="INotifyPropertyChanged"/>.
	/// 
	/// IMPORTANT DESIGN DECISION:
	/// - This class is part of the CORE layer and is explicitly NOT WPF-based.
	/// - It does NOT inherit from DependencyObject.
	/// - WPF-specific view models use a separate base class that extends DependencyObject.
	/// 
	/// Rationale:
	/// - DependencyObject introduces a hard WPF dependency that is not suitable for
	///   core logic, services, DI containers, unit tests, or .NET 8 non-UI projects.
	/// - .NET 8 places additional constraints on DependencyObject usage outside WPF.
	/// 
	/// This class provides a lightweight, framework-agnostic implementation of
	/// property change notification.
	/// </summary>
	public abstract class ObservableObject : INotifyPropertyChanged, IViewModel
	{
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Sets the backing field and raises <see cref="PropertyChanged"/> only
		/// when the value has actually changed.
		/// Intended for use in property setters.
		/// </summary>
		/// <typeparam name="T">Type of the property.</typeparam>
		/// <param name="storage">Reference to the backing field.</param>
		/// <param name="value">New value.</param>
		/// <param name="propertyName">
		/// Property name automatically supplied by the compiler.
		/// </param>
		/// <returns>
		/// True if the value changed; otherwise false.
		/// </returns>
		protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
				return false;

			storage = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> event.
		/// The event is raised on the calling thread.
		/// No UI-thread dispatching is performed.
		/// </summary>
		/// <param name="propertyName">Name of the changed property.</param>
		protected virtual void OnPropertyChanged(
			[CallerMemberName] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Raises <see cref="PropertyChanged"/> indicating that all properties have changed.
		/// </summary>
		protected void OnPropertyChangedAll() => OnPropertyChanged(string.Empty);

		#region Static property support

		/// <summary>
		/// Raised when a static property value changes.
		/// </summary>
		public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

		/// <summary>
		/// Raises <see cref="StaticPropertyChanged"/> for a static property.
		/// </summary>
		/// <param name="propertyName">
		/// Static property name automatically supplied by the compiler.
		/// </param>
		protected static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = null)
			=> StaticPropertyChanged?.Invoke(
				null,
				new PropertyChangedEventArgs(propertyName));

		#endregion Static property support (optional)
	}
}
