using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OcrAutomation.Services.Interfaces;

namespace OcrAutomation.Services;

public class PreprocessingPipeline : IPreprocessingPipeline
{
    private bool _enableGrayscale = true;
    private bool _enableContrastEnhancement = true;
    private bool _enableAdaptiveThreshold = false;
    private bool _enableDeskew = false;
    private bool _enableSharpening = false;
    private bool _enableUpscaleSmall = true;

    public void EnableGrayscale(bool enabled) => _enableGrayscale = enabled;
    public void EnableContrastEnhancement(bool enabled) => _enableContrastEnhancement = enabled;
    public void EnableAdaptiveThreshold(bool enabled) => _enableAdaptiveThreshold = enabled;
    public void EnableDeskew(bool enabled) => _enableDeskew = enabled;
    public void EnableSharpening(bool enabled) => _enableSharpening = enabled;
    public void EnableUpscaleSmall(bool enabled) => _enableUpscaleSmall = enabled;

    public Bitmap Process(Bitmap input)
    {
        // Upscale small screen captures first — Tesseract needs ~300 DPI equivalent.
        Bitmap working = input;
        bool ownsWorking = false;
        if (_enableUpscaleSmall && input.Height < 800)
        {
            double scale = Math.Min(3.0, 800.0 / Math.Max(1, input.Height));
            int w = Math.Max(1, (int)(input.Width * scale));
            int h = Math.Max(1, (int)(input.Height * scale));
            working = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(working))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(input, 0, 0, w, h);
            }
            ownsWorking = true;
        }
        using var mat = BitmapConverter.ToMat(working);
        using var processed = new Mat();

        mat.CopyTo(processed);

        // Step 1: Convert to grayscale
        if (_enableGrayscale && processed.Channels() > 1)
        {
            Cv2.CvtColor(processed, processed, ColorConversionCodes.BGR2GRAY);
        }

        // Step 2: Enhance contrast using CLAHE
        if (_enableContrastEnhancement)
        {
            using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new OpenCvSharp.Size(8, 8));
            clahe.Apply(processed, processed);
        }

        // Step 3: Adaptive threshold (black text on white background)
        if (_enableAdaptiveThreshold)
        {
            Cv2.AdaptiveThreshold(
                processed,
                processed,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                blockSize: 11,
                c: 2);

            // Invert back so text is black on white for Tesseract
            Cv2.BitwiseNot(processed, processed);
        }

        // Step 4: Deskew (detect and correct text orientation)
        if (_enableDeskew)
        {
            processed.CopyTo(processed);
            var angle = DetectSkewAngle(processed);
            if (Math.Abs(angle) > 0.5) // Only rotate if angle is significant
            {
                var center = new Point2f(processed.Width / 2f, processed.Height / 2f);
                using var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
                Cv2.WarpAffine(processed, processed, rotationMatrix, processed.Size());
            }
        }

        // Step 5: Sharpen
        if (_enableSharpening)
        {
            var kernelData = new float[,]
            {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 }
            };
            using var kernel = Mat.FromArray(kernelData);
            Cv2.Filter2D(processed, processed, -1, kernel);
        }

        var result = BitmapConverter.ToBitmap(processed);
        if (ownsWorking) working.Dispose();
        return result;
    }

    private double DetectSkewAngle(Mat image)
    {
        try
        {
            using var edges = new Mat();
            Cv2.Canny(image, edges, 50, 150, apertureSize: 3);

            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold: 100, minLineLength: 100, maxLineGap: 10);

            if (lines == null || lines.Length == 0)
                return 0;

            var angles = new List<double>();
            foreach (var line in lines)
            {
                var angle = Math.Atan2(line.P2.Y - line.P1.Y, line.P2.X - line.P1.X) * 180 / Math.PI;
                angles.Add(angle);
            }

            // Return median angle
            angles.Sort();
            return angles[angles.Count / 2];
        }
        catch
        {
            return 0;
        }
    }
}
