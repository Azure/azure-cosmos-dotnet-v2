namespace DocumentDB.Samples.Partitioning.DataModels
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a user profile to be stored within DocumentDB.
    /// </summary>
    public class UserProfile : IEquatable<UserProfile>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserProfile"/> class.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="userName">The user screen name.</param>
        /// <param name="primaryRegion">The primary region for the user</param>
        /// <param name="status">The user's status.</param>
        /// <param name="isPublic">The user's public status.</param>
        public UserProfile(
            string userId, 
            string userName, 
            Region primaryRegion = Region.UnitedStatesEast, 
            UserStatus status = UserStatus.Available,
            bool isPublic = true)
        {
            this.UserId = userId;
            this.UserName = userName;
            this.PrimaryRegion = primaryRegion;
            this.Status = status;
            this.IsPublic = isPublic;
            this.CreatedTime = DateTime.UtcNow;
            this.LastStatusModifiedTime = DateTime.UtcNow;
            this.FriendUserIds = new List<string>();
        }

        /// <summary>
        /// Gets or sets the ID for the user.
        /// </summary>
        [JsonProperty("id")]
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the primary region for the user.
        /// </summary>
        public Region PrimaryRegion { get; set; }

        /// <summary>
        /// Gets or sets the user specified name.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the status of the user.
        /// </summary>
        public UserStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the user's status message.
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Gets or sets the user's creation time.
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Gets or sets the user's last status modified time.
        /// </summary>
        public DateTime LastStatusModifiedTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user's information is public or private. 
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Gets or sets the profile image URL.
        /// </summary>
        public string ProfileImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the list of friends user IDs.
        /// </summary>
        public List<string> FriendUserIds { get; set; }

        /// <summary>
        /// Adds a friend to the user's profile.
        /// </summary>
        /// <param name="friend">The user to add.</param>
        public void AddFriend(UserProfile friend)
        {
            string friendUserId = friend.UserId;
            if (this.FriendUserIds.Contains(friendUserId))
            {
                this.FriendUserIds.Add(friendUserId);
            }
        }

        /// <summary>
        /// Removes a friend to the user's profile.
        /// </summary>
        /// <param name="friend">The user to remove.</param>
        public void RemoveFriend(UserProfile friend)
        {
            string friendUserId = friend.UserId;
            if (this.FriendUserIds.Contains(friendUserId))
            {
                this.FriendUserIds.Remove(friendUserId);
            }
        }

        /// <summary>
        /// Checks if the UserProfile object is the same as the specified argument.
        /// </summary>
        /// <param name="other">The other object</param>
        /// <returns>If this is equal to the other object.</returns>
        public bool Equals(UserProfile other)
        {
            return this.UserId == other.UserId;
        }
    }
}
