using System;
using System.Collections.Generic;

using Xamarin.Forms;

namespace ToDoItems.Core
{
	public partial class ItemDetailPage : ContentPage
	{
		ToDoItem todoItem;

		public ItemDetailPage(ToDoItem item, bool isReadOnly)
		{
			InitializeComponent();

			todoItem = item;
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();
		}
	}
}
