namespace VSOWorkBot.Api
{
    using Microsoft.VisualStudio.Services.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using System;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
    using global::VSOWorkBot.Helpers;
    using System.Threading.Tasks;
    using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
    using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
    using Microsoft.VisualStudio.Services.WebApi.Patch;
    using System.Collections.Generic;
    using System.Linq;
    using WorkItemType = Models.WorkItemType;
    using VSOWorkBot.Interfaces;
    using Microsoft.Extensions.Logging;
    using VSOWorkBot.Models;
    using Microsoft.TeamFoundation.Core.WebApi;
    using Microsoft.TeamFoundation.SourceControl.WebApi;

    public class VsoApiHelper : IVsoApiController
    {
        private readonly ILogger logger;

        public enum ApiType
        {
            WorkItem,
            Git,
        };

        public VsoApiHelper(ILogger logger)
        {
            logger.RequireNotNull();
            this.logger = logger;
        }

        public async Task<GitPullRequest> CreatePullRequest(GitRepository repo, string accessToken)
        {
            VssConnection vssConnection = GetVssConnection(null, accessToken);
            GitPullRequest pullRequest = null;
            using (GitHttpClient gitClient = vssConnection.GetClient<GitHttpClient>())
            {
                List<GitPullRequest> pullRequests = await gitClient.GetPullRequestsAsync(
                                                 repo.Id,
                                                 new GitPullRequestSearchCriteria()
                                                 {
                                                     Status = PullRequestStatus.Active,

                                                 }).ConfigureAwait(false);
                pullRequest = pullRequests.FirstOrDefault();
            }

            return pullRequest;
        }

        public async Task<IEnumerable<TeamProjectCollectionReference>> GetProjectCollections(string accessToken)
        {
            VssConnection vssConnection = GetVssConnection(null, accessToken);
            IEnumerable<TeamProjectCollectionReference> projectCollections = new List<TeamProjectCollectionReference>();

            using (var projectCollectionClient = vssConnection.GetClient<ProjectCollectionHttpClient>())
            {
                projectCollections = await projectCollectionClient.GetProjectCollections().ConfigureAwait(false);

                foreach (var collection in projectCollections)
                {
                    Console.WriteLine(collection.Name);
                }
            }

            return projectCollections;
        }

