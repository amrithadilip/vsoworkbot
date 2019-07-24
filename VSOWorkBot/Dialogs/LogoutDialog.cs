// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using VSOWorkBot.Extensions;

namespace VSOWorkBot.Dialogs
{
	public class LogoutDialog : ComponentDialog
	{
		private readonly AuthHelper authHelper;

		public LogoutDialog(string id, AuthHelper authHelper)
			: base(id)
		{
			this.authHelper = authHelper;
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
			if (innerDc.Context.Activity.Type == ActivityTypes.Message)
			{
				var text = innerDc.Context.Activity.Text.ToLowerInvariant();

				if (text == "logout")
				{
					await this.authHelper.SignOutAsync(innerDc.Context.Activity);
					await innerDc.Context.SendActivityAsync(MessageFactory.Text("You have been signed out."), cancellationToken);
					return await innerDc.CancelAllDialogsAsync(cancellationToken);
				}
			}

			return null;
		}
	}
}