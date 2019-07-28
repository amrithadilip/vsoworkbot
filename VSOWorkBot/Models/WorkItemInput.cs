namespace VSOWorkBot.Models
{
    public enum WorkItemType
    {
        None,
        Bug,
        Task,
        Feature,
    };

    public enum WorkItemStatus
    {
        New,
        Active,
        InProgress,
        Resolved,
        Closed,
    };

    public class WorkItemInput
    {
        public string WorkItemId { get; set; }

        public WorkItemType WorkItemType { get; set; }

        public WorkItemStatus WorkItemStatus { get; set; }

        public string UserName { get; set; }

        public string ProjectCollection { get; set; }

        public string ProjectName { get; set; }
    }
}
