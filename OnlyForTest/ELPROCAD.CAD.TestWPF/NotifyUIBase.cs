using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ELPROCAD.CAD.TestWPF
{
	// Very minimal implementation of INotifyPropertyChanged matching msdn
	// Note that this is dependent on .net 4.5+ because of CallerMemberName
	public class NotifyUIBase : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		public void RaisePropertyChanged([CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
