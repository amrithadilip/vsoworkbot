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
    using WorkItemType = Models.WorkItemType;

    public class CreateWorkItemDialog : CancelAndLogoutDialog
	{
		private IVsoApiController vsoApiController;

		private ILogger logger;

		private AuthHelper authHelper;

        protected readonly IStatePropertyAccessor<WorkItemInput> workItemInputAccessor;

        public CreateWorkItemDialog(IConfiguration configuration, ILogger logger, AuthHelper authHelper, IVsoApiController vsoApiController, UserState userState)
			: base(nameof(CreateWorkItemDialog), authHelper, configuration)
		{
            PromptValidator<Activity> promptValidator = new PromptValidator<Activity>(PromptValidatorStep);
            AddDialog(new ActivityInfoPrompt(promptValidator));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
			AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
			{
                PromptForProjectCollectionAndProjectName,
                GetProjectInfo,
                PromptForWorkItemDetails,
                FinalStepAsync,
			})
			{
				TelemetryClient = TelemetryClient,
			});

			this.authHelper = authHelper;
			this.logger = logger;
			this.vsoApiController = vsoApiController;
            this.workItemInputAccessor = userState.CreateProperty<WorkItemInput>("InputForCreateWorItem");

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
		}

        private async Task<DialogTurnResult> PromptForProjectCollectionAndProjectName(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var cardText = await CardProvider.GetCardText("ProjectInformationCard").ConfigureAwait(false);
            var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
            return await stepContext.PromptAsync(nameof(ActivityInfoPrompt), new PromptOptions { Prompt = replyActivity }, cancellationToken);
        }

        private async Task<DialogTurnResult> GetProjectInfo(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!(stepContext.Options is RecognizerResult))
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            var recognizerResult = (RecognizerResult)stepContext.Options;
            var workItemType = Utilities.GetWorkItemType(recognizerResult.Entities);
            var workItemInput = new WorkItemInput { WorkItemType = workItemType };

            var activity = stepContext.Result as Activity;
            var projectInfo = JObject.Parse(activity.Value.ToString());
            workItemInput.ProjectCollection = projectInfo["ProjectCollection"].ToString();
            workItemInput.ProjectName = projectInfo["ProjectName"].ToString();
            await workItemInputAccessor.SetAsync(stepContext.Context, workItemInput, cancellationToken).ConfigureAwait(false);
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> PromptForWorkItemDetails(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var workItemInput = await workItemInputAccessor.GetAsync(stepContext.Context, cancellationToken: cancellationToken).ConfigureAwait(false);
            var activity = stepContext.Result as Activity;

            var replaceInfo = new Dictionary<string, string>();
            replaceInfo.Add("{{workitemType}}", workItemInput.WorkItemType.ToString());
            var cardText = await CardProvider.GetCardText("CreateWorkItemCard", replaceInfo).ConfigureAwait(false);
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
			if (stepContext.Result == null || !(stepContext.Result is Activity))
			{
				return await stepContext.EndDialogAsync(null, cancellationToken);
			}

            var workItemInput = await workItemInputAccessor.GetAsync(stepContext.Context, cancellationToken: cancellationToken).ConfigureAwait(false);
            try
			{
                var authenticatedProfile = await this.authHelper.GetAuthenticatedProfileAsync(stepContext.Context, cancellationToken).ConfigureAwait(false);
                var activity = stepContext.Result as Activity;
                var workItemInfo = JObject.Parse(activity.Value.ToString());

                var fieldToValueMappings = GetFieldToValueMappings(workItemInfo["WorkItemTitle"].ToString(), workItemInfo["WorkItemDescription"].ToString(), workItemInfo["Priority"].ToString(), workItemInfo["AreaPath"].ToString(), workItemInfo["IterationPath"].ToString());
                WorkItem workItem = await vsoApiController.CreateWorkItemAsync(workItemInput.ProjectCollection, workItemInput.ProjectName, fieldToValueMappings, workItemInput.WorkItemType, authenticatedProfile.Token.AccessToken).ConfigureAwait(false);

                if (workItem == null)
                {
                    this.logger.LogError($"Error while creating the {workItemInput.WorkItemType}");
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Sorry something went wrong when creating {workItemInput.WorkItemType}."), cancellationToken);
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }

                var replaceInfo = new Dictionary<string, string>();
				replaceInfo.Add("{{workitemId}}", workItem.Id.ToString());
                replaceInfo.Add("{{workitemUrl}}", $"https://{workItemInput.ProjectCollection}.visualstudio.com/{workItemInput.ProjectName}/_workitems/edit/{workItem.Id}");
                if (workItem.Fields.Count == 0)
				{
					this.logger.LogWarning($"Work item details are miising for {workItem.Id}");
				}

                if (workItem.Fields.TryGetValue("System.WorkItemType", out object workItemType))
                {
                    replaceInfo.Add("{{workitemType}}", workItemType.ToString());
                }

                if (workItem.Fields.TryGetValue("System.Title", out object title))
				{
					replaceInfo.Add("{{workitemTitle}}", title.ToString());
				}

				if (workItem.Fields.TryGetValue("System.State", out object status))
				{
					replaceInfo.Add("{{workitemStatus}}", status.ToString());
				}

                if (workItem.Fields.TryGetValue("System.ChangedDate", out object lastUpdatedDate))
                {
                    replaceInfo.Add("{{lastUpdated}}", lastUpdatedDate.ToString());
                }
                
				var cardText = await CardProvider.GetCardText("WorkItemDetailsCard", replaceInfo).ConfigureAwait(false);
				var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
                replyActivity.Text = $"Created {workItemInput.WorkItemType} successfully";
				await stepContext.Context.SendActivityAsync(replyActivity, cancellationToken);
				return await stepContext.EndDialogAsync(workItemInput, cancellationToken);
			}
			catch (Exception)
			{
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Sorry something went wrong when creating {workItemInput.WorkItemType}."), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
			}
		}

        private static IDictionary<string, string> GetFieldToValueMappings(string title, string description, string priority = null, string projectAreaPath = null, string iterationPath = null)
        {
            var fieldToValueMappings = new Dictionary<string, string>()
            {
                { "/fields/System.Title", title },
                { "/fields/System.Description", description },
            };

            if (!string.IsNullOrEmpty(priority))
            {
                fieldToValueMappings.Add("/fields/Microsoft.VSTS.Common.Priority", priority);
            }

            if (!string.IsNullOrEmpty(projectAreaPath))
            {
                fieldToValueMappings.Add("/fields/System.AreaPath", projectAreaPath);
            }

            if (!string.IsNullOrEmpty(iterationPath))
            {
                fieldToValueMappings.Add("/fields/System.IterationPath", iterationPath);
            }

            return fieldToValueMappings;
        }
	}
}
