using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ToDoItems.Core
{
	public class ToDoItem : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		string _id;
		[JsonProperty("id")]
		public string Id
		{
			get => _id;
			set
			{
				if (_id == value)
					return;

				_id = value;

				HandlePropertyChanged();
			}
		}

		string _name;
		[JsonProperty("name")]
		public string Name
		{
			get => _name;
			set
			{
				if (_name == value)
					return;

				_name = value;

				HandlePropertyChanged();
			}
		}

		string _description;
		[JsonProperty("description")]
		public string Description
		{
			get => _description;
			set
			{
				if (_description == value)
					return;

				_description = value;

				HandlePropertyChanged();
			}
		}

		bool _completed;
		[JsonProperty("completed")]
		public bool Completed
		{
			get => _completed;
			set
			{
				if (_completed == value)
					return;

				_completed = value;

				HandlePropertyChanged();
			}
		}

		void HandlePropertyChanged([CallerMemberName]string propertyName = "")
		{
			var eventArgs = new PropertyChangedEventArgs(propertyName);

			PropertyChanged?.Invoke(this, eventArgs);
		}
	}
}
