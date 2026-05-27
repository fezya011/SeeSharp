using System.Drawing;

namespace SeeSharp.Models.EmguCV
{
    public class ExtendedDetectionResult
    {
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public int FacesCount { get; set; }
        public List<FaceBox> Faces { get; set; } = new();
        public List<Rectangle> FullBodies { get; set; } = new();
        public List<Rectangle> UpperBodies { get; set; } = new();
        public byte[]? ProcessedImage { get; set; }
        public long ProcessingTime { get; set; }
    }
}
