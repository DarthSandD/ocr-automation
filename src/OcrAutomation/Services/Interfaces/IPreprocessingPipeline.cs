using System.Drawing;

namespace OcrAutomation.Services.Interfaces;

public interface IPreprocessingPipeline
{
    Bitmap Process(Bitmap input);
    void EnableGrayscale(bool enabled);
    void EnableContrastEnhancement(bool enabled);
    void EnableAdaptiveThreshold(bool enabled);
    void EnableDeskew(bool enabled);
    void EnableSharpening(bool enabled);
    void EnableUpscaleSmall(bool enabled);
}
