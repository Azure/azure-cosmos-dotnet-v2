using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ToDoItems.Core
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        string _title = "";
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value)
                    return;

                value = _title;

                ExecutePropertyChanged();
            }
        }

        bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                value = _isBusy;

                ExecutePropertyChanged();
            }
        }

        void ExecutePropertyChanged([CallerMemberName]string propertyName = "")
        {

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
