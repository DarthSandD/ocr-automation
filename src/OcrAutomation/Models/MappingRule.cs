namespace OcrAutomation.Models;

public class MappingRule
{
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;  // Regex pattern
    public List<AutomationAction> Actions { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

public class MappingRuleCollection
{
    public List<MappingRule> Rules { get; set; } = new();
}
