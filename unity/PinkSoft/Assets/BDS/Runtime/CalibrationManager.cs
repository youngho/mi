using System.Text.Json;

namespace PinkSoft.BDS
{
    [Serializable]
    public class CalibrationData
    {
        public string sensorModel = "RPLIDAR-A3";
        public long calibratedAtUnixMs;
        public float[,] lidarCorners = new float[4, 2];
        public float[,] screenCorners = new float[4, 2];
        public double[] homography = Array.Empty<double>();

        public void ComputeHomography()
        {
            var src = new double[4, 2];
            var dst = new double[4, 2];
            for (int i = 0; i < 4; i++)
            {
                src[i, 0] = lidarCorners[i, 0];
                src[i, 1] = lidarCorners[i, 1];
                dst[i, 0] = screenCorners[i, 0];
                dst[i, 1] = screenCorners[i, 1];
            }
            homography = HomographyCalculator.Compute(src, dst);
            calibratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public (float u, float v) MapLidarToScreen(float lidarX, float lidarY)
        {
            if (homography.Length != 9)
                return (0, 0);
            return HomographyCalculator.Transform(homography, lidarX, lidarY);
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static CalibrationData Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CalibrationData>(json)
                   ?? throw new InvalidDataException("Invalid calibration JSON");
        }
    }

    public sealed class CalibrationManager
    {
        public CalibrationData Data { get; private set; } = new();
        public int CurrentCornerIndex { get; private set; }
        public bool IsComplete => CurrentCornerIndex >= 4 && Data.homography.Length == 9;

        static readonly (float u, float v)[] DefaultScreenCorners =
        {
            (0f, 1f), (1f, 1f), (1f, 0f), (0f, 0f)
        };

        public void BeginSession()
        {
            Data = new CalibrationData();
            CurrentCornerIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                Data.screenCorners[i, 0] = DefaultScreenCorners[i].u;
                Data.screenCorners[i, 1] = DefaultScreenCorners[i].v;
            }
        }

        public void RegisterCornerShot(float lidarX, float lidarY)
        {
            if (CurrentCornerIndex >= 4)
                return;
            Data.lidarCorners[CurrentCornerIndex, 0] = lidarX;
            Data.lidarCorners[CurrentCornerIndex, 1] = lidarY;
            CurrentCornerIndex++;
            if (CurrentCornerIndex == 4)
                Data.ComputeHomography();
        }

        /// <summary>터치/디버그 입력용 — 화면 정규 좌표로 4점 교정.</summary>
        public void RegisterCornerFromScreen(float normU, float normV)
        {
            if (CurrentCornerIndex >= 4)
                return;
            Data.lidarCorners[CurrentCornerIndex, 0] = normU;
            Data.lidarCorners[CurrentCornerIndex, 1] = normV;
            Data.screenCorners[CurrentCornerIndex, 0] = normU;
            Data.screenCorners[CurrentCornerIndex, 1] = normV;
            CurrentCornerIndex++;
            if (CurrentCornerIndex == 4)
                Data.ComputeHomography();
        }

        public void Reset() => BeginSession();

        public void LoadFromFile(string path)
        {
            Data = CalibrationData.Load(path);
            CurrentCornerIndex = 4;
        }

        public void SaveToFile(string path) => Data.Save(path);
    }
}
