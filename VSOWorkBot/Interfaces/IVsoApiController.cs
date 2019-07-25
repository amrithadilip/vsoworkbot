namespace VSOWorkBot.Interfaces
{
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using VSOWorkBot.Models;
    using WorkItemType = Models.WorkItemType;

    public interface IVsoApiController
    {
        Task<IEnumerable<WorkItem>> GetWorkItemsFromWorkItemDetailsAsync(string projectCollection, string projectName, WorkItemDetails workItemDetails);

        Task<IEnumerable<WorkItem>> GetWorkItemByQueryAsync(string projectCollection, string projectName, Wiql wiql);

        Task<WorkItem> GetWorkItemAsync(string id, string projectCollection, string projectName);

        Task<WorkItem> CreateWorkItemAsync(string projectCollection, string projectName, string projectAreaPath, string title, string description, string reproSteps, string priority, string severity, WorkItemType workItemType);

        Task<WorkItem> UpdateWorkItemAsync(string id, string projectCollection, IDictionary<string, string> fieldToValueMappings);

    }
}
