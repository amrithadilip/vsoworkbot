// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VSOWorkBot.Helpers;

namespace VSOWorkBot.Bots
{
public class AuthBot<T> : DialogBot<T> where T : Dialog
{
    public AuthBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        : base(conversationState, userState, dialog, logger)
    {
    }

    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                var cardText = await CardProvider.GetCardText("WelcomeCard").ConfigureAwait(false);
                var replyActivity = JsonConvert.DeserializeObject<Activity>(cardText);
                await turnContext.SendActivityAsync(replyActivity, cancellationToken);
            }
        }
    }

    protected override async Task OnTokenResponseEventAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Running dialog with Token Response Event Activity.");

        // Run the Dialog with the new Token Response Event Activity.
        await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
    }
}
}