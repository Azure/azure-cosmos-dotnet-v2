using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ToDoItems.Core
{
	public partial class ItemDetailPage : ContentPage
	{
		ToDoItem ToDoItem;
		bool IsNew;
		ItemDetailViewModel ViewModel;

		public ItemDetailPage(ToDoItem item, bool isNew)
		{
			InitializeComponent();

			ToDoItem = item;
			IsNew = isNew;

			ViewModel = new ItemDetailViewModel(ToDoItem, IsNew);
			ViewModel.SaveComplete += Handle_SaveComplete;

			BindingContext = ViewModel;
		}

		protected override void OnDisappearing()
		{
			base.OnDisappearing();

			ViewModel.SaveComplete -= Handle_SaveComplete;
		}

		async void Handle_SaveComplete(object sender, EventArgs e)
		{
			await DismissPage();
		}

		protected async void Handle_CancelClicked(object sender, EventArgs e)
		{
			await DismissPage();
		}

		async Task DismissPage()
		{
			if (IsNew)
				await Navigation.PopModalAsync();
			else
				await Navigation.PopAsync();
		}
	}
}