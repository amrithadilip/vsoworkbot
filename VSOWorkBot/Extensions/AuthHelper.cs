using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using VSOWorkBot.Models;

namespace VSOWorkBot.Extensions
{
	public class AuthHelper
	{
		private readonly string url;

		private readonly CloudTable table;

		private readonly EntityResolver<BotTokenEntity> botTokenEntityResolver;

		public AuthHelper(IConfiguration configuration)
		{
			// Register the current API Url
			url = configuration["API_URL"];

			// Create a connection to the AzureTable storage for tokens
			var connectionString = configuration["VSOBotStorage"];
			CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
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