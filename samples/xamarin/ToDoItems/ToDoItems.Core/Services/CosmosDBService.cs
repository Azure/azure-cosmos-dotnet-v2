using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace ToDoItems.Core
{
    public class CosmosDBService
    {
        public CosmosDBService()
        {
        }

        static List<ToDoItem> tempItems = new List<ToDoItem>
        {
            new ToDoItem { Completed=false, Id="123", Name="First", Description="First Desc"},
            new ToDoItem { Completed=false, Id="456", Name="Second", Description="Second Desc"},
            new ToDoItem{ Completed=true, Id="789", Name="Third", Description = "Third Desc"}
        };

        public async static Task<List<ToDoItem>> GetToDoItems()
        {
            return await Task.FromResult(tempItems.Where(todo => todo.Completed == false).ToList());
        }

        public static List<ToDoItem> GetCompletedToDoItems()
        {
            return tempItems.Where(todo => todo.Completed == true).ToList();
        }

        public static void CompleteToDoItem(ToDoItem item)
        {
            var found = tempItems.First(todo => todo.Id == item.Id);

            found.Completed = true;
        }

        public static void InsertToDoItem(ToDoItem item)
        {
            tempItems.Add(item);
        }

        public static void DeleteToDoItem(ToDoItem item)
        {
            tempItems.Remove(item);
        }

        public static void UpdateToDoItem(ToDoItem item)
        {
            var found = tempItems.First(todo => todo.Id == item.Id);

            found.Completed = item.Completed;
            found.Description = item.Description;
            found.Name = item.Name;
        }
    }
}
