using System.IO;
using OpenCvSharp;

namespace FlipKit.Desktop.Services
{
    public static class OcrImagePreprocessor
    {
        // Returns a temp PNG path with grayscale + CLAHE contrast applied.
        // Caller must delete the returned file after use.
        public static string Preprocess(string imagePath)
        {
            using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (mat.Empty())
                return imagePath;

            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

            var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, new Size(8, 8));
            using var enhanced = new Mat();
            clahe.Apply(gray, enhanced);

            var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
            Cv2.ImWrite(tempPath, enhanced);
            return tempPath;
        }
    }
}
