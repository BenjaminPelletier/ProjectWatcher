using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp.Extensions;
using Json.Serialization;

namespace ProjectWatcher
{
    class ManagedCamera
    {
        public class StableImageEventArgs : EventArgs
        {
            public readonly string Filename;

            public StableImageEventArgs(string filename)
            {
                Filename = filename;
            }
        }

        public CameraData Descriptor;
        private string DescriptorFilename;

        private FrameSource Source;
        private DirectoryInfo HistoryDirectory;

        private DateTime LastConsideredTime = DateTime.Now;

        private DateTime LastSaveTime = DateTime.Now;
        private Mat LastSaveForComparison;
        private string LastSaveFilename;

        private Mat LastStableForComparison;

        public event EventHandler<FrameSource.NewFrameEventArgs> NewFrame;
        public event EventHandler<StableImageEventArgs> NewStableImage;

        private readonly TimeSpan CONSIDERATION_INTERVAL = TimeSpan.FromSeconds(1);
        private readonly TimeSpan MIN_SAVE_INTERVAL = TimeSpan.FromSeconds(5);
        private readonly TimeSpan MIN_STABLE_DURATION = TimeSpan.FromSeconds(30);
        private readonly TimeSpan START_UP_TIME = TimeSpan.FromSeconds(10);
        private const float MEGAPIXEL_COMPARE_CEILING = 0.5f;
        private const float SUBSTANTIVE_DIFFERENCE_THRESHOLD = 1e-5f;
        private const float ZERO_CHANGE_THRESHOLD = 80;

        public const string CAMERA_DATA_FILENAME = "camera_data.json";

        public ManagedCamera(string name, string moniker, int index, int width, int height)
        {
            Descriptor = new CameraData(name, moniker);

            HistoryDirectory = new DirectoryInfo(name);
            if (!HistoryDirectory.Exists) HistoryDirectory.Create();
            DescriptorFilename = Path.Combine(HistoryDirectory.FullName, CAMERA_DATA_FILENAME);
            if (File.Exists(DescriptorFilename))
            {
                Descriptor = JsonTranslator.Singleton.MakeObject<CameraData>(JsonObject.Parse(File.ReadAllText(DescriptorFilename)));

                string latestFilename = null;
                DateTime latestTimestamp = DateTime.MinValue;
                foreach (FileInfo fi in HistoryDirectory.GetFiles("*.jpg"))
                {
                    DateTime? t = fi.Name.ToDateTime();
                    if (!t.HasValue) continue;
                    if (t.Value > latestTimestamp)
                    {
                        latestFilename = fi.Name;
                        latestTimestamp = t.Value;
                    }
                }
                if (latestFilename != null)
                {
                    using (Mat m = Mat.FromImageData(File.ReadAllBytes(Path.Combine(HistoryDirectory.FullName, latestFilename)), ImreadModes.AnyColor)) {
                        UpdateLastSave(m, latestFilename, latestTimestamp);
                    }
                }

                if (Descriptor.StableImages.Count > 0)
                {
                    var filenames = Descriptor.StableImages.ToArray();
                    string stableFilename = filenames[filenames.IndexOfMax(f => f.ToDateTime().Value)];
                    using (Mat m = Mat.FromImageData(File.ReadAllBytes(Path.Combine(HistoryDirectory.FullName, stableFilename)), ImreadModes.AnyColor))
                    {
                        LastStableForComparison = MakeComparisonImage(m, LastSaveForComparison.Width, LastSaveForComparison.Height);
                    }
                }
            }
            else
            {
                Descriptor = new CameraData(name, moniker);
                Save();
            }

            Source = new FrameSource(index, width, height);
            Source.NewFrame += (sender, e) => NewFrame?.Invoke(this, e);
        }

        public void Start()
        {
            LastConsideredTime = DateTime.Now + START_UP_TIME;
            Source.Start();
        }

