namespace VSOWorkBot.Helpers
{
    using Newtonsoft.Json.Linq;
    using VSOWorkBot.Models;

    public static class Utilities
    {
        public static WorkItemType GetWorkItemType(JObject entities)
        {
            if (entities["bugentity"] != null)
            {
                return WorkItemType.Bug;
            }
            else if (entities["taskentity"] != null)
            {
                return WorkItemType.Task;
            }

            return WorkItemType.None;
        }
    }
}
