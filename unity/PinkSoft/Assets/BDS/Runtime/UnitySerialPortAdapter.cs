#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
using System.IO.Ports;

namespace PinkSoft.BDS
{
    /// <summary>Unity 스탠드얼론 빌드용 UART 어댑터.</summary>
    public sealed class UnitySerialPortAdapter : SerialPortLike, System.IDisposable
    {
        readonly SerialPort _inner;

        public UnitySerialPortAdapter(string portName, int baudRate, int readBufferSize = 1024 * 1024)
        {
            _inner = new SerialPort(portName, baudRate)
            {
                ReadBufferSize = readBufferSize,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
        }

        public override bool IsOpen => _inner.IsOpen;
        public override void Open() => _inner.Open();
        public override void Close() => _inner.Close();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public void Dispose() => _inner.Dispose();
    }
}
#endif
