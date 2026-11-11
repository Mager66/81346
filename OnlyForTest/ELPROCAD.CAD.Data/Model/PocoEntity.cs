using DevExpress.Xpo;
using ELPROCAD.CAD.Library.AppModel.Observable;
using FluentValidation.Results;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ELPROCAD.CAD.Data.Model
{
	[NonPersistent]
	public abstract class PocoEntity : ObservableObject, INotifyDataErrorInfo
	{
		private readonly Dictionary<string, string[]> _errors = new();

		[Key]
		public Guid Id { get; set; } = Guid.NewGuid();

		[NonPersistent]
		public int? LoadedVersion { get; internal set; }

		[NonPersistent]
		public bool HasErrors => _errors.Count > 0;

		public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

		public IEnumerable GetErrors(string? propertyName)
		{
			var key = propertyName ?? string.Empty;

			return _errors.TryGetValue(key, out var errors)
				? errors
				: Array.Empty<string>();
		}

		protected new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
		{
			if(Equals(storage, value))
				return false;

			storage = value;
			OnPropertyChanged(propertyName);
			Validate();

			return true;
		}

		public ValidationResult Validate()
		{
			var result = ValidateCore();
			ApplyValidationResult(result);

			return result;
		}

		public void ApplyValidationResult(ValidationResult result)
		{
			if (result == null)
				throw new ArgumentNullException(nameof(result));

			var affectedProperties = new HashSet<string>(_errors.Keys);

			_errors.Clear();

			foreach(var group in result.Errors.GroupBy(x=>x.PropertyName))
			{
				_errors[group.Key] = group
					.Select(x => x.ErrorMessage)
					.Distinct()
					.ToArray();

				affectedProperties.Add(group.Key);
			}

			foreach(var propertyName in affectedProperties)
			{
				ErrorsChanged?.Invoke(
				this,
				new DataErrorsChangedEventArgs(propertyName));
			}

			OnPropertyChanged(nameof(HasErrors));
		}

		protected abstract ValidationResult ValidateCore();
	}
}