        public void Save()
        {
            File.WriteAllText(DescriptorFilename, JsonTranslator.Singleton.MakeJson<CameraData>(Descriptor).ToMultilineString());
        }

        public void Stop()
        {
            Source.Stop();
        }

        public static Mat MakeComparisonImage(Mat src, int width, int height)
        {
            Mat comparison = src.Resize(new OpenCvSharp.Size(width, height), interpolation: InterpolationFlags.Area);
            return comparison;
        }

        public static void MakeDifferenceImage(Mat last, Mat next)
        {
            Cv2.Absdiff(next, last, next);
            Cv2.Threshold(next, next, ZERO_CHANGE_THRESHOLD, 255, ThresholdTypes.Binary);
        }

        public static double DifferenceFrom(Mat m, Mat last)
        {
            if (last == null) return double.PositiveInfinity;
            using (Mat next = MakeComparisonImage(m, last.Width, last.Height))
            {
                MakeDifferenceImage(last, next);
                return Cv2.Sum(next).ToDouble() / (next.Width * next.Height * 3 * 255);
            }
        }

        private void UpdateLastSave(Mat m, string filename, DateTime t)
        {
            var old = LastSaveForComparison;
            float scale = Math.Min(1.0f, MEGAPIXEL_COMPARE_CEILING * 1e6f / (m.Width * m.Height));
            LastSaveForComparison = MakeComparisonImage(m, (int)(m.Width * scale), (int)(m.Height * scale));
            LastSaveTime = t;
            LastSaveFilename = filename;
            if (old != null) old.Dispose();
        }

        public string AddFrame(Mat m, DateTime t)
        {
            if (t > LastSaveTime + MIN_SAVE_INTERVAL)
            {
                if (t >= LastConsideredTime + CONSIDERATION_INTERVAL)
                {
                    double diff = DifferenceFrom(m, LastSaveForComparison);
                    if (diff > SUBSTANTIVE_DIFFERENCE_THRESHOLD)
                    {
                        string filename = t.ToFilename();
                        m.ToBitmap().Save(Path.Combine(HistoryDirectory.FullName, filename));
                        UpdateLastSave(m, filename, t);
                        return filename;
                    }
                    else if (t > LastSaveTime + MIN_STABLE_DURATION)
                    {
                        // This camera has been stable for a while; see if we should count it as a new long-term-stable image
                        diff = LastStableForComparison != null ? DifferenceFrom(LastSaveForComparison, LastStableForComparison) : double.PositiveInfinity;
                        if (diff > SUBSTANTIVE_DIFFERENCE_THRESHOLD)
                        {
                            Descriptor.StableImages.Add(LastSaveFilename);
                            Save();
                            var old = LastStableForComparison;
                            LastStableForComparison = LastSaveForComparison.Clone();
                            NewStableImage?.Invoke(this, new StableImageEventArgs(LastSaveFilename));
                            if (old != null) old.Dispose();
                        }
                    }
                    LastConsideredTime = t;
                }
            }

            return null;
        }

        public Mat VisualizeDifferenceFromLastSave(Mat m)
        {
            if (LastSaveForComparison != null)
            {
                Mat diffImg = ManagedCamera.MakeComparisonImage(m, LastSaveForComparison.Width, LastSaveForComparison.Height);
                ManagedCamera.MakeDifferenceImage(LastSaveForComparison, diffImg);
                return diffImg;
            }
            else
            {
                return m.Clone();
            }
        }

        public Bitmap Load(string filename)
        {
            return new Bitmap(Path.Combine(HistoryDirectory.FullName, filename));
        }

        public IEnumerable<DateTime> GetHistory()
        {
            foreach (var fi in HistoryDirectory.GetFiles("*.jpg"))
            {
                DateTime? t = fi.Name.ToDateTime();
                if (t.HasValue)
                {
                    yield return t.Value;
                }
            }
        }

        public override string ToString()
        {
            return Descriptor.Name;
        }
    }
}
