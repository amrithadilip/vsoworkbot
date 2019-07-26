// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VSOWorkBot.Api;
using VSOWorkBot.Extensions;
using VSOWorkBot.Helpers;
using VSOWorkBot.Interfaces;
using VSOWorkBot.Models;

namespace VSOWorkBot.Dialogs
{
    public class MainDialog : CancelAndLogoutDialog
    {
        protected readonly ILogger logger;

        protected readonly AuthHelper authHelper;

        protected readonly UserState userState;

        protected readonly IStatePropertyAccessor<string> tokenAccessor;

        protected readonly IConfiguration configuration;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger, IBotTelemetryClient telemetryClient, UserState userState, AuthHelper authHelper)
            : base(nameof(MainDialog), authHelper, configuration)
        {
            this.logger = logger;
            this.authHelper = authHelper;
            this.userState = userState;
            this.tokenAccessor = userState.CreateProperty<string>("VSOToken");
            this.configuration = configuration;
            TelemetryClient = telemetryClient;
            IVsoApiController vsoApiController = new VsoApiController(logger);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new GetWorkItemDialog(configuration, logger, telemetryClient, userState, authHelper, vsoApiController));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt))
            {
                TelemetryClient = telemetryClient,
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginCompleteAsync,
                ActStepAsync,
            })
            {
                TelemetryClient = telemetryClient,
            });

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Cards are sent as Attachments in the Bot Framework.
            // So we need to create a list of attachments for the reply activity.			
            var attachments = new List<Attachment>() { GetSignInCard(stepContext.Context.Activity).ToAttachment() };

            // Reply to the activity we received with an activity.
            var reply = MessageFactory.Attachment(attachments);

            await stepContext.Context.SendActivityAsync(reply, cancellationToken).ConfigureAwait(false);
            return await stepContext.BeginDialogAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Did you have a successful login?") }, cancellationToken);
        }

        private async Task<DialogTurnResult> LoginCompleteAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var promptResult = (bool)stepContext.Result;
            if (!promptResult)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Let's try again."), cancellationToken).ConfigureAwait(false);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            // Not working for me on the local machine. Need to investigate.
            // var token = await authHelper.GetTokenAsync(stepContext.Context.Activity);
            var token = "test token";
            if (string.IsNullOrEmpty(token))
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We didn't receive a token. Let's try that again."), cancellationToken).ConfigureAwait(false);
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            await tokenAccessor.SetAsync(stepContext.Context, token, cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken).ConfigureAwait(false);
            return await stepContext.ContinueDialogAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var WorkItemInput = stepContext.Result != null
                    ?
                await LuisHelper.ExecuteLuisQuery(TelemetryClient, configuration, this.logger, stepContext.Context, cancellationToken)
                    :
                new WorkItemInput();

            // In this sample we only have a single Intent we are concerned with. However, typically a scenario
            // will have multiple different Intents each corresponding to starting a different child Dialog.

            // Run the BookingDialog giving it whatever details we have from the LUIS call, it will fill out the remainder.
            return await stepContext.BeginDialogAsync(nameof(GetWorkItemDialog), WorkItemInput, cancellationToken);
        }


        private ThumbnailCard GetSignInCard(Activity activity)
        {
            ThumbnailCard thumbnailCard = new ThumbnailCard()
            {
                Title = "Sign into Visual Studio Online",
                Subtitle = "",
                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "Authentication Required", value: this.authHelper.GetSignInUrl(conversationId: activity.Conversation.Id, userId: activity.From.Id)) }
            };
            return thumbnailCard;
        }
    }
}