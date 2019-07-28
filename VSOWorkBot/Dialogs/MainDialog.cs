// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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

		protected readonly IConfiguration configuration;

		public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger, IBotTelemetryClient telemetryClient, UserState userState, AuthHelper authHelper) : base(nameof(MainDialog), authHelper, configuration)
		{
			this.logger = logger;
			this.authHelper = authHelper;
			this.configuration = configuration;
			TelemetryClient = telemetryClient;
			IVsoApiController vsoApiController = new VsoApiHelper(logger);

			AddDialog(new GetWorkItemDialog(configuration, logger, authHelper, vsoApiController)
            {
                TelemetryClient = telemetryClient,
            });

            AddDialog(new CreateWorkItemDialog(configuration, logger, authHelper, vsoApiController, userState)
            {
                TelemetryClient = telemetryClient,
            });

            AddDialog(new SignInDialog(configuration, logger, telemetryClient, authHelper));
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[] {
				ActStepAsync,
			})
			{
				TelemetryClient = telemetryClient,
			});

			// The initial child Dialog to run.
			InitialDialogId = nameof(WaterfallDialog);
		}

		private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var profile = await authHelper.GetAuthenticatedProfileAsync(stepContext.Context, cancellationToken);
			if (profile == null)
			{
				return await stepContext.BeginDialogAsync(nameof(SignInDialog), cancellationToken: cancellationToken);
			}

			// Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
			var recognizerResult = await LuisHelper.ExecuteLuisQuery(TelemetryClient, configuration, this.logger, stepContext.Context, cancellationToken);

            // In this sample we only have a single Intent we are concerned with. However, typically a scenario
            // will have multiple different Intents each corresponding to starting a different child Dialog.

            var (intent, score) = recognizerResult.GetTopScoringIntent();
            if (intent == "getvsoitem")
            {
                // Run the BookingDialog giving it whatever details we have from the LUIS call, it will fill out the remainder.
                return await stepContext.BeginDialogAsync(nameof(GetWorkItemDialog), recognizerResult, cancellationToken);
            }
            else if (intent == "createvsoitem")
            {
                return await stepContext.BeginDialogAsync(nameof(CreateWorkItemDialog), recognizerResult, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
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