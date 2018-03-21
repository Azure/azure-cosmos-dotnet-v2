using System;
using System.Collections.Generic;

using Xamarin.Forms;

namespace ToDoItems.Core
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage(ToDoItem item, bool isReadOnly)
        {
            InitializeComponent();
        }
    }
}
