using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace DocumentDBTodo
{
	public partial class DocumentDBTodoPage : ContentPage
	{
		TodoItemManager manager;
		private bool loginInProgress = false;

		public DocumentDBTodoPage ()
		{
			InitializeComponent ();

			manager = TodoItemManager.DefaultManager;
		}

		protected override async void OnAppearing ()
		{
			base.OnAppearing ();

			await LoginAsync ();

			// Set syncItems to true in order to synchronize the data on startup when running in offline mode
			await RefreshItems (true);
		}

		// Data methods
		async Task AddItem (TodoItem item)
		{
			await manager.InsertItemAsync (item);
			todoList.ItemsSource = await manager.GetTodoItemsAsync ();
		}

		async Task CompleteItem (TodoItem item)
		{
			item.Complete = true;
			await manager.CompleteItemAsync (item);
			todoList.ItemsSource = await manager.GetTodoItemsAsync ();
		}

		public async void OnAdd (object sender, EventArgs e)
		{
			var todo = new TodoItem { Text = newItemName.Text };
			await AddItem (todo);

			newItemName.Text = string.Empty;
			newItemName.Unfocus ();
		}

		// Event handlers
		public async void OnSelected (object sender, SelectedItemChangedEventArgs e)
		{
			var todo = e.SelectedItem as TodoItem;
			if (Device.OS != TargetPlatform.iOS && todo != null) {
				// Not iOS - the swipe-to-delete is discoverable there
				if (Device.OS == TargetPlatform.Android) {
					await DisplayAlert (todo.Text, "Press-and-hold to complete task " + todo.Text, "Got it!");
				} else {
					// Windows, not all platforms support the Context Actions yet
					if (await DisplayAlert ("Mark completed?", "Do you wish to complete " + todo.Text + "?", "Complete", "Cancel")) {
						await CompleteItem (todo);
					}
				}
			}

			// prevents background getting highlighted
			todoList.SelectedItem = null;
		}

		// http://developer.xamarin.com/guides/cross-platform/xamarin-forms/working-with/listview/#context
		public async void OnComplete (object sender, EventArgs e)
		{
			var mi = ((MenuItem)sender);
			var todo = mi.CommandParameter as TodoItem;
			await CompleteItem (todo);
		}

		// http://developer.xamarin.com/guides/cross-platform/xamarin-forms/working-with/listview/#pulltorefresh
		public async void OnRefresh (object sender, EventArgs e)
		{
			var list = (ListView)sender;
			Exception error = null;
			try {
				await RefreshItems (false);
			} catch (Exception ex) {
				error = ex;
			} finally {
				list.EndRefresh ();
			}

			if (error != null) {
				await DisplayAlert ("Refresh Error", "Couldn't refresh data (" + error.Message + ")", "OK");
			}
		}

		public async void OnSyncItems (object sender, EventArgs e)
		{
			await RefreshItems (true);
		}

		private async Task RefreshItems (bool showActivityIndicator)
		{
			if (manager.Client != null) {
				using (var scope = new ActivityIndicatorScope (syncIndicator, showActivityIndicator)) {
					todoList.ItemsSource = await manager.GetTodoItemsAsync ();
				}
			}
		}

		private async Task LoginAsync ()
		{
			if ((manager.Client == null) && (loginInProgress == false)) {
				loginInProgress = true;
				await manager.LoginAsync (this);
				loginInProgress = false;
				if (manager.Client == null) {
					Console.WriteLine ("couldn't login!!");
					return;
				}
			}

		}

		private class ActivityIndicatorScope : IDisposable
		{
			private bool showIndicator;
			private ActivityIndicator indicator;
			private Task indicatorDelay;

			public ActivityIndicatorScope (ActivityIndicator indicator, bool showIndicator)
			{
				this.indicator = indicator;
				this.showIndicator = showIndicator;

				if (showIndicator) {
					indicatorDelay = Task.Delay (2000);
					SetIndicatorActivity (true);
				} else {
					indicatorDelay = Task.FromResult (0);
				}
			}

			private void SetIndicatorActivity (bool isActive)
			{
				this.indicator.IsVisible = isActive;
				this.indicator.IsRunning = isActive;
			}

			public void Dispose ()
			{
				if (showIndicator) {
					indicatorDelay.ContinueWith (t => SetIndicatorActivity (false), TaskScheduler.FromCurrentSynchronizationContext ());
				}
			}
		}
	}
}
