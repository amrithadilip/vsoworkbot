namespace VSOWorkBot.Models
{
public enum WorkItemType
{
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
    public string workItemId {
        get;
        set;
    }

    public string workItemType {
        get;
        set;
    }

    public string workItemStatus {
        get;
        set;
    }

    public string userName {
        get;
        set;
    }
}
}
