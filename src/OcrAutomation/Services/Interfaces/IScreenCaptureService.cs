using System.Drawing;
using OcrAutomation.Models;

namespace OcrAutomation.Services.Interfaces;

public interface IScreenCaptureService
{
    Task<List<WindowInfo>> EnumerateWindowsAsync();
    Task<Bitmap> CaptureWindowAsync(IntPtr windowHandle);
    Task<Bitmap> CaptureRegionAsync(CaptureRegion region);
    Task<CaptureRegion?> SelectRegionAsync();
}
