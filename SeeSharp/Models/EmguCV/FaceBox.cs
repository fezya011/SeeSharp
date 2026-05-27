using System.Drawing;

namespace SeeSharp.Models.EmguCV
{
    public class FaceBox
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public string? GazeDirection { get; set; }
        public double GazeAngle { get; set; }

        public Point Center => new Point(X + Width / 2, Y + Height / 2);
        public double Area => Width * Height;

        public List<Point> Eyes { get; set; } = new();
        public List<Point> Mouth { get; set; } = new();
        public List<Point> Nose { get; set; } = new();

        // Дополнительные признаки
        public bool IsSmiling { get; set; }
        public Rectangle? SmileRect { get; set; }

        // Уверенность детекции (0-100)
        public int Confidence => CalculateConfidence();

        private int CalculateConfidence()
        {
            int score = 0;
            if (Eyes.Count >= 2) score += 40;
            else if (Eyes.Count == 1) score += 20;

            if (Nose.Count > 0) score += 30;

            if (Mouth.Count > 0) score += 30;
            else if (IsSmiling) score += 20;

            return Math.Min(100, score);
        }

        public string GetExpression()
        {
            if (IsSmiling) return "Улыбается";
            if (Eyes.Count >= 2 && Mouth.Count > 0) return "Нейтральное";
            if (Eyes.Count < 2) return "Лицо";
            return "Лицо";
        }
    }
}