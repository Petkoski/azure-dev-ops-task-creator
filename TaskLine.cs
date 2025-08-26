using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TaskLine
{
    public int ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float OriginalEstimate { get; set; }
    public float RemainingWork { get; set; }
    public List<string> Tags { get; set; } = [];
    public string State { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;

    public static TaskLine Parse(string line)
    {
        //Split by commas but ignore commas inside quotes:
        var parts = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

        if (parts.Length < 7)
            throw new ArgumentException($"Invalid line format: expected 7 fields, got {parts.Length}");

        string TrimQuotes(string s) => s.Trim().Trim('"');

        var tagsString = TrimQuotes(parts[5]);

        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(tagsString))
        {
            foreach (var tag in tagsString.Split(';'))
            {
                var trimmedTag = tag.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedTag))
                {
                    tags.Add(trimmedTag);
                }
            }
        }

        return new TaskLine
        {
            ParentId = int.Parse(parts[0]),
            Title = TrimQuotes(parts[1]),
            Description = TrimQuotes(parts[2]),
            OriginalEstimate = float.Parse(parts[3]),
            RemainingWork = float.Parse(parts[4]),
            Tags = tags,
            State = TrimQuotes(parts[6]),
            IterationPath = TrimQuotes(parts[7]),
        };
    }
}