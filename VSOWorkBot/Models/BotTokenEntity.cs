namespace VSOWorkBot.Models
{
using System;
using Microsoft.WindowsAzure.Storage.Table;

public class BotTokenEntity : TableEntity
{
    public DateTime ExpiresAt {
        get;
        set;
    }

    public string Token {
        get;
        set;
    }
}
}