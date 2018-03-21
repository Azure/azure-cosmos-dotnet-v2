using System;
using Xamarin.Forms;
namespace ToDoItems.Core
{
    public class App : Application
    {
        public App()
        {
            MainPage = new NavigationPage(new ToDoItemsPage());
        }
    }
}
