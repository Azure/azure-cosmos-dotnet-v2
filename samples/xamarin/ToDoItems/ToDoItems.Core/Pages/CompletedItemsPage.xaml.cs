using System;
using System.Collections.Generic;

using Xamarin.Forms;

namespace ToDoItems.Core
{
	public partial class CompletedItemsPage : ContentPage
	{
		CompletedItemsViewModel ViewModel;
		public CompletedItemsPage()
		{
			InitializeComponent();

			ViewModel = new CompletedItemsViewModel();
			BindingContext = ViewModel;
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();

			ViewModel.RefreshCommand.Execute(null);
		}
	}
}
