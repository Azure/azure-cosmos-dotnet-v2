using System;
using System.Collections.Generic;

using Xamarin.Forms;

namespace ToDoItems.Core
{
    public partial class ToDoItemsPage : ContentPage
    {
        ToDoItemsViewModel vm;

        public ToDoItemsPage()
        {
            InitializeComponent();

            vm = new ToDoItemsViewModel();
            BindingContext = vm;
            vm.Title = "To Do Items";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            todoItemsList.ItemSelected += listItemSelected;
            todoItemsList.ItemTapped += (sender, args) => todoItemsList.SelectedItem = null;

            if (vm.ToDoItems.Count == 0)
                vm.RefreshCommand.Execute(null);
        }


        protected async void listItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var todoItem = e.SelectedItem as ToDoItem;

            if (todoItem == null)
                return;

            await Navigation.PushAsync(new ItemDetailPage(todoItem, false));
        }
    }
}
