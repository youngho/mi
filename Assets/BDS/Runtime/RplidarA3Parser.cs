using System;
using System.Collections.Generic;

namespace PinkSoft.BDS
{
    /// <summary>
    /// RPLIDAR EXPRESS SCAN 스타일 패킷 파서 (A3 호환 단순화).
    /// 실제 하드웨어 연동 시 Slamtec SDK 프로토콜 문서에 맞게 보정 필요.
    /// </summary>
    public sealed class RplidarA3Parser : ILidarParser
    {
        const byte SyncByte1 = 0xA5;
        const byte SyncByte2 = 0x5A;

        readonly List<byte> _accumulator = new(8192);

        public string SensorModel => "RPLIDAR-A3";

        public void Reset() => _accumulator.Clear();

        public bool TryParse(byte[] buffer, int offset, int count, out int consumed, List<LidarScanPoint> output, ulong timestampUs)
        {
            consumed = 0;
            for (int i = 0; i < count; i++)
                _accumulator.Add(buffer[offset + i]);

            int parsed = 0;
            while (_accumulator.Count >= 5)
            {
                int sync = FindSync();
                if (sync < 0)
                {
                    _accumulator.RemoveRange(0, _accumulator.Count - 1);
                    break;
                }

                if (sync > 0)
                    _accumulator.RemoveRange(0, sync);

                if (_accumulator.Count < 5)
                    break;

                byte b0 = _accumulator[0];
                byte b1 = _accumulator[1];
                byte b2 = _accumulator[2];
                byte b3 = _accumulator[3];
                byte check = _accumulator[4];

                byte calc = (byte)((b0 ^ b1 ^ b2 ^ b3) & 0x7F);
                if (check != calc)
                {
                    _accumulator.RemoveAt(0);
                    continue;
                }

                bool startFlag = (b0 & 0x01) != 0;
                float angle = ((b1 | ((b0 & 0x7F) << 8)) / 64f);
                float distance = (b2 | (b3 << 8)) / 4f;
                byte quality = (byte)((b0 >> 2) & 0x3F);

                if (distance > 0)
                {
                    output.Add(new LidarScanPoint(angle % 360f, distance, quality, timestampUs));
                    parsed++;
                }

                _accumulator.RemoveRange(0, 5);
            }

            consumed = count;
            return parsed > 0;
        }

        int FindSync()
        {
            for (int i = 0; i < _accumulator.Count - 1; i++)
            {
                if (_accumulator[i] == SyncByte1 && _accumulator[i + 1] == SyncByte2)
                    return i;
            }
            return -1;
        }
    }
}
