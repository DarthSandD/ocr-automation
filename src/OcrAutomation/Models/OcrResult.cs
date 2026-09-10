namespace OcrAutomation.Models;

public class OcrResult
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<BoundingBox> BoundingBoxes { get; set; } = new();
    public string EngineName { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public List<OcrLine> Lines { get; set; } = new();
}

public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class OcrLine
{
    public string Text { get; set; } = string.Empty;
    public Rect BoundingRect { get; set; } = new();
}

public class Rect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Rect() { }
    public Rect(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; }
}
