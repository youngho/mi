using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PinkSoft.BDS
{
    /// <summary>
    /// Raw LiDAR 포인트 녹화/재생 (.bin).
    /// 포맷: magic "BDSL" + version u32 + 반복 [angle f32, dist f32, quality u8, pad u8, timestamp u64]
    /// </summary>
    public sealed class LidarRecordingService
    {
        const uint Magic = 0x4C534442; // "BDSL" little-endian
        const uint Version = 1;

        readonly List<LidarScanPoint> _points = new();

        public int PointCount => _points.Count;

        public void RecordPoint(LidarScanPoint point) => _points.Add(point);

        public void Clear() => _points.Clear();

        public IEnumerable<LidarScanPoint> EnumeratePoints() => _points;

        public void Save(string path)
        {
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(_points.Count);
            foreach (var p in _points)
            {
                bw.Write(p.AngleDeg);
                bw.Write(p.DistanceMm);
                bw.Write(p.Quality);
                bw.Write((byte)0);
                bw.Write(p.TimestampUs);
            }
        }

        public static LidarRecordingService Load(string path)
        {
            var svc = new LidarRecordingService();
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            var magic = br.ReadUInt32();
            if (magic != Magic)
                throw new InvalidDataException("Invalid BDS log magic");
            var ver = br.ReadUInt32();
            if (ver != Version)
                throw new InvalidDataException($"Unsupported version {ver}");
            var count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var angle = br.ReadSingle();
                var dist = br.ReadSingle();
                var quality = br.ReadByte();
                br.ReadByte();
                var ts = br.ReadUInt64();
                svc._points.Add(new LidarScanPoint(angle, dist, quality, ts));
            }
            return svc;
        }

        public string ExportSummaryJson()
        {
            return JsonSerializer.Serialize(new
            {
                pointCount = _points.Count,
                durationUs = _points.Count > 0 ? _points[^1].TimestampUs - _points[0].TimestampUs : 0
            });
        }
    }
}
