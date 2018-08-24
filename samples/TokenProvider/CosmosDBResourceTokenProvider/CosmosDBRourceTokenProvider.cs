using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Web;

namespace CosmosDBResourceTokenProvider
{
    public class ResourceTokenProvider
    {
        private static ResourceTokenProvider _default;
        private static DateTime BeginningOfTime = new DateTime(2017, 1, 1);
        private DocumentClient Client;
        private Dictionary<string, (DateTime, Permission)> permissionsCache = new Dictionary<string, (DateTime, Permission)>();

        public static ResourceTokenProvider GetDefault()
        {
            if (_default == null)
            {
                var endpoint = new Uri(Environment.GetEnvironmentVariable("TOKEN_PROVIDER_COSMOS_ENDPOINT") ?? throw new InvalidOperationException("Missing environment variable: TOKEN_PROVIDER_COSMOS_ENDPOINT"));
                var masterKey = Environment.GetEnvironmentVariable("TOKEN_PROVIDER_COSMOS_MASTERKEY") ?? throw new InvalidOperationException("Missing environment variable : TOKEN_PROVIDER_COSMOS_MASTERKEY");
                var client = new DocumentClient(endpoint, masterKey);
                _default = new ResourceTokenProvider(client);
            }
            return _default;
        }

        public ResourceTokenProvider(DocumentClient client)
        {
            Client = client;
        }

        public async Task<PermissionToken> GetToken(string permissionId, string databaseId, Uri resourceId, string roleName, PermissionMode permissionMode, PartitionKey partitionKey = null, int expireInSeconds = 3600)
        {
            return await GetOrCreatePermission(permissionId, databaseId, roleName, resourceId, expireInSeconds, partitionKey, permissionMode);
        }

        private async Task<PermissionToken> GetOrCreatePermission(string permissionId, string databaseId, string userId, Uri resource, int expireInSeconds, PartitionKey partitionKey = null, PermissionMode permissionMode = PermissionMode.All)
        {
            Permission permission = null;
            User user = await CreateUserIfNotExistAsync(databaseId, userId);
            int? expires = null;

            // Check the cache
            if(permissionsCache.ContainsKey(permissionId))
            {
                DateTime cacheExpiry;
                (cacheExpiry, permission) = permissionsCache[permissionId];
                expires = Convert.ToInt32(cacheExpiry.Subtract(BeginningOfTime).TotalSeconds);
            }

            //// TODO: This doesn't seem to like our special characters, it never finds it
            //if (permission == null)
            //{
            //    try
            //    {
            //        var temp = $"dbs/{databaseId}/users/{userId}/permissions/{permissionId}";
            //        var permissionLink = UriFactory.CreatePermissionUri(databaseId, userId, permissionId);
            //        permission = await Client.ReadPermissionAsync(temp);
            //        permissionsCache.Add(permissionId, (DateTime.Now.AddHours(1), permission));
            //    }
            //    catch (Exception e)
            //    {
            //        // TODO: something useful
            //    }
            //}

            if (permission == null)
            {
                try
                {
                    // Create a new permission
                    Permission p;
                    p = new Permission
                    {
                        PermissionMode = permissionMode,
                        ResourceLink = resource.ToString(),
                        ResourcePartitionKey = partitionKey, // If not set, everyone can access every document
                        Id = permissionId ?? Guid.NewGuid().ToString() //needs to be unique for a given user
                    };
                    // TODO: Did not like expire time so have disabled
                    // var ro = new RequestOptions
                    // {
                    //     ResourceTokenExpirySeconds = expires
                    // };
                    permission = await Client.CreatePermissionAsync(user.SelfLink, p);
                    expires = Convert.ToInt32(DateTime.UtcNow.Subtract(BeginningOfTime).TotalSeconds) + expireInSeconds;
                }
                catch (Exception e)
                {
                    // TODO: something useful
                }
            }

            // Last option, it's possible that permission already exists on that resource, but with a different name. :(
            // Look it up from the feed
            if(permission == null)
            {
                // Fetch the latest permission feed for the database and role
                FeedResponse<Permission> permissionFeed = await Client.ReadPermissionFeedAsync(UriFactory.CreateUserUri(databaseId, user.Id));
                var permissions = permissionFeed.ToList();

                // Save the permissions list to the in memory cache so we don't have to do this very often
                var cacheExpiry = DateTime.Now.AddHours(1);
                foreach(var p in permissionFeed)
                {
                    if(permissionsCache.ContainsKey(p.Id))
                    {
                        permissionsCache.Remove(p.Id);
                    }
                    permissionsCache.Add(p.Id, (cacheExpiry, p));
                }

                // Look up the specified permission
                permission = permissions.Where(p => p.ResourceLink == resource.ToString() && partitionKey?.ToString() == p.ResourcePartitionKey?.ToString()).FirstOrDefault();
                expires = Convert.ToInt32(DateTime.UtcNow.Subtract(BeginningOfTime).TotalSeconds) + expireInSeconds;
            }
            
            if(permission == null)
            {
                throw new InvalidOperationException("Could not find or create a permission");
            }

            return new PermissionToken()
            {
                Token = permission.Token,
                Expires = expires ?? 0,
                UserId = userId,
                Id = permission.Id,
                ResourceId = permission.ResourceLink,
            };
        }

        private async Task<User> CreateUserIfNotExistAsync(string databaseId, string userId)
        {
            try
            {
                return await Client.ReadUserAsync(UriFactory.CreateUserUri(databaseId, userId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var user = await Client.CreateUserAsync(UriFactory.CreateDatabaseUri(databaseId), new User { Id = userId });
                    return user;
                }
                else throw e;
            }

        }

        public static string Sanitize(string input)
        {
            return HttpUtility.UrlEncode(input);
        }
    }

    public class PermissionToken
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }
        [JsonProperty(PropertyName = "expires")]
        public int Expires { get; set; }
        [JsonProperty(PropertyName = "userid")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "resourceId")]
        public string ResourceId { get; set; }
    }

}
