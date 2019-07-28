namespace VSOWorkBot.Dialogs
{
	using Microsoft.Bot.Builder;
	using Microsoft.Bot.Builder.Dialogs;
	using Microsoft.Bot.Schema;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.Logging;
	using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
	using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
	using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
	using System.Threading.Tasks;
	using VSOWorkBot.Extensions;
	using VSOWorkBot.Helpers;
	using VSOWorkBot.Interfaces;
	using VSOWorkBot.Models;

	public class GetWorkItemDialog : CancelAndLogoutDialog
	{
		private IVsoApiController vsoApiController;

		private ILogger logger;

		private AuthHelper authHelper;

		public GetWorkItemDialog(IConfiguration configuration, ILogger logger, AuthHelper authHelper, IVsoApiController vsoApiController)
			: base(nameof(GetWorkItemDialog), authHelper, configuration)
		{
            PromptValidator<Activity> promptValidator = new PromptValidator<Activity>(PromptValidatorStep);
            AddDialog(new ActivityInfoPrompt(promptValidator)
            {
                TelemetryClient = TelemetryClient,
            });
			AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
			{
                PromptForProjectCollectionAndProjectName,
				FinalStepAsync,
			})
			{
				TelemetryClient = TelemetryClient,
			});

			this.authHelper = authHelper;
			this.logger = logger;
			this.vsoApiController = vsoApiController;

			// The initial child Dialog to run.
			InitialDialogId = nameof(WaterfallDialog);
		}

		private async Task<DialogTurnResult> PromptForProjectCollectionAndProjectName(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
            var cardText = await CardProvider.GetCardText("ProjectInformationCard").ConfigureAwait(false);
            var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
            return await stepContext.PromptAsync(nameof(ActivityInfoPrompt), new PromptOptions { Prompt = replyActivity }, cancellationToken);

        }

        private Task<bool> PromptValidatorStep<Activity>(PromptValidatorContext<Activity> promptContext, CancellationToken cancellationToken)
        {
            if (promptContext.Context.Activity.Value == null && promptContext.AttemptCount < 3)
            {
                Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			if (stepContext.Result == null || !(stepContext.Result is Activity) || !(stepContext.Options is RecognizerResult))
			{
				return await stepContext.EndDialogAsync(null, cancellationToken);
			}

            var recognizerResult = (RecognizerResult)stepContext.Options;
            // We need to get the result from the LUIS JSON which at every level returns an array.
            WorkItemInput workItemInput = new WorkItemInput();
            Match match = Regex.Match(recognizerResult.Entities["id"].ToString(), @"(\d+)");
            if (match.Success)
            {
                workItemInput.WorkItemId = match.Groups[1].Value;
            }

            try
			{
                var authenticatedProfile = await this.authHelper.GetAuthenticatedProfileAsync(stepContext.Context, cancellationToken).ConfigureAwait(false);
                var activity = stepContext.Result as Activity;
                var projectInfo = JObject.Parse(activity.Value.ToString());
				WorkItem workItem = await vsoApiController.GetWorkItemAsync(workItemInput.WorkItemId, projectInfo["ProjectCollection"].ToString(), projectInfo["ProjectName"].ToString(), authenticatedProfile.Token.AccessToken).ConfigureAwait(false);


                var replaceInfo = new Dictionary<string, string>();
				replaceInfo.Add("{{bugId}}", workItem.Id.ToString());
                replaceInfo.Add("{{bugUrl}}", $"https://{projectInfo["ProjectCollection"].ToString()}.visualstudio.com/{projectInfo["ProjectName"].ToString()}/_workitems/edit/{workItemInput.WorkItemId}");
                if (workItem.Fields.Count == 0)
				{
					this.logger.LogWarning($"Work item details are miising for {workItem.Id}");
				}

				if (workItem.Fields.TryGetValue("System.Title", out object title))
				{
					replaceInfo.Add("{{bugTitle}}", title.ToString());
				}

				if (workItem.Fields.TryGetValue("System.State", out object status))
				{
					replaceInfo.Add("{{bugStatus}}", status.ToString());
				}

                if (workItem.Fields.TryGetValue("System.ChangedDate", out object lastUpdatedDate))
                {
                    replaceInfo.Add("{{lastUpdated}}", lastUpdatedDate.ToString());
                }
                
				var cardText = await CardProvider.GetCardText("BugDetailsCard", replaceInfo).ConfigureAwait(false);
				var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
				await stepContext.Context.SendActivityAsync(replyActivity, cancellationToken);
				return await stepContext.EndDialogAsync(workItemInput, cancellationToken);
			}
			catch (Exception)
			{
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Sorry something went wrong when fetching {workItemInput.WorkItemId}."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
			}
		}
	}
}
