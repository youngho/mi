namespace PinkSoft.BDS
{
    /// <summary>정규화된 LiDAR 스캔 포인트 (각도°, 거리 mm).</summary>
    public readonly struct LidarScanPoint
    {
        public readonly float AngleDeg;
        public readonly float DistanceMm;
        public readonly byte Quality;
        public readonly ulong TimestampUs;

        public LidarScanPoint(float angleDeg, float distanceMm, byte quality, ulong timestampUs)
        {
            AngleDeg = angleDeg;
            DistanceMm = distanceMm;
            Quality = quality;
            TimestampUs = timestampUs;
        }
    }
}
