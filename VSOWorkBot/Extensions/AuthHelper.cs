namespace VSOWorkBot.Extensions
{
	using System.Collections.Generic;
	using System.Net.Http.Headers;
	using System.Net.Http;
	using System.Threading.Tasks;
	using System.Threading;
	using System.Web;
	using System;
	using Microsoft.Bot.Builder;
	using Microsoft.Bot.Schema;
	using Microsoft.Extensions.Configuration;
	using Microsoft.WindowsAzure.Storage.Table;
	using Microsoft.WindowsAzure.Storage;
	using Newtonsoft.Json;
	using VSOWorkBot.Models;

	public class AuthHelper
	{
		private readonly string url;

		private readonly string clientId;

		private readonly string clientSecret;

		private readonly string scopes;

		private readonly CloudTable table;

		private readonly string authorizeUrl = "https://app.vssps.visualstudio.com/oauth2/authorize";

		private readonly string tokenUrl = "https://app.vssps.visualstudio.com/oauth2/token";

		private readonly string profileUrl = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me";

		protected readonly IStatePropertyAccessor<AuthenticatedProfile> profileAccessor;

		public AuthHelper(IConfiguration configuration, UserState userState)
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

			profileAccessor = userState.CreateProperty<AuthenticatedProfile>("AuthenticatedProfile");
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

		public async Task<VSOToken> GetTokenAsync(string codeOrToken, bool refresh = false)
		{
			using (var client = new HttpClient())
			{
				var body = new Dictionary<string, string> {
						{
							"client_assertion_type",
							"urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
						},
						{ "client_assertion", clientSecret },
						{ "grant_type", refresh? "refresh_token": "urn:ietf:params:oauth:grant-type:jwt-bearer" },
						{ "assertion", codeOrToken },
						{ "redirect_uri", $"{url}/oauth-callback" }
					};
				var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(body));
				var content = await response.Content.ReadAsStringAsync();
				return JsonConvert.DeserializeObject<VSOToken>(content);
			}
		}

		public async Task<AuthenticatedProfile> GetAuthenticatedProfileAsync(ITurnContext context, CancellationToken cancellationToken)
		{
			var localProfile = await profileAccessor.GetAsync(context, null, cancellationToken);
			if (localProfile != null)
			{
				// TODO: Check expiriy of the token and profile;
				return localProfile;
			}
			// Query all entities in the table
			var activity = context.Activity;
			var activityEntities = await table.ExecuteAsync(TableOperation.Retrieve<AuthenticationTableEntity>(activity.Conversation.Id, activity.From.Id, AuthenticationTableEntity.Resolver));
			var authenticationEntity = activityEntities.Result as AuthenticationTableEntity;
			try
			{
				var authenticatedProfile = await DeserializeProfile(authenticationEntity);
				await profileAccessor.SetAsync(context, authenticatedProfile, cancellationToken).ConfigureAwait(false);
				return authenticatedProfile;
			}
			catch
			{
				// TODO Don't catch Pokemons :P
				return null;
			}
		}

		public async Task SaveAuthenticatedProfileAsync(string conversationId, string userId, VSOToken token, UserProfile profile)
		{
			TableOperation insertActivity = TableOperation.InsertOrReplace(new AuthenticationTableEntity
			{
				PartitionKey = conversationId,
				RowKey = userId,
				SerializedToken = JsonConvert.SerializeObject(token),
				SerializedProfile = JsonConvert.SerializeObject(profile),
				ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
			});
			await table.ExecuteAsync(insertActivity);
		}

		public Task SaveAuthenticatedProfileAsync(AuthenticatedProfile authenticatedProfile)
		{
			return SaveAuthenticatedProfileAsync(authenticatedProfile.ConversationId, authenticatedProfile.UserId, authenticatedProfile.Token, authenticatedProfile.Profile);
		}

		public async Task SignOutAsync(string conversationId, string userId)
		{
			TableOperation deleteActivity = TableOperation.Delete(new TableEntity { PartitionKey = conversationId, RowKey = userId });
			await table.ExecuteAsync(deleteActivity);
		}

		public Task SignOutAsync(Activity activity)
		{
			return SignOutAsync(activity.Conversation.Id, activity.From.Id);
		}

		public async Task<UserProfile> GetUserProfileAsync(VSOToken token)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
				var response = await client.GetAsync(profileUrl);
				var content = await response.Content.ReadAsStringAsync();
				return JsonConvert.DeserializeObject<UserProfile>(content);
			}
		}

		private async Task<AuthenticatedProfile> DeserializeProfile(AuthenticationTableEntity authenticationTableEntity)
		{
			var authenticatedProfile = new AuthenticatedProfile(authenticationTableEntity);

			// If the token is closer to 5 minutes away from expiry
			// Then update the token and the userProfile.
			if (authenticationTableEntity.ExpiresAt <= DateTime.UtcNow.AddMinutes(-5))
			{
				authenticatedProfile.Token = await GetTokenAsync(authenticatedProfile.Token.RefreshToken, refresh: true);
				authenticatedProfile.Profile = await GetUserProfileAsync(authenticatedProfile.Token);
				await SaveAuthenticatedProfileAsync(authenticatedProfile);
			}

			return authenticatedProfile;
		}
	}
}