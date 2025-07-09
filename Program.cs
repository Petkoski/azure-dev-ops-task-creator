using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var org = config["AzureDevOps:Organization"] ?? string.Empty;
var project = config["AzureDevOps:Project"] ?? string.Empty;
var pat = config["AzureDevOps:PersonalAccessToken"] ?? string.Empty;

var devOpsService = new AzureDevOpsService(org, project, pat);
var lines = File.ReadAllLines("tasks.txt");

foreach (var line in lines)
{
    try
    {
        TaskLine? task = TaskLine.Parse(line);
        int? createdTaskId = await devOpsService.CreateTaskAsync(task);

        if (createdTaskId.HasValue
            && !string.IsNullOrWhiteSpace(task.State)
            && task.State != "New")
        {
            await devOpsService.UpdateTaskStateAsync(createdTaskId.Value, task.State);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing line: {line}\n{ex.Message}");
    }
}

//var changedFiles = await devOpsService.GetAllFileChangesInPullRequestAsync(34);