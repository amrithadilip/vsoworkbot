using Newtonsoft.Json;

namespace VSOWorkBot.Models
{
	public class UserProfile
	{
		[JsonProperty("displayName")]
		public string DisplayName { get; set; }

		[JsonProperty("emailAddress")]
		public string EmailAddress { get; set; }

		[JsonProperty("id")]
		public string ID { get; set; }
	}
}