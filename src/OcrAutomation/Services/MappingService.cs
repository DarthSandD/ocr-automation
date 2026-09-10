using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using OcrAutomation.Models;
using OcrAutomation.Services.Interfaces;

namespace OcrAutomation.Services;

public class MappingService : IMappingService
{
    private MappingRuleCollection _rules = new();

    public MappingRuleCollection Rules => _rules;

    public async Task LoadRulesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _rules = new MappingRuleCollection();
            return;
        }

        var json = await File.ReadAllTextAsync(filePath);
        _rules = JsonSerializer.Deserialize<MappingRuleCollection>(json) ?? new MappingRuleCollection();
    }

    public async Task SaveRulesAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_rules, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public List<AutomationAction> MatchText(string ocrText)
    {
        var actions = new List<AutomationAction>();
        foreach (var rule in _rules.Rules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Pattern)))
        {
            try
            {
                var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                if (regex.IsMatch(ocrText ?? string.Empty)) actions.AddRange(rule.Actions);
            }
            catch (ArgumentException) { /* Invalid rules are ignored safely. */ }
            catch (RegexMatchTimeoutException) { /* Pathological rules cannot block automation. */ }
        }
        return actions;
    }

    public IReadOnlyList<string> ValidateRules()
    {
        var errors = new List<string>();
        foreach (var rule in _rules.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name)) errors.Add("A rule has no name.");
            if (string.IsNullOrWhiteSpace(rule.Pattern)) { errors.Add($"Rule '{rule.Name}' has no pattern."); continue; }
            try { _ = new Regex(rule.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)); }
            catch (Exception ex) { errors.Add($"Rule '{rule.Name}': {ex.Message}"); }
        }
        return errors;
    }
}
