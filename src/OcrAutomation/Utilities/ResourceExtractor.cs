using System.IO;
using System.Reflection;

namespace OcrAutomation.Utilities;

public static class ResourceExtractor
{
    /// <summary>
    /// Base directory that works in single-file publishes too
    /// (AppDomain.CurrentDomain.BaseDirectory can be null there).
    /// </summary>
    public static string AppBaseDirectory()
    {
        var dir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(dir)) return dir;
        return Path.GetDirectoryName(Environment.ProcessPath) ?? ".";
    }

    /// <summary>
    /// Extracts the embedded Tesseract training data to the specified directory.
    /// </summary>
    public static void ExtractTessdata(string targetDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "OcrAutomation.Resources.eng.traineddata";

        // Create directory if it doesn't exist
        Directory.CreateDirectory(targetDirectory);
        var outputPath = Path.Combine(targetDirectory, "eng.traineddata");

        // Skip if already extracted
        if (File.Exists(outputPath))
        {
            return;
        }

        // Get embedded resource stream
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // If embedded resource not found, check if file exists in assets folder
            var assetsPath = Path.Combine(AppBaseDirectory(), "assets", "tessdata", "eng.traineddata");
            if (File.Exists(assetsPath))
            {
                File.Copy(assetsPath, outputPath, true);
                return;
            }

            throw new FileNotFoundException($"Embedded tessdata resource not found: {resourceName}");
        }

        // Extract to target directory
        using var fileStream = File.Create(outputPath);
        stream.CopyTo(fileStream);
    }

    /// <summary>
    /// Ensures tessdata exists either from embedded resource or assets folder.
    /// </summary>
    public static string EnsureTessdataExists()
    {
        var tessdataPath = Path.Combine(AppBaseDirectory(), "tessdata");

        try
        {
            ExtractTessdata(tessdataPath);
        }
        catch (FileNotFoundException)
        {
            // If embedded resource doesn't exist, try to use assets folder directly
            var assetsPath = Path.Combine(AppBaseDirectory(), "..", "..", "assets", "tessdata");
            if (Directory.Exists(assetsPath))
            {
                return Path.GetFullPath(assetsPath);
            }

            // Last fallback: check if tessdata folder already exists
            if (Directory.Exists(tessdataPath) && File.Exists(Path.Combine(tessdataPath, "eng.traineddata")))
            {
                return tessdataPath;
            }

            throw new FileNotFoundException("Cannot locate Tesseract training data (eng.traineddata). Please ensure it exists in the assets/tessdata folder.");
        }

        return tessdataPath;
    }
}
