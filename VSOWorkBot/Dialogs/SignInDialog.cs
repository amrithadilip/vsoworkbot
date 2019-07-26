namespace VSOWorkBot.Dialogs
{
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using VSOWorkBot.Extensions;

    public class SignInDialog : CancelAndLogoutDialog
    {
        private AuthHelper authHelper;
        private ILogger logger;
        protected readonly IStatePropertyAccessor<string> tokenAccessor;

        public SignInDialog(IConfiguration configuration, ILogger logger, IBotTelemetryClient telemetryClient, UserState userState, AuthHelper authHelper)
            : base(nameof(SignInDialog), authHelper, configuration)
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            this.tokenAccessor = userState.CreateProperty<string>("VSOToken");
            this.authHelper = authHelper;

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginCompleteAsync,
            })
            { 
                TelemetryClient = telemetryClient,
            });

            this.logger = logger;

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

            var token = await authHelper.GetTokenAsync(stepContext.Context.Activity);
            if (string.IsNullOrEmpty(token))
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We didn't receive a token. Let's try that again."), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.tokenAccessor.SetAsync(stepContext.Context, token, cancellationToken);
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken).ConfigureAwait(false);
            }

            return await stepContext.EndDialogAsync(token, cancellationToken);
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
