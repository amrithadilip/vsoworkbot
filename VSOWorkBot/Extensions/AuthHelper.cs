namespace VSOWorkBot.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Newtonsoft.Json;
    using VSOWorkBot.Models;

    public class AuthHelper
    {
        private readonly string url;

        private readonly string clientId;

        private readonly string clientSecret;

        private readonly string scopes;

        private readonly CloudTable table;

        private readonly EntityResolver<BotTokenEntity> botTokenEntityResolver;

        private readonly string authorizeUrl = "https://app.vssps.visualstudio.com/oauth2/authorize";

        private readonly string tokenUrl = "https://app.vssps.visualstudio.com/oauth2/token";

        public AuthHelper(IConfiguration configuration)
        {
            // Register the current API Url
            url = configuration["API_URL"];
            clientId = configuration["VSTS_CLIENT_ID"];
            clientSecret = configuration["VSTS_CLIENT_SECRET"];
            scopes = configuration["VSTS_SCOPES"];

            // Create a connection to the AzureTable storage for tokens
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(configuration["VSOBotStorage"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            table = tableClient.GetTableReference("BotAccessTokens");
            table.CreateIfNotExistsAsync();

            botTokenEntityResolver = (partitionKey, rowKey, timestamp, properties, etag) =>
                                     new BotTokenEntity
                                     {
                                         PartitionKey = partitionKey,
                                         RowKey = rowKey,
                                         Timestamp = timestamp,
                                         ETag = etag,
                                         ExpiresAt = properties["ExpiresAt"].DateTime.Value,
                                         Token = properties["Token"].StringValue
                                     };
        }

        public string GetSignInUrl(string conversationId, string userId)
        {
            return $"{url}/signin/{conversationId}/{userId}";
        }

        public string GetAuthorizeUrl()
        {
            var encodedScopes = HttpUtility.UrlEncode(scopes);
            var encodedRedirectUrl = HttpUtility.UrlEncode($"{url}/oauth-callback");
            return $"{authorizeUrl}?client_id={clientId}&response_type=Assertion&scope={encodedScopes}&redirect_uri={encodedRedirectUrl}";
        }

        public async Task<VSOToken> GetTokenAsync(string code)
        {
            using (var client = new HttpClient())
            {
                var body = new Dictionary<string, string> {
                {
                    "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
                },
                { "client_assertion", clientSecret },
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", code },
                { "redirect_uri", $"{url}/oauth-callback" }
            };
                var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(body));
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<VSOToken>(content);
            }
        }

        public Task<string> GetTokenAsync(Activity activity)
        {
            return this.GetTokenAsync(activity.Conversation.Id, activity.From.Id);
        }

        public async Task<string> GetTokenAsync(string conversationId, string userId)
        {
            // Query all entities in the table
            var activityEntities = await table.ExecuteAsync(TableOperation.Retrieve<BotTokenEntity>(conversationId, userId, botTokenEntityResolver));
            return (activityEntities.Result as BotTokenEntity).Token;
        }

        public async Task SaveTokenAsync(string conversationId, string userId, string token)
        {
            var entry = new BotTokenEntity { PartitionKey = conversationId, RowKey = userId, Token = token, ExpiresAt = DateTime.UtcNow.AddHours(1) };
            TableOperation insertActivity = TableOperation.Insert(entry);
            await table.ExecuteAsync(insertActivity);
        }

        public Task SignOutAsync(Activity activity)
        {
            return this.SignOutAsync(activity.Conversation.Id, activity.From.Id);
        }

        public async Task SignOutAsync(string conversationId, string userId)
        {
            TableOperation deleteActivity = TableOperation.Delete(new TableEntity { PartitionKey = conversationId, RowKey = userId });
            await table.ExecuteAsync(deleteActivity);
        }

    }
}