        public async Task<IEnumerable<WorkItem>> GetWorkItemsFromWorkItemInputAsync(string projectCollection, string projectName, WorkItemInput WorkItemInput, string accessToken)
        {
            return await GetWorkItemByQueryAsync(projectCollection, projectName, ContructWiqlQuery(WorkItemInput), accessToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<WorkItem>> GetWorkItemByQueryAsync(string projectCollection, string projectName, Wiql wiql, string accessToken)
        {
            accessToken.RequireNotNull();
            VssConnection vssConnection = GetVssConnection(projectCollection, accessToken);
            IEnumerable<WorkItem> workItems = new List<WorkItem>();
            using (var client = vssConnection.GetClient<WorkItemTrackingHttpClient>())
            {
                try
                {
                    var workItemQueryResult = await client.QueryByWiqlAsync(wiql).ConfigureAwait(false);
                    if (workItemQueryResult?.WorkItems.Count() == 0)
                    {
                        logger.LogInformation($"No results found using query for project collection: {projectCollection} with project name: {projectName}");
                    }

                    //need to get the list of our work item ids and put them into an array
                    List<int> list = new List<int>();
                    foreach (var item in workItemQueryResult.WorkItems)
                    {
                        list.Add(item.Id);
                    }
                    int[] arr = list.ToArray();

                    //build a list of the fields we want to see
                    string[] fields = new string[3];
                    fields[0] = "System.Id";
                    fields[1] = "System.Title";
                    fields[2] = "System.State";

                    //get work items for the ids found in query
                    workItems = await client.GetWorkItemsAsync(arr, fields, workItemQueryResult.AsOf);
                    return workItems;
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"{nameof(GetWorkItemByQueryAsync)} Error occured while fetching work items {ex}");
                }
            }

            return workItems;
        }

        public async Task<WorkItem> GetWorkItemAsync(string id, string projectCollection, string projectName, string accessToken)
        {
            id.RequireNotNull();
            accessToken.RequireNotNull();
            int workItemId = 0;
            if (!Int32.TryParse(id, out workItemId) || workItemId == 0)
            {
                return null;
            }

            VssConnection vssConnection = GetVssConnection(projectCollection, accessToken);
            WorkItem workItem = default(WorkItem);
            using (var client = vssConnection.GetClient<WorkItemTrackingHttpClient>())
            {
                try
                {
                    workItem = await client.GetWorkItemAsync(workItemId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"{nameof(GetWorkItemAsync)} Error occured while fetching work items {ex}");
                }
            }

            return workItem;
        }

        public async Task<WorkItem> CreateWorkItemAsync(
            string projectCollection,
            string projectName,
            string projectAreaPath,
            string title,
            string description,
            string reproSteps,
            string priority, 
            string severity, 
            WorkItemType workItemType,
            string accessToken)
        {
            accessToken.RequireNotNull();
            VssConnection vssConnection = GetVssConnection(projectCollection, accessToken);
            WorkItem workItem = default(WorkItem);
            using (var client = vssConnection.GetClient<WorkItemTrackingHttpClient>())
            {
                try
                {
                    JsonPatchDocument patchDocument = new JsonPatchDocument();

                    //add fields and their values to your patch document
                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/System.Title",
                            Value = title
                        }
                    );

                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/System.Description",
                            Value = description
                        }
                    );

                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/Microsoft.VSTS.TCM.ReproSteps",
                            Value = reproSteps
                        }
                    );

                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/Microsoft.VSTS.Common.Priority",
                            Value = priority
                        }
                    );

                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/Microsoft.VSTS.Common.Severity",
                            Value = severity
                        }
                    );

                    patchDocument.Add(
                        new JsonPatchOperation()
                        {
                            Operation = Operation.Add,
                            Path = "/fields/System.AreaPath",
                            Value = projectAreaPath
                        }
                    );

                    workItem = await client.CreateWorkItemAsync(patchDocument, projectName, workItemType.ToString()).ConfigureAwait(false);
                    return workItem;
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"{nameof(CreateWorkItemAsync)} Error occured while creating a work items with execption: {ex}");
                    return null;
                }
            }
        }

        public async Task<WorkItem> UpdateWorkItemAsync(string id, string projectCollection, IDictionary<string, string> fieldToValueMappings, string accessToken)
        {
            id.RequireNotNull();
            accessToken.RequireNotNull();
            int workItemId = 0;
            if (!Int32.TryParse(id, out workItemId) || workItemId == 0)
            {
                return null;
            }

            VssConnection vssConnection = GetVssConnection(projectCollection, accessToken);
            WorkItem workItem = default(WorkItem);
            using (var client = vssConnection.GetClient<WorkItemTrackingHttpClient>())
            {
                try
                {
                    JsonPatchDocument patchDocument = new JsonPatchDocument();

                    foreach (var kvp in fieldToValueMappings)
                    {
                        //add fields and their values to your patch document
                        patchDocument.Add(
                            new JsonPatchOperation()
                            {
                                Operation = Operation.Add,
                                Path = kvp.Key,
                                Value = kvp.Value,
                            }
                        );
                    }

                    workItem = await client.UpdateWorkItemAsync(patchDocument, workItemId).ConfigureAwait(false);
                    return workItem;
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"{nameof(UpdateWorkItemAsync)} Error occured while updating work item with id {id} with exception: {ex}");
                    return null;
                }
            }
        }

        private static Uri GenerateBaseApiUri(string projectCollection = null)
        {
            return projectCollection != null ? new Uri($"https://dev.azure.com/{projectCollection}") : new Uri($"https://dev.azure.com");
        }

        private static Wiql ContructWiqlQuery(WorkItemInput WorkItemInput)
        {
            return new Wiql()
            {
                Query = "Select [State], [Title] [Description]" +
                        "From WorkItems " +
                        "Where [Work Item Type] = 'Bug' " +
                        "And [System.TeamProject] = '" + "Cortana" + "' " +
                        "And [System.State] <> 'Active' " +
                        "Order By [State] Asc, [Changed Date] Desc"
            };
        }

        private static VssConnection GetVssConnection(string projectCollection, string accessToken)
        {
            var vssCredentials = new VssBasicCredential(string.Empty, accessToken);
            return new VssConnection(GenerateBaseApiUri(projectCollection), vssCredentials);
        }

    }
}
