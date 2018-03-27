using System;
using Xamarin.Forms;
using Xamarin.Forms.PlatformConfiguration.iOSSpecific;

namespace ToDoItems.Core
{
    public class App : Application
    {
        public App()
        {
            var toDoNavPage = new Xamarin.Forms.NavigationPage(new ToDoItemsPage())
            {
                Title = "To Do",
                BarBackgroundColor = Color.FromHex("#2082fa"),
                BarTextColor = Color.White
            };
            toDoNavPage.On<Xamarin.Forms.PlatformConfiguration.iOS>().SetPrefersLargeTitles(true);

            var completedNavPage = new Xamarin.Forms.NavigationPage(new CompletedItemsPage())
            {
                Title = "Complete",
                BarBackgroundColor = Color.FromHex("#2082fa"),
                BarTextColor = Color.White
            };
            completedNavPage.On<Xamarin.Forms.PlatformConfiguration.iOS>().SetPrefersLargeTitles(true);

            var mainTabbedPage = new TabbedPage
            {
                Children = {
                    toDoNavPage, completedNavPage
                }
            };

            MainPage = mainTabbedPage;
        }
    }
}
