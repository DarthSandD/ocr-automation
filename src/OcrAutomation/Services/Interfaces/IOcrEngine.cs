using System.Drawing;
using OcrAutomation.Models;

namespace OcrAutomation.Services.Interfaces;

public interface IOcrEngine
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<OcrResult> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default);
}
