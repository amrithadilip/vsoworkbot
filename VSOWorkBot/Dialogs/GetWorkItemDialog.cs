using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSOWorkBot.Extensions;
using VSOWorkBot.Helpers;
using VSOWorkBot.Interfaces;
using VSOWorkBot.Models;

namespace VSOWorkBot.Dialogs
{
    public class GetWorkItemDialog : CancelAndLogoutDialog
    {
        private IVsoApiController vsoApiController;
        private ILogger logger;

        public GetWorkItemDialog(IConfiguration configuration, ILogger logger, IBotTelemetryClient telemetryClient, UserState userState, AuthHelper authHelper, IVsoApiController vsoApiController)
            : base(nameof(GetWorkItemDialog), authHelper, configuration)
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetWorItemType,
                FinalStepAsync,
            })
            { 
                TelemetryClient = telemetryClient,
            });

            this.logger = logger;
            this.vsoApiController = vsoApiController;

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetWorItemType(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var WorkItemInput = (WorkItemInput)stepContext.Options;

            if (WorkItemInput.workItemType == null || Enum.TryParse(WorkItemInput.workItemType, true, out Models.WorkItemType workItemType))
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("What is the work item type?") }, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(WorkItemInput.workItemType, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Enum.TryParse<Models.WorkItemType>(stepContext.Result.ToString(), out _))
            {
                var workItemInput = (WorkItemInput)stepContext.Options;
                WorkItem workItem = await vsoApiController.GetWorkItemAsync("1231687", "msasg", "Cortana").ConfigureAwait(false);

                var replaceInfo = new Dictionary<string, string>();
                replaceInfo.Add("{{bugId}}", workItem.Id.ToString());
                if (workItem.Fields.Count == 0)
                {
                    this.logger.LogWarning($"Work item details are miising for {workItem.Id}");
                }

                replaceInfo.Add("{{bugTitle}}", workItem.Fields["System.Title"].ToString());
                replaceInfo.Add("{{bugDescription}}", workItem.Fields["System.Description"].ToString());
                replaceInfo.Add("{{bugStatus}}", workItem.Fields["System.State"].ToString());
                
                replaceInfo.Add("{{numberOfUpdates}}", "5");
                var cardText = await CardProvider.GetCardText("BugDetailsCard", replaceInfo).ConfigureAwait(false);
                var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
                await stepContext.Context.SendActivitiesAsync(new [ ] { replyActivity }, cancellationToken);
                return await stepContext.EndDialogAsync(workItemInput, cancellationToken);
            }
            else
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}
