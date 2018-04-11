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
		public ICommand CompleteCommand { get; }

		public ToDoItemsViewModel()
		{
			ToDoItems = new List<ToDoItem>();
			RefreshCommand = new Command(async () => await ExecuteRefreshCommand());
			CompleteCommand = new Command<ToDoItem>(async (item) => await ExecuteCompleteCommand(item));
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

		async Task ExecuteCompleteCommand(ToDoItem item)
		{
			if (IsBusy)
				return;

			IsBusy = true;

			try
			{
				await CosmosDBService.CompleteToDoItem(item);
				ToDoItems = await CosmosDBService.GetToDoItems();
			}
			finally
			{
				IsBusy = false;
			}
		}
	}
}
