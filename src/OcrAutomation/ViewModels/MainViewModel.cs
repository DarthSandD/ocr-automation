using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OcrAutomation.Models;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Services.OcrEngines;
using OcrAutomation.Utilities;

namespace OcrAutomation.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IScreenCaptureService _captureService;
    private readonly IPreprocessingPipeline _preprocessingPipeline;
    private readonly IAutomationService _automationService;
    private readonly IMappingService _mappingService;
    private readonly List<IOcrEngine> _ocrEngines;

    // Config
    private const double LowConfidenceThreshold = 0.6;

    [ObservableProperty]
    private BitmapSource? _capturedImage;

    [ObservableProperty]
    private string _ocrText = string.Empty;

    [ObservableProperty]
    private double _confidence;

    public IReadOnlyList<string> OcrEngineOptions { get; } = new[] { "Tesseract OCR" };

    [ObservableProperty]
    private string _selectedOcrEngine = "Tesseract OCR";

    [ObservableProperty]
    private ObservableCollection<string> _logEntries = new();

    [ObservableProperty]
    private ObservableCollection<MappingRule> _mappingRules = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private List<WindowInfo> _availableWindows = new();

    [ObservableProperty]
    private WindowInfo? _selectedWindow;

    public IReadOnlyList<int> RenderDpiOptions { get; } = new[] { 150, 300, 600 };

    [ObservableProperty]
    private int _selectedRenderDpi = 300;

    [ObservableProperty]
    private int _pdfPageCount;

    [ObservableProperty]
    private int _pdfPage;

    [ObservableProperty]
    private string _importedFileName = string.Empty;

    public bool HasPdf => PdfPageCount > 0;
    public string PdfPageInfo => PdfPageCount > 0 ? $"Page {PdfPage} / {PdfPageCount}" : string.Empty;

    private byte[]? _importedPdfBytes;

    partial void OnSelectedRenderDpiChanged(int value)
    {
        if (_importedPdfBytes != null && PdfPageCount > 0)
            _ = RenderPdfPageAsync(PdfPage - 1);
    }

    partial void OnPdfPageCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasPdf));
        OnPropertyChanged(nameof(PdfPageInfo));
    }

    partial void OnPdfPageChanged(int value)
    {
        OnPropertyChanged(nameof(PdfPageInfo));
    }

    private Bitmap? _lastCapturedBitmap;
    private IntPtr? _lastTargetWindow;

    public void DisposeCapturedImage()
    {
        _lastCapturedBitmap?.Dispose();
        _lastCapturedBitmap = null;
    }

    public void Dispose() => DisposeCapturedImage();

    public MainViewModel(
        IScreenCaptureService captureService,
        IPreprocessingPipeline preprocessingPipeline,
        IAutomationService automationService,
        IMappingService mappingService,
        IEnumerable<IOcrEngine> ocrEngines)
    {
        _captureService = captureService;
        _preprocessingPipeline = preprocessingPipeline;
        _automationService = automationService;
        _mappingService = mappingService;
        _ocrEngines = ocrEngines.ToList();

        _ = LoadMappingsAsync();
    }

    [RelayCommand]
    private async Task RefreshWindowsAsync()
    {
        try
        {
            AddLog("Enumerating windows...");
            AvailableWindows = await _captureService.EnumerateWindowsAsync();
            AddLog($"Found {AvailableWindows.Count} window(s).");
        }
        catch (Exception ex)
        {
            AddLog($"Error enumerating windows: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CaptureWindowAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Capturing window...";

            // If no window selected, enumerate and pick first non-elevated
            if (SelectedWindow == null)
            {
                await RefreshWindowsAsync();
                SelectedWindow = AvailableWindows
                    .Where(w => !w.IsElevated && !string.IsNullOrWhiteSpace(w.Title))
                    .OrderBy(w => w.Title)
                    .FirstOrDefault();
            }

            if (SelectedWindow == null)
            {
                AddLog("No suitable window found. All windows are elevated or hidden.");
                StatusMessage = "No window available";
                return;
            }

            if (SelectedWindow.IsElevated)
            {
                AddLog($"WARNING: Window '{SelectedWindow.Title}' is running elevated. Automation will be blocked.");
            }

            AddLog($"Capturing window: {SelectedWindow.Title}");
            _lastTargetWindow = SelectedWindow.Handle;

            _lastCapturedBitmap = await _captureService.CaptureWindowAsync(SelectedWindow.Handle);
            CapturedImage = ConvertBitmapToBitmapSource(_lastCapturedBitmap);

            AddLog("Window captured successfully.");
            StatusMessage = "Capture complete";
        }
        catch (Exception ex)
        {
            AddLog($"Error capturing window: {ex.Message}");
            StatusMessage = "Capture failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CaptureRegionAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Select region...";

            // Minimize this window so overlay can capture
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var previousState = mainWindow?.WindowState;
            if (mainWindow != null) mainWindow.WindowState = WindowState.Minimized;

            await Task.Delay(300); // Let window minimize

            try
            {
                var region = await _captureService.SelectRegionAsync();

                if (region == null)
                {
                    AddLog("Region selection cancelled.");
                    StatusMessage = "Selection cancelled";
                    return;
                }

                AddLog($"Region selected: {region.X},{region.Y} {region.Width}x{region.Height}");
                _lastTargetWindow = Win32Api.GetForegroundWindow();

                _lastCapturedBitmap = await _captureService.CaptureRegionAsync(region);
                CapturedImage = ConvertBitmapToBitmapSource(_lastCapturedBitmap);

                AddLog("Region captured successfully.");
                StatusMessage = "Capture complete";
            }
            finally
            {
                if (mainWindow != null)
                {
                    mainWindow.WindowState = previousState ?? WindowState.Normal;
                    mainWindow.Activate();
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error capturing region: {ex.Message}");
            StatusMessage = "Region capture failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RunOcrAsync()
    {
        if (_lastCapturedBitmap == null)
        {
            AddLog("No image captured. Please capture a window or region first.");
            StatusMessage = "No image";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Running OCR with {SelectedOcrEngine}...";
            AddLog($"Running OCR with {SelectedOcrEngine}...");

            using var processed = _preprocessingPipeline.Process(_lastCapturedBitmap);
            AddLog("Image preprocessing complete.");

            // Select OCR engine (Tesseract only — the only bundled engine)
            var engine = _ocrEngines.OfType<TesseractOcrEngine>().FirstOrDefault();

            if (engine == null || !engine.IsAvailable)
            {
                var detail = engine?.LastInitError;
                AddLog("No OCR engine available." + (detail != null ? $" Init error: {detail}" : ""));
                StatusMessage = "No OCR engine";
                return;
            }

            // Perform OCR on BOTH raw and preprocessed images; keep the better result.
            // (Preprocessing can destroy clean screenshots, raw can miss noisy scans.)
            var rawResult = await engine.RecognizeAsync(_lastCapturedBitmap);
            var proResult = await engine.RecognizeAsync(processed);
            var result = PickBetterOcrResult(rawResult, proResult);
            AddLog($"OCR path winner: {result.EngineName}.");

            OcrText = result.Text;
            Confidence = result.Confidence;

            AddLog($"OCR completed. Confidence: {Confidence:P2}");

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                AddLog("No text recognized — try a larger region / different window, or disable preprocessing for clean screenshots.");
            }
            else if (result.Text.Length > 0)
            {
                var preview = result.Text.Length > 200
                    ? result.Text.Substring(0, 200) + "..."
                    : result.Text;
                AddLog($"Recognized text ({result.BoundingBoxes.Count} word(s)):\n{preview}");
            }

            // Check low confidence
            if (Confidence < LowConfidenceThreshold)
            {
                AddLog($"WARNING: Low confidence ({Confidence:P2}). Automation may be unreliable.");
            }

            StatusMessage = $"OCR done ({Confidence:P0} confidence)";
        }
        catch (Exception ex)
        {
            AddLog($"Error during OCR: {ex.Message}");
            StatusMessage = "OCR failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(OcrText))
        {
            AddLog("No OCR text to copy.");
            return;
        }

        try
        {
            Clipboard.SetText(OcrText);
            AddLog("Text copied to clipboard.");
            StatusMessage = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            AddLog($"Error copying to clipboard: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RunAutomationAsync()
    {
        if (string.IsNullOrWhiteSpace(OcrText))
        {
            AddLog("No OCR text available. Please run OCR first.");
            return;
        }

        // Check if target window is elevated
        if (_lastTargetWindow.HasValue)
        {
            Win32Api.GetWindowThreadProcessId(_lastTargetWindow.Value, out int pid);
            if (ProcessElevationChecker.IsProcessElevated(pid))
            {
                AddLog("ERROR: Target window is elevated. Automation blocked for security.");
                AddLog("To automate elevated windows, run this application as Administrator.");
                StatusMessage = "Blocked: elevated window";
                MessageBox.Show(
                    "The target window is running elevated (as Administrator).\n" +
                    "For security, automation of elevated windows is blocked.\n" +
                    "Please run this application as Administrator to enable automation.",
                    "Elevated Window Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        // Check low confidence warning
        if (Confidence < LowConfidenceThreshold)
        {
            var result = MessageBox.Show(
                $"OCR confidence is low ({Confidence:P0}).\n" +
                "Automation may produce incorrect results.\n\n" +
                "Do you want to continue?",
                "Low Confidence Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                AddLog("Automation cancelled by user (low confidence).");
                return;
            }
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Running automation...";
            AddLog("Matching OCR text against rules...");

            var actions = _mappingService.MatchText(OcrText);

            if (actions.Count == 0)
            {
                AddLog("No matching automation rules found.");
                StatusMessage = "No matching rules";
                return;
            }

            AddLog($"Found {actions.Count} action(s) to execute.");

            await _automationService.ExecuteActionsAsync(actions, _lastTargetWindow);

            AddLog("Automation completed successfully.");
            StatusMessage = "Automation complete";
        }
        catch (UnauthorizedAccessException ex)
        {
            AddLog($"ERROR: Access denied - {ex.Message}");
            AddLog("Run as Administrator to automate this window.");
            StatusMessage = "Access denied";
        }
        catch (Exception ex)
        {
            AddLog($"Error during automation: {ex.Message}");
            StatusMessage = "Automation failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (!_automationService.CanUndo)
        {
            AddLog("Nothing to undo.");
            return;
        }

        try
        {
            await _automationService.UndoLastActionAsync();
            AddLog("Last action undone.");
            StatusMessage = "Action undone";
        }
        catch (Exception ex)
        {
            AddLog($"Error undoing action: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadMappingsAsync()
    {
        try
        {
            var filePath = Path.Combine(Utilities.ResourceExtractor.AppBaseDirectory(), "sample_mappings.json");
            await _mappingService.LoadRulesAsync(filePath);

            MappingRules.Clear();
            foreach (var rule in _mappingService.Rules.Rules)
            {
                MappingRules.Add(rule);
            }

            AddLog($"Loaded {MappingRules.Count} mapping rule(s).");
        }
        catch (Exception ex)
        {
            AddLog($"Error loading mappings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveMappingsAsync()
    {
        var errors = _mappingService.ValidateRules();
        if (errors.Count > 0)
        {
            foreach (var error in errors) AddLog($"Rule validation error: {error}");
            AddLog($"Save blocked: {errors.Count} rule validation error(s). Fix the rules and try again.");
            StatusMessage = "Rule validation failed";
            return;
        }

        try
        {
            var filePath = Path.Combine(Utilities.ResourceExtractor.AppBaseDirectory(), "sample_mappings.json");
            await _mappingService.SaveRulesAsync(filePath);
            AddLog("Mappings saved successfully.");
        }
        catch (Exception ex)
        {
            AddLog($"Error saving mappings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ArrangeRows()
    {
        if (string.IsNullOrWhiteSpace(OcrText))
        {
            AddLog("No OCR text to arrange into rows.");
            return;
        }
        var rows = OcrText
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l.Trim(), @"\s{2,}", " "))
            .Where(l => l.Length > 0)
            .ToList();
        OcrText = string.Join(Environment.NewLine, rows);
        AddLog($"Arranged into {rows.Count} clean row(s) — notepad view updated.");
        StatusMessage = $"{rows.Count} rows";
    }

    [RelayCommand]
    private void SaveRaster()
    {
        if (_lastCapturedBitmap == null)
        {
            AddLog("Nothing to save — capture a window or region first.");
            return;
        }
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "OcrAutomation");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            _lastCapturedBitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            AddLog($"Raster saved: {path}");
            StatusMessage = "Raster saved";
        }
        catch (Exception ex)
        {
            AddLog($"Error saving raster: {ex.Message}");
        }
    }

    private static Models.OcrResult PickBetterOcrResult(Models.OcrResult a, Models.OcrResult b)
    {
        static int Score(Models.OcrResult r) => r == null ? -1 : (r.Text ?? string.Empty).Trim().Length;
        int sa = Score(a), sb = Score(b);
        if (sb > sa) { b.EngineName = (b.EngineName ?? "OCR") + " (preprocessed)"; return b; }
        a.EngineName = (a.EngineName ?? "OCR") + " (raw)";
        return a;
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import file to raster",
            Filter = "PDF and images (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|PDF files (*.pdf)|*.pdf|Image files (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            IsBusy = true;
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            ImportedFileName = Path.GetFileName(dlg.FileName);

            if (ext == ".pdf")
            {
                _importedPdfBytes = await File.ReadAllBytesAsync(dlg.FileName);
                PdfPageCount = PDFtoImage.Conversion.GetPageCount(_importedPdfBytes);
                PdfPage = 1;
                AddLog($"PDF imported: {ImportedFileName} ({PdfPageCount} page(s)). Rendering at {SelectedRenderDpi} DPI.");
                await RenderPdfPageAsync(0);
            }
            else
            {
                _importedPdfBytes = null;
                PdfPageCount = 0;
                PdfPage = 0;
                DisposeCapturedImage();
                using var tmp = new Bitmap(dlg.FileName);
                _lastCapturedBitmap = new Bitmap(tmp);
                _lastTargetWindow = null;
                CapturedImage = ConvertBitmapToBitmapSource(_lastCapturedBitmap);
                AddLog($"Image imported: {ImportedFileName} ({_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}). Ready for OCR.");
                StatusMessage = "Import complete";
            }
        }
        catch (Exception ex)
        {
            AddLog($"Error importing file: {ex.Message}");
            StatusMessage = "Import failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RenderPdfPageAsync(int zeroBasedPage)
    {
        if (_importedPdfBytes == null || PdfPageCount == 0) return;
        zeroBasedPage = Math.Clamp(zeroBasedPage, 0, PdfPageCount - 1);
        try
        {
            IsBusy = true;
            StatusMessage = $"Rendering page {zeroBasedPage + 1}...";
            var bytes = _importedPdfBytes;
            int dpi = SelectedRenderDpi;
            var pngBytes = await Task.Run(() =>
            {
                using var sk = PDFtoImage.Conversion.ToImage(bytes, page: zeroBasedPage, dpi: dpi);
                using var data = sk.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            });
            using var ms = new MemoryStream(pngBytes);
            using var tmp = new Bitmap(ms);
            DisposeCapturedImage();
            _lastCapturedBitmap = new Bitmap(tmp);
            _lastTargetWindow = null;
            CapturedImage = ConvertBitmapToBitmapSource(_lastCapturedBitmap);
            PdfPage = zeroBasedPage + 1;
            AddLog($"Page {PdfPage}/{PdfPageCount} rasterized at {dpi} DPI ({_lastCapturedBitmap.Width}x{_lastCapturedBitmap.Height}). CAD-ready PNG via Save Raster.");
            StatusMessage = "Import complete";
        }
        catch (Exception ex)
        {
            AddLog($"Error rendering PDF page: {ex.Message}");
            StatusMessage = "Render failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PdfNextAsync()
    {
        if (_importedPdfBytes == null || PdfPage >= PdfPageCount) return;
        await RenderPdfPageAsync(PdfPage);
    }

    [RelayCommand]
    private async Task PdfPrevAsync()
    {
        if (_importedPdfBytes == null || PdfPage <= 1) return;
        await RenderPdfPageAsync(PdfPage - 2);
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add($"[{timestamp}] {message}");
        });
    }

    private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            Win32Api.DeleteObject(hBitmap);
        }
    }
}
