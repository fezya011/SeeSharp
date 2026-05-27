using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;
using System.Diagnostics;
using SeeSharp.Models.EmguCV;

namespace SeeSharp.Services;

public class FaceDetectionService : IDisposable
{
    private readonly CascadeClassifier _faceCascade;
    private readonly CascadeClassifier _eyeCascade;
    private readonly CascadeClassifier _mouthCascade;
    private readonly CascadeClassifier _noseCascade;
    private readonly CascadeClassifier _smileCascade;
    private readonly CascadeClassifier _fullBodyCascade;
    private readonly CascadeClassifier _upperBodyCascade;

    private bool _disposed = false;
    private readonly object _lock = new object();

    public FaceDetectionService()
    {
        var basePath = Directory.GetCurrentDirectory();

        var faceHaarPath = Path.Combine(basePath, "haarcascade_frontalface_default.xml");
        var eyeHaarPath = Path.Combine(basePath, "haarcascade_eye.xml");
        var mouthHaarPath = Path.Combine(basePath, "haarcascade_mcs_mouth.xml");
        var noseHaarPath = Path.Combine(basePath, "haarcascade_mcs_nose.xml");
        var smileHaarPath = Path.Combine(basePath, "haarcascade_smile.xml");
        var fullBodyPath = Path.Combine(basePath, "haarcascade_fullbody.xml");
        var upperBodyPath = Path.Combine(basePath, "haarcascade_upperbody.xml");

        _faceCascade = new CascadeClassifier(faceHaarPath);
        _eyeCascade = new CascadeClassifier(eyeHaarPath);

        if (File.Exists(mouthHaarPath))
            _mouthCascade = new CascadeClassifier(mouthHaarPath);

        if (File.Exists(noseHaarPath))
            _noseCascade = new CascadeClassifier(noseHaarPath);

        if (File.Exists(smileHaarPath))
            _smileCascade = new CascadeClassifier(smileHaarPath);

        if (File.Exists(fullBodyPath))
            _fullBodyCascade = new CascadeClassifier(fullBodyPath);

        if (File.Exists(upperBodyPath))
            _upperBodyCascade = new CascadeClassifier(upperBodyPath);
    }

    public async Task<ExtendedDetectionResult> DetectFullAsync(byte[] imageData)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = new ExtendedDetectionResult();

                try
                {
                    using var mat = new Mat();
                    CvInvoke.Imdecode(imageData, ImreadModes.AnyColor, mat);

                    if (mat.IsEmpty) return result;

                    var imageHeight = mat.Height;
                    var imageWidth = mat.Width;

                    using var smallMat = new Mat();
                    CvInvoke.Resize(mat, smallMat, new Size(mat.Width / 2, mat.Height / 2));

                    using var gray = new Mat();
                    CvInvoke.CvtColor(smallMat, gray, ColorConversion.Bgr2Gray);

                    var faces = _faceCascade.DetectMultiScale(gray, 1.05, 6);
                    var detectedFaces = new List<FaceBox>();

                    foreach (var face in faces)
                    {
                        var scaledFace = new Rectangle(
                            face.X * 2,
                            face.Y * 2,
                            face.Width * 2,
                            face.Height * 2
                        );

                        if (!IsValidFace(scaledFace, imageWidth, imageHeight)) continue;

                        var faceBox = new FaceBox
                        {
                            X = scaledFace.X,
                            Y = scaledFace.Y,
                            Width = scaledFace.Width,
                            Height = scaledFace.Height,
                        };

                        using var faceROI = new Mat(gray, face);
                        using var faceROIColor = new Mat(smallMat, face);

                        if (scaledFace.Width > 80)
                        {
                            DetectEyes(faceROI, face, scaledFace, faceBox);
                        }

                        if (_noseCascade != null && scaledFace.Width > 70)
                        {
                            DetectNose(faceROI, face, scaledFace, faceBox);
                        }

                        if (_mouthCascade != null && scaledFace.Width > 70)
                        {
                            DetectMouth(faceROI, face, scaledFace, faceBox);
                        }

                        detectedFaces.Add(faceBox);
                    }

                    if (_fullBodyCascade != null)
                    {
                        var bodies = _fullBodyCascade.DetectMultiScale(gray, 1.05, 3);
                        result.FullBodies = bodies.Select(b => new Rectangle(b.X * 2, b.Y * 2, b.Width * 2, b.Height * 2)).ToList();
                    }

                    if (_upperBodyCascade != null)
                    {
                        var upperBodies = _upperBodyCascade.DetectMultiScale(gray, 1.05, 3);
                        result.UpperBodies = upperBodies.Select(b => new Rectangle(b.X * 2, b.Y * 2, b.Width * 2, b.Height * 2)).ToList();
                    }

                    detectedFaces = RemoveOverlappingFaces(detectedFaces);
                    detectedFaces = SelectBestFace(detectedFaces);

                    result.Faces = detectedFaces;
                    result.FacesCount = detectedFaces.Count;
                    result.ProcessedImage = DrawFullDetection(mat, detectedFaces, result.FullBodies, result.UpperBodies);
                    result.ProcessingTime = stopwatch.ElapsedMilliseconds;
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }

