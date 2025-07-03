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
        var task = TaskLine.Parse(line);
        await devOpsService.CreateTaskAsync(task);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing line: {line}\n{ex.Message}");
    }
}