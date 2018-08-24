using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using CosmosDBResourceTokenProvider;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using System.Web;
using System.Net.Http.Headers;

namespace TokenProvider
{
    public static class Cosmos
    {
        private static ResourceTokenProvider resourceTokenProvider = ResourceTokenProvider.GetDefault();
        private static Dictionary<string, List<AppPermission>> RolePermissionsMap = CreateDefaultRolePermissionMap(); // TODO: Right now, only support 1 default role


        [FunctionName("Cosmos_TokenProvider")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "cosmos/token")]HttpRequestMessage req, TraceWriter log, ExecutionContext context)
        {
            string userId;
            JwtSecurityToken parsedToken;
            try
            {
                // "x-ms-client-principal-id" is the flag that this is a valid token
                userId = req.Headers.FirstOrDefault(h => string.Equals(h.Key, "x-ms-client-principal-id",StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? throw new InvalidOperationException("Principal id header was missing");
                var identifyProvider = req.Headers.FirstOrDefault(h => string.Equals(h.Key, "x-ms-client-principal-idp", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? throw new InvalidOperationException("Principal ipd header was missing");

                var token = req.Headers.FirstOrDefault(h => string.Equals(h.Key, $"x-ms-token-{identifyProvider}-id-token",StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault() ?? throw new InvalidOperationException("token header was missing");
                parsedToken = new JwtSecurityToken(token) ?? throw new InvalidOperationException("Token was invalid");
            }
            catch (Exception e)
            {
                log.Error($"Authentication issue", e);
                log.Info($"Headers {HeadersToString(req.Headers)}");
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, $"Could not authenticate user. Reference {context.InvocationId} for details.");
            }

            List<PermissionToken> permissionTokens = new List<PermissionToken>();

            var role = "Default"; // TODO: make this able to be added from claim
            try
            {
                foreach (AppPermission p in RolePermissionsMap[role])
                {
                    var partitionKey = parsedToken.Claims.Where(c => c.Type == p.PartitionKeyPropertyName).FirstOrDefault()?.Value;
                    PermissionToken permissionToken = await resourceTokenProvider.GetToken(ResourceTokenProvider.Sanitize(p.ToString()), p.DatabaseId, new Uri(p.ResourceId, UriKind.Relative), role, p.PermissionMode, partitionKey == null ? null : new PartitionKey(partitionKey));
                    log.Info($"User[{userId}] assigned token[{permissionToken.Id}] in role[{role}] with permissions(resource[{p.ResourceId}], PermissionMode[{(p.PermissionMode == PermissionMode.All ? "ALL" : "READ/")}{(partitionKey != null ? $", {partitionKey}" : "")}");
                    permissionTokens.Add(permissionToken);
                }
                return req.CreateResponse(permissionTokens);
            }
            catch (Exception e)
            {
                log.Error($"Could not create token for user[{userId}]", e);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Server error. Reference {context.InvocationId} for details.");
            }
        }

        private static string GetUserId(JwtSecurityToken jwtToken)
        {
            // This only works for AAD for now...
            return jwtToken.Claims.First(c => c.Type == "upn").Value;
        }

        private static string HeadersToString(HttpRequestHeaders headers)
        {
            string output = "";
            foreach(var h in headers)
            {
                foreach (var h2 in h.Value)
                {
                    output += $"{h.Key}: {h2.Substring(0, Math.Min(30, h2.Length))}\n";
                }
            }
            return output;
        }

        private static Dictionary<string, List<AppPermission>> CreateDefaultRolePermissionMap()
        {
            var d = new Dictionary<string, List<AppPermission>>();
            var l = new List<AppPermission>();
            var n = "Default";

            // Handle single value case
            string value = Environment.GetEnvironmentVariable("TOKEN_PROVIDER_COSMOS_DEFAULT");
            if (!string.IsNullOrEmpty(value))
            {
                l.Add(AppPermission.ParseFromString(value));
            }

            // Handle multiple value case
            string keysString = Environment.GetEnvironmentVariable("TOKEN_PROVIDER_COSMOS_DEFAULT_KEYS");
            if (!string.IsNullOrEmpty(keysString))
            {
                string[] keys = keysString.Split(';');
                foreach (string key in keys)
                {
                    try
                    {
                        string v = Environment.GetEnvironmentVariable(key);
                        l.Add(AppPermission.ParseFromString(v));
                    } 
                    catch (Exception e)
                    {
                        // TODO: should probably log or something if there is a bad token
                    }
                }
            }

            d.Add(n, l);
            return d;
        }
    }

    public class AppPermission
    {
        public string ResourceId { get; set; }
        public string DatabaseId { get; set; }
        public string PartitionKeyPropertyName { get; set; }
        public PermissionMode PermissionMode { get; set; }

        public static AppPermission ParseFromString(string permissionString)
        {
            var p = new AppPermission();
            // String format: path/to/resource[(partitionKey)][{permission}]
            var databaseSectionStart = permissionString.IndexOf("/");
            var databaseSectionEnd = permissionString.IndexOf("/", databaseSectionStart + 1);
            var partitionKeySectionTokenStart = permissionString.IndexOf('(');
            var partitionKeySectionTokenEnd = permissionString.IndexOf(')');
            var permissionSectionStart = permissionString.IndexOf('{');
            var permissionSectionEnd = permissionString.IndexOf('}');
            var resourceSectionEnd = partitionKeySectionTokenStart >= 0 ? partitionKeySectionTokenStart : permissionSectionStart >= 0 ? permissionSectionStart : permissionString.Length - 1;

            p.ResourceId = permissionString.Substring(0, resourceSectionEnd);

            // This should always be true, but just in case there is a bad string, just ignore it here
            if (databaseSectionStart >= 0 && databaseSectionEnd >= databaseSectionStart)
            {
                p.DatabaseId = permissionString.Substring(databaseSectionStart + 1, databaseSectionEnd - (databaseSectionStart + 1));
            }

            if (partitionKeySectionTokenStart >= 0 && partitionKeySectionTokenEnd > partitionKeySectionTokenStart)
            {
                p.PartitionKeyPropertyName = permissionString.Substring(partitionKeySectionTokenStart + 1, partitionKeySectionTokenEnd - (partitionKeySectionTokenStart + 1));
            }

            if (permissionSectionStart >= 0 && permissionSectionEnd > permissionSectionStart)
            {
                var permissionModeString = permissionString.Substring(permissionSectionStart + 1, permissionSectionEnd - (permissionSectionStart + 1));
                p.PermissionMode = permissionModeString == "All" ? PermissionMode.All : PermissionMode.Read;
            }

            return p;
        }

        public override string ToString()
        {
            var partitionKeySection = !string.IsNullOrEmpty(PartitionKeyPropertyName) ? $"({PartitionKeyPropertyName})" : "";
            var permissionSection = PermissionMode == PermissionMode.All ? "{All}" : "{Read}";

            return $"{ResourceId}{partitionKeySection}{permissionSection}";
        }
    }
}
