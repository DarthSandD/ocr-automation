using System.Drawing;
using OcrAutomation.Models;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Utilities;
using Tesseract;

namespace OcrAutomation.Services.OcrEngines;

public class TesseractOcrEngine : IOcrEngine
{
    private TesseractEngine? _engine;
    private readonly string _tessdataPath;

    public string Name => "Tesseract OCR";

    public string? LastInitError { get; private set; }

    public bool IsAvailable
    {
        get
        {
            try
            {
                EnsureEngineInitialized();
                LastInitError = null;
                return _engine != null;
            }
            catch (Exception ex)
            {
                var chain = ex.GetType().Name + ": " + ex.Message;
                var inner = ex.InnerException;
                int depth = 0;
                while (inner != null && depth < 3)
                {
                    chain += " <= " + inner.GetType().Name + ": " + inner.Message;
                    inner = inner.InnerException;
                    depth++;
                }
                var stack = ex.StackTrace ?? string.Empty;
                var frames = string.Join(" <- ", stack.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).Take(8));
                var basedir = AppContext.BaseDirectory ?? "NULL_BASEDIR";
                var procpath = Environment.ProcessPath ?? "NULL_PROCPATH";
                LastInitError = chain + " || BASEDIR=" + basedir + " PROCPATH=" + procpath + " || STACK: " + frames;
                return false;
            }
        }
    }

    public TesseractOcrEngine()
    {
        _tessdataPath = ResourceExtractor.EnsureTessdataExists();
    }

    public TesseractOcrEngine(string tessdataPath)
    {
        _tessdataPath = tessdataPath;
    }

    private void EnsureEngineInitialized()
    {
        if (_engine == null)
        {
            _engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
            try { _engine.DefaultPageSegMode = PageSegMode.Auto; } catch { }
        }
    }

    public async Task<OcrResult> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            EnsureEngineInitialized();

            if (_engine == null)
            {
                throw new InvalidOperationException("Tesseract engine is not initialized");
            }

            // Convert Bitmap to Pix
            using var memStream = new System.IO.MemoryStream();
            image.Save(memStream, System.Drawing.Imaging.ImageFormat.Png);
            memStream.Position = 0;

            using var pix = Pix.LoadFromMemory(memStream.ToArray());
            using var page = _engine.Process(pix);

            var result = new OcrResult
            {
                Text = page.GetText(),
                Confidence = page.GetMeanConfidence(),
                EngineName = Name,
                ProcessingTime = TimeSpan.Zero,
                Success = true,
            };

            // Extract bounding boxes
            using var iterator = page.GetIterator();
            iterator.Begin();

            do
            {
                if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds))
                {
                    var wordText = iterator.GetText(PageIteratorLevel.Word);
                    var confidence = iterator.GetConfidence(PageIteratorLevel.Word);

                    result.BoundingBoxes.Add(new BoundingBox
                    {
                        X = bounds.X1,
                        Y = bounds.Y1,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Text = wordText ?? string.Empty,
                        Confidence = confidence / 100.0
                    });
                }
            } while (iterator.Next(PageIteratorLevel.Word));

            return result;
        }, cancellationToken);
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
