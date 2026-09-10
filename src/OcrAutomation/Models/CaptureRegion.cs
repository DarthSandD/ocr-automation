namespace OcrAutomation.Models;

public class CaptureRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsElevated { get; set; }
    public CaptureRegion Bounds { get; set; } = new();

    public override string ToString() => string.IsNullOrWhiteSpace(Title) ? "(untitled window)" : Title;
}
