using OcrAutomation.Models;

namespace OcrAutomation.Services.Interfaces;

public interface IAutomationService
{
    Task ExecuteActionsAsync(List<AutomationAction> actions, IntPtr? targetWindow = null);
    bool CanUndo { get; }
    Task UndoLastActionAsync();
    void ClearHistory();
}
