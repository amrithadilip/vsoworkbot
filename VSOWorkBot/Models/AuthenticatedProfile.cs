using System;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace VSOWorkBot.Models
{
	public class AuthenticationTableEntity : TableEntity
	{
		public DateTime ExpiresAt { get; set; }

		public string SerializedProfile { get; set; }

		public string SerializedToken { get; set; }

		public static EntityResolver<AuthenticationTableEntity> Resolver = (partitionKey, rowKey, timestamp, properties, etag) =>
			new AuthenticationTableEntity
			{
				PartitionKey = partitionKey,
				RowKey = rowKey,
				Timestamp = timestamp,
				ETag = etag,
				ExpiresAt = properties["ExpiresAt"].DateTime.Value,
				SerializedToken = properties["SerializedToken"].StringValue,
				SerializedProfile = properties["SerializedProfile"].StringValue
			};
	}

	public class AuthenticatedProfile
	{
		public string UserId { get; }

		public string ConversationId { get; }

		public UserProfile Profile { get; set; }

		public VSOToken Token { get; set; }

		public AuthenticatedProfile()
		{

		}

		public AuthenticatedProfile(AuthenticationTableEntity authenticationEntity)
		{
			if (authenticationEntity == null || String.IsNullOrEmpty(authenticationEntity.SerializedProfile) || String.IsNullOrEmpty(authenticationEntity.SerializedToken))
			{
				throw new ArgumentNullException($"Unable to get a saved authentication profile the current user activity. Please re-authenticate");
			}
			var token = JsonConvert.DeserializeObject<VSOToken>(authenticationEntity.SerializedToken);
			var profile = JsonConvert.DeserializeObject<UserProfile>(authenticationEntity.SerializedProfile);

			ConversationId = authenticationEntity.PartitionKey;
			UserId = authenticationEntity.RowKey;
			Profile = profile;
			Token = token;
		}
	}
}