                return result;
            }
        });
    }

    private bool IsValidFace(Rectangle face, int imageWidth, int imageHeight)
    {
        if (face.Width < 60 || face.Height < 60) return false;
        if (face.Width > imageWidth / 1.3 || face.Height > imageHeight / 1.3) return false;

        var aspectRatio = (double)face.Width / face.Height;
        if (aspectRatio < 0.65 || aspectRatio > 1.15) return false;

        var faceCenterY = face.Y + face.Height / 2;
        if (faceCenterY > imageHeight * 0.75) return false;

        if (face.X < 15 || face.Y < 15) return false;
        if (face.X + face.Width > imageWidth - 15) return false;

        return true;
    }

    private void DetectEyes(Mat faceROI, Rectangle face, Rectangle scaledFace, FaceBox faceBox)
    {
        var eyes = _eyeCascade.DetectMultiScale(faceROI, 1.05, 4);

        foreach (var eye in eyes)
        {
            if (eye.Y + eye.Height > face.Height * 0.6) continue;
            if (eye.X < 5 || eye.X + eye.Width > face.Width - 5) continue;

            var eyeCenter = new Point(
                scaledFace.X + (eye.X + eye.Width / 2) * 2,
                scaledFace.Y + (eye.Y + eye.Height / 2) * 2
            );

            faceBox.Eyes.Add(eyeCenter);
            if (faceBox.Eyes.Count >= 2) break;
        }
    }

    private void DetectNose(Mat faceROI, Rectangle face, Rectangle scaledFace, FaceBox faceBox)
    {
        var noses = _noseCascade.DetectMultiScale(faceROI, 1.05, 3);

        foreach (var nose in noses)
        {
            double noseCenterY = nose.Y + nose.Height / 2;
            double faceHeight = face.Height;

            if (noseCenterY > faceHeight * 0.3 && noseCenterY < faceHeight * 0.7)
            {
                var noseCenter = new Point(
                    scaledFace.X + (nose.X + nose.Width / 2) * 2,
                    scaledFace.Y + (nose.Y + nose.Height / 2) * 2
                );
                faceBox.Nose.Add(noseCenter);
                break;
            }
        }
    }

    private void DetectMouth(Mat faceROI, Rectangle face, Rectangle scaledFace, FaceBox faceBox)
    {
        var mouths = _mouthCascade.DetectMultiScale(faceROI, 1.05, 5);

        foreach (var mouth in mouths)
        {
            double mouthCenterY = mouth.Y + mouth.Height / 2;
            double faceHeight = face.Height;

            if (mouthCenterY > faceHeight * 0.65)
            {
                var mouthCenter = new Point(
                    scaledFace.X + (mouth.X + mouth.Width / 2) * 2,
                    scaledFace.Y + (mouth.Y + mouth.Height / 2) * 2
                );
                faceBox.Mouth.Add(mouthCenter);
                break;
            }
        }
    }

    private List<FaceBox> RemoveOverlappingFaces(List<FaceBox> faces)
    {
        if (faces.Count <= 1) return faces;

        var sorted = faces.OrderByDescending(f => f.Confidence).ToList();
        var result = new List<FaceBox>();

        foreach (var face in sorted)
        {
            bool overlaps = false;

            foreach (var existing in result)
            {
                var intersection = Rectangle.Intersect(
                    new Rectangle(face.X, face.Y, face.Width, face.Height),
                    new Rectangle(existing.X, existing.Y, existing.Width, existing.Height)
                );

                if (intersection.Width > 0 && intersection.Height > 0)
                {
                    double overlapArea = intersection.Width * intersection.Height;
                    double faceArea = face.Width * face.Height;

                    if (overlapArea / faceArea > 0.4)
                    {
                        overlaps = true;
                        break;
                    }
                }
            }

            if (!overlaps)
            {
                result.Add(face);
            }
        }

        return result;
    }

    private List<FaceBox> SelectBestFace(List<FaceBox> faces)
    {
        if (faces.Count <= 1) return faces;

        var bestFace = faces.OrderByDescending(f => f.Confidence).First();

        if (bestFace.Confidence >= 50)
        {
            return new List<FaceBox> { bestFace };
        }

        return faces;
    }

    private byte[] DrawFullDetection(Mat original, List<FaceBox> faces, List<Rectangle> fullBodies, List<Rectangle> upperBodies)
    {
        using var result = original.Clone();

        foreach (var body in fullBodies)
        {
            CvInvoke.Rectangle(result, body, new MCvScalar(255, 0, 0), 2);
        }

        foreach (var upper in upperBodies)
        {
            CvInvoke.Rectangle(result, upper, new MCvScalar(255, 255, 0), 2);
        }

        foreach (var face in faces)
        {
            var faceRect = new Rectangle(face.X, face.Y, face.Width, face.Height);

            MCvScalar color;
            if (face.Confidence >= 70)
                color = new MCvScalar(0, 255, 0);
            else if (face.Confidence >= 40)
                color = new MCvScalar(0, 255, 255);
            else
                color = new MCvScalar(0, 165, 255);

            CvInvoke.Rectangle(result, faceRect, color, 2);

            foreach (var eye in face.Eyes)
            {
                CvInvoke.Circle(result, eye, 4, new MCvScalar(0, 0, 255), 2);
                CvInvoke.Circle(result, eye, 2, new MCvScalar(0, 0, 255), -1);
            }

            foreach (var nose in face.Nose)
            {
                CvInvoke.Circle(result, nose, 5, new MCvScalar(255, 0, 0), 2);
            }

            foreach (var mouth in face.Mouth)
            {
                CvInvoke.Ellipse(result, mouth, new Size(12, 6), 0, 0, 180, new MCvScalar(255, 0, 255), 2);
            }

            if (face.IsSmiling && face.SmileRect.HasValue)
            {
                CvInvoke.Rectangle(result, face.SmileRect.Value, new MCvScalar(255, 192, 203), 3);
                CvInvoke.PutText(result, "FACE",
                    new Point(face.X, face.Y - 25),
                    FontFace.HersheySimplex, 0.7,
                    new MCvScalar(255, 192, 203), 2);
            }

            string text = $"Confidence [{face.Confidence}%]";
            CvInvoke.PutText(result, text,
                new Point(face.X, face.Y - 8),
                FontFace.HersheySimplex, 0.5,
                color, 1);
        }

        using var ms = new MemoryStream();
        result.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }


    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _faceCascade?.Dispose();
                _eyeCascade?.Dispose();
                _mouthCascade?.Dispose();
                _noseCascade?.Dispose();
                _smileCascade?.Dispose();
                _fullBodyCascade?.Dispose();
                _upperBodyCascade?.Dispose();
                _disposed = true;
            }
        }
    }
}