namespace VSOWorkBot.Helpers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public static class CardProvider
    {
        public static async Task<string> GetCardText(string cardName, IDictionary<string, string> replaceInfo = null)
        {
            string cardJson = await GetCardText(cardName).ConfigureAwait(false);
            if (string.IsNullOrEmpty(cardJson))
                return string.Empty;

            if (replaceInfo == null)
            {
                return cardJson;
            }

            foreach (var replaceKvp in replaceInfo)
            {
                cardJson = cardJson.Replace(replaceKvp.Key, replaceKvp.Value);
            }

            return cardJson;
        }

        public static async Task<string> GetCardText(string cardName)
        {
            return await File.ReadAllTextAsync($@".\Cards/{cardName}.json");
        }
    }
}
