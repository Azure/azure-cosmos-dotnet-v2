using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using Xamarin.Forms;
namespace ToDoItems.Core
{
    public class ToDoItemsViewModel : BaseViewModel
    {
        public ObservableCollection<ToDoItem> ToDoItems { get; }

        public ICommand RefreshCommand { get; }

        public ToDoItemsViewModel()
        {
            ToDoItems = new ObservableCollection<ToDoItem>();
            RefreshCommand = new Command(async () => await ExecuteRefreshCommand());
        }

        async Task ExecuteRefreshCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                var items = await CosmosDBService.GetToDoItems();

                if (items != null && items.Count > 0)
                {
                    ToDoItems.Clear();
                    items.ForEach(todo => ToDoItems.Add(todo));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

    }
}
