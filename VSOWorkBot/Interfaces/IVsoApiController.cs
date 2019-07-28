namespace VSOWorkBot.Interfaces
{
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using VSOWorkBot.Models;
    using WorkItemType = Models.WorkItemType;

    public interface IVsoApiController
    {
        Task<IEnumerable<WorkItem>> GetWorkItemsFromWorkItemInputAsync(string projectCollection, string projectName, WorkItemInput WorkItemInput, string accessToken);

        Task<IEnumerable<WorkItem>> GetWorkItemByQueryAsync(string projectCollection, string projectName, Wiql wiql, string accessToken);

        Task<WorkItem> GetWorkItemAsync(string id, string projectCollection, string projectName, string accessToken);

        Task<WorkItem> CreateWorkItemAsync(string projectCollection, string projectName, IDictionary<string, string> fieldToValueMappings, WorkItemType workItemType, string accessToken);

        Task<WorkItem> UpdateWorkItemAsync(string id, string projectCollection, IDictionary<string, string> fieldToValueMappings, string accessToken);

    }
}
