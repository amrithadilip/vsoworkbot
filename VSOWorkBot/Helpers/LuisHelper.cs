namespace VSOWorkBot.Helpers
{
    using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.AI.Luis;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using VSOWorkBot.Models;

    public static class LuisHelper
    {
        private const string luisURL = @"https://{0}/{1}?subscription-key={2}&timezoneOffset=-480&verbose=true&q={3}";

        /// <summary>
        /// You only need to say the invocation phrase when starting the skill from outside. Sometimes, users will say it when they're already in the app, which screws up LUIS.
        /// This can help fix that.
        /// </summary>
        private static readonly Regex removeInvocationPhrase = new Regex($"^[a-zA-Z]{{0,5}} (cortana) (for|to|about) (.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static async Task<WorkItemDetails> ExecuteLuisQuery(IBotTelemetryClient telemetryClient, IConfiguration configuration, ILogger logger, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var workItemDetails = new WorkItemDetails();

            try
            {
                // Create the LUIS settings from configuration.
                var luisApplication = new LuisApplication(
                    configuration["LuisAppId"],
                    configuration["LuisAPIKey"],
                    "https://" + configuration["LuisAPIHostName"]
                );

                var luisPredictionOptions = new LuisPredictionOptions()
                {
                    TelemetryClient = telemetryClient,
                };

                var recognizer = new LuisRecognizer(luisApplication, luisPredictionOptions);

                // The actual call to LUIS
                var recognizerResult = await recognizer.RecognizeAsync(turnContext, cancellationToken);

                if (recognizerResult == null)
                {
                    logger.LogError($"LUIS returned null for this turn.");
                }
                var (intent, score) = recognizerResult.GetTopScoringIntent();
                if (intent == "getvsoitem")
                {
                    // We need to get the result from the LUIS JSON which at every level returns an array.
                    workItemDetails.workItemStatus = WorkItemStatus.Active;
                    workItemDetails.workItemId = "dd"; // recognizerResult.Entities["From"]?.FirstOrDefault()?["Airport"]?.FirstOrDefault()?.FirstOrDefault()?.ToString();
                }
            }
            catch (Exception e)
            {
                logger.LogWarning($"LUIS Exception: {e.Message} Check your LUIS configuration.");
            }

            return workItemDetails;
        }

        public static async Task<LuisResult> GetLuisResult(IConfiguration configuration, string message)
        {
            LuisResult luisResult = null;
            using (var httpClient = new HttpClient())
            {
                var luisRequest = string.Format(luisURL, configuration["LuisAPIHostName"], configuration["LuisAppId"], configuration["LuisAPIKey"], CleanUpMessage(message));
                var response = await httpClient.GetAsync(luisRequest).ConfigureAwait(false);
                luisResult = JsonConvert.DeserializeObject<LuisResult>(await response.Content.ReadAsStringAsync());
            }
            
            return luisResult;
        }

        private static string CleanUpMessage(string message)
        {
            //If message.Text is null (which happens when someone just says "Ask Help Desk"), LUIS will choke on it.
            //Change it to something that maps to the None intent.
            if (string.IsNullOrWhiteSpace(message))
                return "they didn't say anything";

            message = removeInvocationPhrase.Replace(message, "$2");
            return message.ToLower().Trim().TrimEnd('.');
        }
    }
}
