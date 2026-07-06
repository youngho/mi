namespace PinkSoft.BDS
{
    /// <summary>
    /// Static masking + 1-frame transient 탄환 검출.
    /// </summary>
    public sealed class LidarBulletFilter
    {
        const int BinCount = 360;
        const float DistanceToleranceMm = 30f;

        readonly float[] _background = new float[BinCount];
        readonly bool[] _hasBackground = new bool[BinCount];
        readonly Dictionary<int, (float dist, ulong ts)> _currentFrame = new();
        readonly HashSet<int> _previousBins = new();

        public float MinQuality { get; set; } = 8f;
        public float MaxDistanceMm { get; set; } = 8000f;
        public int MinBinSeparation { get; set; } = 2;

        public event Action<BulletDetection>? OnBulletDetected;

        ulong _frameId;

        public void CalibrateBackground(IReadOnlyList<LidarScanPoint> samples)
        {
            var sums = new float[BinCount];
            var counts = new int[BinCount];
            foreach (var p in samples)
            {
                if (p.DistanceMm <= 0 || p.DistanceMm > MaxDistanceMm)
                    continue;
                int bin = AngleToBin(p.AngleDeg);
                sums[bin] += p.DistanceMm;
                counts[bin]++;
            }
            for (int i = 0; i < BinCount; i++)
            {
                if (counts[i] > 0)
                {
                    _background[i] = sums[i] / counts[i];
                    _hasBackground[i] = true;
                }
            }
        }

        public void ProcessPoint(LidarScanPoint point)
        {
            if (point.Quality < MinQuality || point.DistanceMm <= 0 || point.DistanceMm > MaxDistanceMm)
                return;

            int bin = AngleToBin(point.AngleDeg);
            if (_hasBackground[bin] && Math.Abs(point.DistanceMm - _background[bin]) < DistanceToleranceMm)
                return;

            _currentFrame[bin] = (point.DistanceMm, point.TimestampUs);
        }

        public void EndScanFrame()
        {
            var transientBins = new List<int>();
            foreach (var (bin, data) in _currentFrame)
            {
                if (!_previousBins.Contains(bin))
                    transientBins.Add(bin);
            }

            foreach (var bin in transientBins)
            {
                var (dist, ts) = _currentFrame[bin];
                var (x, y) = LidarCoordinateMapper.PolarToCartesian(BinToAngle(bin), dist);
                OnBulletDetected?.Invoke(new BulletDetection(x, y, ts, _frameId));
            }

            _previousBins.Clear();
            foreach (var bin in _currentFrame.Keys)
                _previousBins.Add(bin);
            _currentFrame.Clear();
            _frameId++;
        }

        static int AngleToBin(float angle) => (int)(angle % 360f) % BinCount;
        static float BinToAngle(int bin) => bin;
    }

    public readonly struct BulletDetection
    {
        public readonly float LidarX;
        public readonly float LidarY;
        public readonly ulong TimestampUs;
        public readonly ulong FrameId;

        public BulletDetection(float lidarX, float lidarY, ulong timestampUs, ulong frameId)
        {
            LidarX = lidarX;
            LidarY = lidarY;
            TimestampUs = timestampUs;
            FrameId = frameId;
        }
    }
}
