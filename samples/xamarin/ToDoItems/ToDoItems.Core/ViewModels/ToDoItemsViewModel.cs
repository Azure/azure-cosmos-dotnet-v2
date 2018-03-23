using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using Xamarin.Forms;
namespace ToDoItems.Core
{
	public class ToDoItemsViewModel : BaseViewModel
	{
		List<ToDoItem> todoItems;
		public List<ToDoItem> ToDoItems { get => todoItems; set => SetProperty(ref todoItems, value); }

		public ICommand RefreshCommand { get; }

		public ToDoItemsViewModel()
		{
			ToDoItems = new List<ToDoItem>();
			RefreshCommand = new Command(async () => await ExecuteRefreshCommand());
		}

		async Task ExecuteRefreshCommand()
		{
			if (IsBusy)
				return;

			IsBusy = true;

			try
			{
				ToDoItems = await CosmosDBService.GetToDoItems();
			}
			finally
			{
				IsBusy = false;
			}
		}

	}
}
