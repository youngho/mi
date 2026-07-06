using System;

namespace PinkSoft.BDS
{
    public static class LidarCoordinateMapper
    {
        public static (float x, float y) PolarToCartesian(float angleDeg, float distanceMm)
        {
            var rad = angleDeg * MathF.PI / 180f;
            return (distanceMm * MathF.Cos(rad), distanceMm * MathF.Sin(rad));
        }

        public static (float x, float y) ToLidarSpace(LidarScanPoint point)
            => PolarToCartesian(point.AngleDeg, point.DistanceMm);
    }
}
