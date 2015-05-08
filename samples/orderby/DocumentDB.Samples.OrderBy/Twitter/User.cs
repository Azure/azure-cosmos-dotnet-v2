namespace DocumentDB.Samples.Twitter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DocumentDB.Samples.OrderBy.Util;
    using Newtonsoft;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a user.
    /// </summary>
    public class User
    {
        [JsonProperty("id")]
        public long UserId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }

        [JsonProperty("created_at")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("followers_count")]
        public int FollowersCount { get; set; }

        [JsonProperty("friends_count")]
        public int FriendsCount { get; set; }

        [JsonProperty("favourites_count")]
        public int FavouritesCount { get; set; }
    }
}
