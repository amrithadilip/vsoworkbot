namespace VSOWorkBot.Dialogs
{
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
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

        public GetWorkItemDialog(IConfiguration configuration, ILogger logger, IBotTelemetryClient telemetryClient, UserState userState, AuthHelper authHelper, IVsoApiController vsoApiController)
            : base(nameof(GetWorkItemDialog), authHelper, configuration)
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetProjectCollectionAndProjectName,
                FinalStepAsync,
            })
            {
                TelemetryClient = telemetryClient,
            });

            this.authHelper = authHelper;
            this.logger = logger;
            this.vsoApiController = vsoApiController;

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetProjectCollectionAndProjectName(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("What is the project collection and project name? \nProvide the response in the format msasg;teams") }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(stepContext.Result.ToString()))
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            
            try
            {
                var workItemInput = (WorkItemInput)stepContext.Options;
                string token = await this.authHelper.GetTokenAsync(stepContext.Context.Activity).ConfigureAwait(false);
                var strings = stepContext.Result.ToString().Split(";");
                WorkItem workItem = await vsoApiController.GetWorkItemAsync(workItemInput.workItemId, strings[0], strings[1], token).ConfigureAwait(false);

                var replaceInfo = new Dictionary<string, string>();
                replaceInfo.Add("{{bugId}}", workItem.Id.ToString());
                if (workItem.Fields.Count == 0)
                {
                    this.logger.LogWarning($"Work item details are miising for {workItem.Id}");
                }

                if (workItem.Fields.TryGetValue("System.Title", out object title))
                {
                    replaceInfo.Add("{{bugTitle}}", title.ToString());
                }

                if (workItem.Fields.TryGetValue("System.Description", out object description))
                {
                    replaceInfo.Add("{{bugDescription}}", description.ToString());
                }

                if (workItem.Fields.TryGetValue("System.State", out object status))
                {
                    replaceInfo.Add("{{bugStatus}}", status.ToString());
                }

                replaceInfo.Add("{{numberOfUpdates}}", "5");
                var cardText = await CardProvider.GetCardText("BugDetailsCard", replaceInfo).ConfigureAwait(false);
                var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
                await stepContext.Context.SendActivitiesAsync(new[] { replyActivity }, cancellationToken);
                return await stepContext.EndDialogAsync(workItemInput, cancellationToken);
            }
            catch (Exception)
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}
