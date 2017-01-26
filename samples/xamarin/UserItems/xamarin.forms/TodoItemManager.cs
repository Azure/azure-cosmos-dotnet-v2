using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DocumentDBTodo
{
	public partial class TodoItemManager
	{
		static TodoItemManager defaultInstance = new TodoItemManager ();

		const string accountURL = @"{account url}";
		const string databaseId = @"{database name}";
		const string collectionId = @"UserItems";
		const string resourceTokenBrokerURL = @"{resource token broker base url, e.g. https://xamarin.azurewebsites.net}";

		private Uri collectionLink = UriFactory.CreateDocumentCollectionUri (databaseId, collectionId);

		public DocumentClient Client { get; private set; }
		public string UserId { get; private set; }

		private TodoItemManager ()
		{
		}

		public static TodoItemManager DefaultManager {
			get {
				return defaultInstance;
			}
			private set {
				defaultInstance = value;
			}
		}

		public async Task LoginAsync (Xamarin.Forms.Page page)
		{
			string resourceToken = null;
			var tcs = new TaskCompletionSource<bool> ();
#if __IOS__
			var controller = UIKit.UIApplication.SharedApplication.KeyWindow.RootViewController;
#endif
			try {
				var auth = new Xamarin.Auth.WebRedirectAuthenticator (
					initialUrl: new Uri (resourceTokenBrokerURL + "/.auth/login/facebook"),
					redirectUrl: new Uri (resourceTokenBrokerURL + "/.auth/login/done"));

				auth.Completed += async (sender, e) => {
					if (e.IsAuthenticated && e.Account.Properties.ContainsKey ("token")) {
#if __IOS__
						controller.DismissViewController (true, null);
#endif
						var easyAuthResponseJson = JsonConvert.DeserializeObject<JObject> (e.Account.Properties ["token"]);
						var easyAuthToken = easyAuthResponseJson.GetValue ("authenticationToken").ToString ();

						//call ResourceBroker to get the DocumentDB resource token
						var http = new HttpClient ();
						http.DefaultRequestHeaders.Add ("x-zumo-auth", easyAuthToken);
						var resourceTokenResponse = await http.GetAsync (resourceTokenBrokerURL + "/api/resourcetoken/");
						var resourceTokenJson = JsonConvert.DeserializeObject<JObject> (await resourceTokenResponse.Content.ReadAsStringAsync ());
						resourceToken = resourceTokenJson.GetValue ("token").ToString ();
						UserId = resourceTokenJson.GetValue ("userid").ToString ();

						if (resourceToken == null) return; // failed to login

						Client = new DocumentClient (new System.Uri (accountURL), resourceToken);

						tcs.SetResult (true);
					}
				};
#if __IOS__
				controller.PresentViewController (auth.GetUI (), true, null);
#else
				Xamarin.Forms.Forms.Context.StartActivity (auth.GetUI (Xamarin.Forms.Forms.Context));
#endif

			} 
			catch (Exception ex) {
				Console.Error.WriteLine (@"ERROR Login {0}", ex.Message);

			}

			await tcs.Task;
		}

		public List<TodoItem> Items { get; private set; }

		public async Task<List<TodoItem>> GetTodoItemsAsync ()
		{
			try {
				// The query excludes completed TodoItems
				Console.WriteLine ("QUERY: partitionkey is {0}", this.UserId);
				var query = Client.CreateDocumentQuery<TodoItem> (collectionLink, new FeedOptions { MaxItemCount = -1, PartitionKey = new PartitionKey (this.UserId) })
								  .Where (todoItem => /*todoItem.UserId == this.UserId &&*/ todoItem.Complete == false)
					  .AsDocumentQuery ();

				var tList = new List<TodoItem> ();
				while (query.HasMoreResults) {
					tList.AddRange (await query.ExecuteNextAsync<TodoItem> ());
				}
				Items = tList;

			} catch (Exception e) {
				Console.Error.WriteLine (@"ERROR {0}", e.Message);
				return null;
			}

			return Items;
		}

		public async Task<TodoItem> InsertItemAsync (TodoItem todoItem)
		{
			try {
				todoItem.UserId = this.UserId;
				var result = await Client.CreateDocumentAsync (collectionLink, todoItem);
				todoItem.Id = result.Resource.Id;
				Items.Add (todoItem);


			} catch (Exception e) {
				Console.Error.WriteLine (@"ERROR {0}", e.Message);
			}
			return todoItem;
		}

		public async Task CompleteItemAsync (TodoItem item)
		{
			try {
				item.Complete = true;
				await Client.ReplaceDocumentAsync (UriFactory.CreateDocumentUri (databaseId, collectionId, item.Id), item);

				Items.Remove (item);

			} catch (Exception e) {
				Console.Error.WriteLine (@"ERROR {0}", e.Message);
			}
		}
	}
}
