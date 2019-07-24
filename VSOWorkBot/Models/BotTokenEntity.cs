using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace VSOWorkBot.Models
{
	public class BotTokenEntity : TableEntity
	{
		public DateTime ExpiresAt { get; set; }

		public string Token { get; set; }
	}
}