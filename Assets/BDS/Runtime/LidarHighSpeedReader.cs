using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PinkSoft.BDS
{
  public enum LidarReaderState
  {
    Stopped,
    Running,
    Reconnecting,
    Error
  }

  public sealed class LidarHighSpeedReader : IDisposable
  {
    readonly object _portLock = new();
    readonly ILidarParser _parser;
    readonly ConcurrentQueue<LidarScanPoint> _pointQueue = new();
    readonly List<LidarScanPoint> _parseBuffer = new(256);
    readonly CancellationTokenSource _cts = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();

    Thread? _worker;
    SerialPortLike? _port;

    public int ReadBufferSize { get; set; } = 1024 * 1024;
    public int ReconnectDelayMs { get; set; } = 2000;
    public LidarReaderState State { get; private set; } = LidarReaderState.Stopped;

    public long BytesReceived { get; private set; }
    public long PointsParsed { get; private set; }
    public long OverflowCount { get; private set; }

    public event Action<LidarScanPoint>? OnScanPoint;
    public event Action<Exception>? OnError;

    public LidarHighSpeedReader(SerialPortLike port, ILidarParser parser)
    {
      _port = port;
      _parser = parser;
    }

    public void Start()
    {
      if (State == LidarReaderState.Running)
        return;

      State = LidarReaderState.Running;
      _worker = new Thread(WorkerLoop)
      {
        IsBackground = true,
        Name = "LidarHighSpeedReader",
        Priority = ThreadPriority.AboveNormal
      };
      _worker.Start();
    }

    public void Stop()
    {
      State = LidarReaderState.Stopped;
      _cts.Cancel();
      _worker?.Join(3000);
      _worker = null;
      lock (_portLock)
      {
        _port?.Close();
      }
    }

    void WorkerLoop()
    {
      var readBuf = new byte[4096];
      while (!_cts.IsCancellationRequested && State != LidarReaderState.Stopped)
      {
        try
        {
          lock (_portLock)
          {
            if (_port is { IsOpen: false })
              _port.Open();
          }

          int read;
          lock (_portLock)
          {
            read = _port!.Read(readBuf, 0, readBuf.Length);
          }

          if (read <= 0)
          {
            Thread.Sleep(1);
            continue;
          }

          BytesReceived += read;
          var ts = (ulong)(_clock.Elapsed.Ticks / 10L);
          _parseBuffer.Clear();

          if (_parser.TryParse(readBuf, 0, read, out _, _parseBuffer, ts))
          {
            foreach (var p in _parseBuffer)
            {
              PointsParsed++;
              OnScanPoint?.Invoke(p);
              if (_pointQueue.Count > 100_000)
              {
                OverflowCount++;
                _pointQueue.TryDequeue(out _);
              }
              _pointQueue.Enqueue(p);
            }
          }
        }
        catch (Exception ex)
        {
          OnError?.Invoke(ex);
          State = LidarReaderState.Reconnecting;
          Thread.Sleep(ReconnectDelayMs);
          try
          {
            lock (_portLock)
            {
              _port?.Close();
              _port?.Open();
            }
            State = LidarReaderState.Running;
          }
          catch (Exception rex)
          {
            OnError?.Invoke(rex);
            State = LidarReaderState.Error;
            break;
          }
        }
      }
    }

    public bool TryDequeue(out LidarScanPoint point) => _pointQueue.TryDequeue(out point);

    public int QueueDepth => _pointQueue.Count;

    public void Dispose()
    {
      Stop();
      _cts.Dispose();
    }
  }

  /// <summary>System.IO.Ports.SerialPort 래퍼 추상화 (Unity/콘솔 공용).</summary>
  public abstract class SerialPortLike
  {
    public abstract bool IsOpen { get; }
    public abstract void Open();
    public abstract void Close();
    public abstract int Read(byte[] buffer, int offset, int count);
  }
}

#if !UNITY_EDITOR && !UNITY_STANDALONE && !UNITY_IOS && !UNITY_ANDROID
namespace PinkSoft.BDS
{
  public sealed class SystemSerialPortAdapter : SerialPortLike, IDisposable
  {
    readonly System.IO.Ports.SerialPort _inner;

    public SystemSerialPortAdapter(System.IO.Ports.SerialPort inner) => _inner = inner;

    public override bool IsOpen => _inner.IsOpen;
    public override void Open() => _inner.Open();
    public override void Close() => _inner.Close();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public void Dispose() => _inner.Dispose();
  }
}
#endif
