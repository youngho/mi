using System;
using System.IO;
using System.Text.Json;

namespace PinkSoft.BDS
{
    public class CalibrationData
    {
        public string sensorModel = "RPLIDAR-A3";
        public long calibratedAtUnixMs;
        public float[] lidarCorners = new float[8];
        public float[] screenCorners = new float[8];
        public double[] homography = Array.Empty<double>();

        static int CornerIndex(int corner, int axis) => corner * 2 + axis;

        public float GetLidarCorner(int corner, int axis) => lidarCorners[CornerIndex(corner, axis)];
        public float GetScreenCorner(int corner, int axis) => screenCorners[CornerIndex(corner, axis)];

        public void SetLidarCorner(int corner, float x, float y)
        {
            lidarCorners[CornerIndex(corner, 0)] = x;
            lidarCorners[CornerIndex(corner, 1)] = y;
        }

        public void SetScreenCorner(int corner, float u, float v)
        {
            screenCorners[CornerIndex(corner, 0)] = u;
            screenCorners[CornerIndex(corner, 1)] = v;
        }

        public void ComputeHomography()
        {
            var src = new double[4, 2];
            var dst = new double[4, 2];
            for (int i = 0; i < 4; i++)
            {
                src[i, 0] = GetLidarCorner(i, 0);
                src[i, 1] = GetLidarCorner(i, 1);
                dst[i, 0] = GetScreenCorner(i, 0);
                dst[i, 1] = GetScreenCorner(i, 1);
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
                Data.SetScreenCorner(i, DefaultScreenCorners[i].u, DefaultScreenCorners[i].v);
        }

        public void RegisterCornerShot(float lidarX, float lidarY)
        {
            if (CurrentCornerIndex >= 4)
                return;
            Data.SetLidarCorner(CurrentCornerIndex, lidarX, lidarY);
            CurrentCornerIndex++;
            if (CurrentCornerIndex == 4)
                Data.ComputeHomography();
        }

        /// <summary>터치/디버그 입력용 — 화면 정규 좌표로 4점 교정.</summary>
        public void RegisterCornerFromScreen(float normU, float normV)
        {
            if (CurrentCornerIndex >= 4)
                return;
            Data.SetLidarCorner(CurrentCornerIndex, normU, normV);
            Data.SetScreenCorner(CurrentCornerIndex, normU, normV);
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
