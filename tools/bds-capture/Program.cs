using System.IO.Ports;
using PinkSoft.BDS;

namespace PinkSoft.BdsCapture;

/// <summary>
/// Go/No-Go 실험용 CLI: record / replay / stats
/// </summary>
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "record" => Record(args),
            "replay" => Replay(args),
            "stats" => Stats(args),
            _ => PrintUsage()
        };
    }

    static int PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  bds-capture record <port> <baud> <output.bin> [seconds]");
        Console.WriteLine("  bds-capture replay <input.bin>");
        Console.WriteLine("  bds-capture stats <input.bin>");
        return 1;
    }

    static int Record(string[] args)
    {
        if (args.Length < 4)
            return PrintUsage();

        var portName = args[1];
        var baud = int.Parse(args[2]);
        var outputPath = args[3];
        var seconds = args.Length > 4 ? int.Parse(args[4]) : 30;

        using var port = new SerialPort(portName, baud)
        {
            ReadBufferSize = 1024 * 1024,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        var parser = new RplidarA3Parser();
        using var adapter = new SystemSerialPortAdapter(port);
        var reader = new LidarHighSpeedReader(adapter, parser);
        var recorder = new LidarRecordingService();

        reader.OnScanPoint += recorder.RecordPoint;
        reader.Start();

        Console.WriteLine($"Recording {seconds}s from {portName} -> {outputPath}");
        Thread.Sleep(seconds * 1000);

        reader.Stop();
        recorder.Save(outputPath);
        Console.WriteLine($"Saved {recorder.PointCount} points.");
        return 0;
    }

    static int Replay(string[] args)
    {
        if (args.Length < 2)
            return PrintUsage();

        var recorder = LidarRecordingService.Load(args[1]);
        Console.WriteLine($"Loaded {recorder.PointCount} points from {args[1]}");
        foreach (var p in recorder.EnumeratePoints().Take(20))
            Console.WriteLine($"  angle={p.AngleDeg:F2} dist={p.DistanceMm:F1} t={p.TimestampUs}");
        return 0;
    }

    static int Stats(string[] args)
    {
        if (args.Length < 2)
            return PrintUsage();

        var recorder = LidarRecordingService.Load(args[1]);
        var points = recorder.EnumeratePoints().ToList();
        var filter = new LidarBulletFilter();
        filter.CalibrateBackground(points.Where(p => p.TimestampUs < 1_000_000).ToList());

        int detections = 0;
        filter.OnBulletDetected += _ => detections++;

        ulong lastRev = 0;
        foreach (var p in points)
        {
            var rev = p.TimestampUs / 100_000;
            if (rev != lastRev)
            {
                filter.EndScanFrame();
                lastRev = rev;
            }
            filter.ProcessPoint(p);
        }
        filter.EndScanFrame();

        Console.WriteLine($"Points: {points.Count}");
        Console.WriteLine($"Transient detections: {detections}");
        return 0;
    }
}
