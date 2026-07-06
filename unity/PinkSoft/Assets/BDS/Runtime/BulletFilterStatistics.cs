namespace PinkSoft.BDS
{
    /// <summary>실탄 500발 등 대량 시험 통계.</summary>
    public sealed class BulletFilterStatistics
    {
        int _shotsFired;
        int _detections;
        int _doubleDetections;
        readonly List<long> _latenciesUs = new();
        ulong _lastDetectionFrame = ulong.MaxValue;

        public int ShotsFired => _shotsFired;
        public int Detections => _detections;
        public float DetectionRate => _shotsFired > 0 ? (float)_detections / _shotsFired : 0f;
        public float DoubleDetectionRate => _detections > 0 ? (float)_doubleDetections / _detections : 0f;

        public void RegisterShotFired() => _shotsFired++;

        public void RegisterDetection(ulong frameId, ulong timestampUs, ulong shotTimestampUs)
        {
            _detections++;
            if (_lastDetectionFrame == frameId)
                _doubleDetections++;
            _lastDetectionFrame = frameId;
            if (shotTimestampUs > 0 && timestampUs >= shotTimestampUs)
                _latenciesUs.Add((long)(timestampUs - shotTimestampUs));
        }

        public BulletFilterReport BuildReport()
        {
            return new BulletFilterReport
            {
                ShotsFired = _shotsFired,
                Detections = _detections,
                DetectionRate = DetectionRate,
                DoubleDetectionRate = DoubleDetectionRate,
                AverageLatencyMs = _latenciesUs.Count > 0
                    ? _latenciesUs.Average() / 1000.0
                    : 0,
                MedianLatencyMs = _latenciesUs.Count > 0
                    ? Percentile(_latenciesUs, 0.5) / 1000.0
                    : 0
            };
        }

        static double Percentile(List<long> values, double p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int idx = (int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }
    }

    public class BulletFilterReport
    {
        public int ShotsFired { get; init; }
        public int Detections { get; init; }
        public float DetectionRate { get; init; }
        public float DoubleDetectionRate { get; init; }
        public double AverageLatencyMs { get; init; }
        public double MedianLatencyMs { get; init; }

        public override string ToString() =>
            $"Shots={ShotsFired} Detections={Detections} Rate={DetectionRate:P1} Double={DoubleDetectionRate:P1} AvgLatency={AverageLatencyMs:F1}ms";
    }
}
