using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

public class FileChangeInfo
{
    public string Path { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
}

public class AzureDevOpsService
{
    private readonly string _org, _project, _pat;
    private readonly HttpClient _client;

    public AzureDevOpsService(string org, string project, string pat)
    {
        _org = org;
        _project = project;
        _pat = pat;

        _client = new HttpClient();
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
    }

    public async Task<int?> CreateTaskAsync(TaskLine task)
    {
        var url = $"https://dev.azure.com/{_org}/{_project}/_apis/wit/workitems/$Task?api-version=7.1-preview.3";
        var patchDocument = new List<object>
        {
            new { op = "add", path = "/fields/System.Title", value = task.Title },
            new { op = "add", path = "/fields/System.Description", value = task.Description },
            new { op = "add", path = "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate", value = task.OriginalEstimate },
            new { op = "add", path = "/fields/Microsoft.VSTS.Scheduling.RemainingWork", value = task.RemainingWork },
            new { op = "add", path = "/fields/System.Tags", value = string.Join(";", task.Tags) },
            new { op = "add", path = "/fields/System.State", value = "New" },
            new {
                op = "add",
                path = "/relations/-",
                value = new {
                    rel = "System.LinkTypes.Hierarchy-Reverse",
                    url = $"https://dev.azure.com/{_org}/{_project}/_apis/wit/workItems/{task.ParentId}",
                    attributes = new { comment = "Link to parent user story" }
                }
            },
        };
        var content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to create task: {response.StatusCode} - {error}");
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var id = doc.RootElement.GetProperty("id").GetInt32();

        Console.WriteLine($"Task '{task.Title}' created successfully. ID: {id}");
        return id;
    }

    public async Task UpdateTaskStateAsync(int taskId, string newState)
    {
        var url = $"https://dev.azure.com/{_org}/{_project}/_apis/wit/workitems/{taskId}?api-version=7.1-preview.3";
        var patchDocument = new List<object>
        {
            new { op = "add", path = "/fields/System.State", value = newState },
        };
        var content = new StringContent(JsonSerializer.Serialize(patchDocument), Encoding.UTF8, "application/json-patch+json");

        var response = await _client.PatchAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"State of task {taskId} successfully updated to '{newState}'.");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to update state of task {taskId} to '{newState}': {response.StatusCode} - {error}");
        }
    }

    public async Task<List<FileChangeInfo>> GetAllFileChangesInPullRequestAsync(int pullRequestId)
    {
        int top = 100;
        int skip = 0;
        bool hasMore = true;

        var allChanges = new List<FileChangeInfo>();

        while (hasMore)
        {
            var url = $"https://dev.azure.com/{_org}/{_project}/_apis/git/repositories/{_project}/pullRequests/{pullRequestId}/iterations/1/changes?$top={top}&$skip={skip}&api-version=7.0";

            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to get changes for PR {pullRequestId}: {response.StatusCode} - {error}");
                break;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("changeEntries", out JsonElement changeEntries))
            {
                break;
            }

            int count = 0;

            foreach (var change in changeEntries.EnumerateArray())
            {
                string changeType = change.GetProperty("changeType").GetString() ?? string.Empty;
                string? path = null;

                //Try extracting path safely:
                if (change.TryGetProperty("item", out JsonElement item))
                {
                    if (item.TryGetProperty("path", out var pathElem))
                    {
                        path = pathElem.GetString() ?? string.Empty;
                    }
                }

                //Still null? Try fallback from sourceServerItem or originalPath (rare):
                if (path == null && change.TryGetProperty("sourceServerItem", out var sourceItem))
                {
                    path = sourceItem.GetString() ?? string.Empty;
                }

                if (path != null)
                {
                    allChanges.Add(new FileChangeInfo
                    {
                        Path = path,
                        ChangeType = changeType,
                    });
                    count++;
                }
            }

            hasMore = count == top;
            skip += top;
        }

        return allChanges;
    }
}