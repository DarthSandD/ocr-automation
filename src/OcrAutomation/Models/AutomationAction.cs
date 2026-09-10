namespace OcrAutomation.Models;

public enum ActionType
{
    Keystroke,
    Click,
    SetText
}

public class AutomationAction
{
    public ActionType Type { get; set; }
    public string? Target { get; set; }  // Window title, automation ID, etc.
    public string? Value { get; set; }   // Text to type, coordinates, etc.
    public Dictionary<string, string> Parameters { get; set; } = new();
}
