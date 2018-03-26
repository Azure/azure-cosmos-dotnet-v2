using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
namespace ToDoItems.Core
{
	public class ItemDetailViewModel : BaseViewModel
	{
		public bool IsNew { get; set; }
		public ToDoItem ToDoItem { get; set; }
		public ICommand SaveCommand { get; }

		public event EventHandler SaveComplete;

		public ItemDetailViewModel(ToDoItem todo, bool isNew)
		{
			IsNew = isNew;
			ToDoItem = todo;

			SaveCommand = new Command(async () => await ExecuteSaveCommand());

			Title = IsNew ? "New To Do" : ToDoItem.Name;
		}

		async Task ExecuteSaveCommand()
		{
			if (IsNew)
				await CosmosDBService.InsertToDoItem(ToDoItem);
			else
				await CosmosDBService.UpdateToDoItem(ToDoItem);

			SaveComplete?.Invoke(this, new EventArgs());
		}
	}
}
