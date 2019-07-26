// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace VSOWorkBot.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Configuration;
    using VSOWorkBot.Extensions;
    using VSOWorkBot.Helpers;

    public class CancelAndLogoutDialog : ComponentDialog
    {
        private readonly AuthHelper authHelper;

        private readonly IConfiguration configuration;

        public CancelAndLogoutDialog(string id, AuthHelper authHelper, IConfiguration configuration)
            : base(id)
        {
            this.authHelper = authHelper;
            this.configuration = configuration;
        }

        protected string ConnectionName { get; }

        protected override async Task<DialogTurnResult> OnBeginDialogAsync(DialogContext innerDc, object options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnBeginDialogAsync(innerDc, options, cancellationToken);
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (innerDc.Context.Activity.Type != ActivityTypes.Message)
            {
                return null;
            }

            var text = innerDc.Context.Activity.Text.ToLowerInvariant();

            var result = await LuisHelper.GetLuisResult(this.configuration, text).ConfigureAwait(false);
            if (result?.TopScoringIntent == null)
            {
                return null;
            }

            switch (result.TopScoringIntent.Intent)
            {
                case "signout":
                    await this.authHelper.SignOutAsync(innerDc.Context.Activity);
                    await innerDc.Context.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
                    return await innerDc.CancelAllDialogsAsync(cancellationToken);
                case "Calendar.Cancel":
                    await innerDc.Context.SendActivityAsync($"Cancelling", cancellationToken: cancellationToken);
                    return await innerDc.CancelAllDialogsAsync(cancellationToken);
            }

            return null;
        }
    }
}