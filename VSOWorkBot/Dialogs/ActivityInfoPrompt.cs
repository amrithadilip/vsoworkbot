namespace VSOWorkBot.Dialogs
{
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Schema;

    public class ActivityInfoPrompt : ActivityPrompt
	{
		public ActivityInfoPrompt(PromptValidator<Activity> promptValidator)
			: base(nameof(ActivityInfoPrompt), promptValidator)
		{
		}
    }
}
