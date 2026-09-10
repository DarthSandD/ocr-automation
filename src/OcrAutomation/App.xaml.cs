using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OcrAutomation.Services;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Services.OcrEngines;
using OcrAutomation.ViewModels;
using OcrAutomation.Utilities;

namespace OcrAutomation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Extract tessdata if not present
        try
        {
            var tessdataPath = Path.Combine(ResourceExtractor.AppBaseDirectory(), "tessdata");
            ResourceExtractor.ExtractTessdata(tessdataPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Warning: Could not extract Tesseract data: {ex.Message}\nThe application will continue but OCR may not work.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Setup DI
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IPreprocessingPipeline, PreprocessingPipeline>();
        services.AddSingleton<IAutomationService, AutomationService>();
        services.AddSingleton<IMappingService, MappingService>();

        // Register OCR engine (Tesseract only)
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();

        // Register ViewModels
        services.AddTransient<MainViewModel>();

        // Register Views
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

