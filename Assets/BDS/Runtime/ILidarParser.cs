using System.Collections.Generic;

namespace PinkSoft.BDS
{
    public interface ILidarParser
    {
        string SensorModel { get; }
        void Reset();
        bool TryParse(byte[] buffer, int offset, int count, out int consumed, List<LidarScanPoint> output, ulong timestampUs);
    }
}
