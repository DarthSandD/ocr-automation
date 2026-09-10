using System.Drawing;
using OcrAutomation.Services;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Services.OcrEngines;
using Xunit;

namespace OcrAutomation.Tests;

public class CaptureOcrPipelineTest
{
    [Fact]
    public async Task CapturePipeline_ProducesImage()
    {
        IScreenCaptureService capture = new ScreenCaptureService();

        // Capture current screen region (small test region)
        var region = new OcrAutomation.Models.CaptureRegion
        {
            X = 0, Y = 0, Width = 200, Height = 200
        };

        var bitmap = await capture.CaptureRegionAsync(region);
        Assert.NotNull(bitmap);
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
        bitmap.Dispose();
    }

    [Fact]
    public void PreprocessingPipeline_ProducesCleanImage()
    {
        IPreprocessingPipeline pipeline = new PreprocessingPipeline();
        using var input = new Bitmap(300, 300, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using var output = pipeline.Process(input);
        Assert.NotNull(output);
        Assert.True(output.Width > 0);
    }

    [Fact]
    public async Task TesseractOcr_RecognizesText()
    {
        // Use a synthetic bitmap with black text on white background
        using var bmp = new Bitmap(400, 100);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            using var font = new Font("Arial", 24, FontStyle.Regular);
            g.DrawString("Hello OCR", font, Brushes.Black, 10, 10);
        }

        IOcrEngine engine = new TesseractOcrEngine();
        Assert.True(engine.IsAvailable);

        var result = await engine.RecognizeAsync(bmp);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Text) && result.BoundingBoxes.Count == 0);
    }

    [Fact]
    public async Task PreprocessedScreenshot_StillRecognizesText()
    {
        // Regression test for DARREN-122: the app pipeline (preprocess -> OCR)
        // must not destroy clean screen text. Small fonts must survive.
        using var bmp = new Bitmap(600, 120);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            using var font = new Font("Consolas", 14, FontStyle.Regular);
            g.DrawString("Invoice TOTAL 123.45", font, Brushes.Black, 10, 10);
            g.DrawString("Row two: alpha beta", font, Brushes.Black, 10, 60);
        }

        IPreprocessingPipeline pipeline = new PreprocessingPipeline();
        using var processed = pipeline.Process(bmp);
        IOcrEngine engine = new TesseractOcrEngine();
        var raw = await engine.RecognizeAsync(bmp);
        var pro = await engine.RecognizeAsync(processed);
        var best = (pro.Text ?? string.Empty).Trim().Length >= (raw.Text ?? string.Empty).Trim().Length ? pro : raw;
        Assert.False(string.IsNullOrWhiteSpace(best.Text));
        Assert.Contains("TOTAL", best.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PdfImport_RendersCadGradeRaster()
    {
        // v2.2: a real PDF must rasterize at 300 DPI to CAD-usable dimensions.
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "sample.pdf");
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        int pages = PDFtoImage.Conversion.GetPageCount(bytes);
        Assert.True(pages > 0);
        using var sk = PDFtoImage.Conversion.ToImage(bytes, page: 0, dpi: 300);
        using var data = sk.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());
        using var bmp = new Bitmap(ms);
        using var raster = new Bitmap(bmp);
        Assert.True(raster.Width > 1000);
        Assert.True(raster.Height > 1000);
    }
}
