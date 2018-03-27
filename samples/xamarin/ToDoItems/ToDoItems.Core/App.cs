using System;
using Xamarin.Forms;
namespace ToDoItems.Core
{
    public class App : Application
    {
        public App()
        {
            var mainTabbedPage = new TabbedPage
            {
                Children = {
                    new NavigationPage(new ToDoItemsPage()) { Title="To Do"},
                    new NavigationPage(new CompletedItemsPage()){ Title="Complete"}
                }
            };

            MainPage = mainTabbedPage;
            //MainPage = new NavigationPage(new ToDoItemsPage());
        }
    }
}
