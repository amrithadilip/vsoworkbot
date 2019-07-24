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


    public static class VsoApiController
    {
        public enum ApiType
        {
            WorkItem,
            Git,
        };

        public enum WorkItemType
        {
            Bug,
            Task,
            Feature,
        };

        public static WorkItem GetWorkItem(string id, string projectCollection, string projectName)
        {
            id.RequireNotNull();
            int workItemId = 0;
            if (!Int32.TryParse(id, out workItemId) || workItemId == 0)
            {
                return null;
            }

            var vssCredentials = new VssBasicCredential(string.Empty, "lckkdu5e3h64pm2xg434ku7daolewtvv27wyua7rm7xopcy23wca");
            Uri uri = new Uri($"https://dev.azure.com/{projectCollection}");
            VssConnection vssConnection = new VssConnection(uri, vssCredentials);
            WorkItem workItem = default(WorkItem);
            using (var client = vssConnection.GetClient<WorkItemTrackingHttpClient>())
            {
                try
                {
                    workItem = client.GetWorkItemAsync(workItemId).SyncResult();

                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }

            return workItem;
        }

        public static async void CreateBug(string projectCollection, string projectName, string title, string description, string reproSteps, string priority, string severity)
        {
            var vssCredentials = new VssBasicCredential(string.Empty, "lckkdu5e3h64pm2xg434ku7daolewtvv27wyua7rm7xopcy23wca");
            Uri uri = new Uri($"https://dev.azure.com/{projectCollection}");
            VssConnection vssConnection = new VssConnection(uri, vssCredentials);
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
                    workItem = await client.CreateWorkItemAsync(patchDocument, projectName, WorkItemType.Bug.ToString()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }

        public static string GenerateBaseApiUri(string instance, string projectCollection, string projectName, ApiType apiType, string id)
        {
            string baseUri = $"https://{instance}/{projectCollection}/{projectName}/_apis";
            switch(apiType)
            {
                case ApiType.WorkItem:
                    return $"{baseUri}/workitems/{id}";
                case ApiType.Git:
                    return $"{baseUri}/git/{id}";
                default: return baseUri;
            }
        }
    }
}
