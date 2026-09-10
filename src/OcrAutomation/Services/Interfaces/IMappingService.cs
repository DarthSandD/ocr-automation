using OcrAutomation.Models;

namespace OcrAutomation.Services.Interfaces;

public interface IMappingService
{
    Task LoadRulesAsync(string filePath);
    Task SaveRulesAsync(string filePath);
    List<AutomationAction> MatchText(string ocrText);
    IReadOnlyList<string> ValidateRules();
    MappingRuleCollection Rules { get; }
}
