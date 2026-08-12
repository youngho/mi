#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PinkSoft.Core.BdsCheck
{
    /// <summary>
    /// macOS Teensy USB Serial (/dev/cu.usb*).
    /// USB CDC는 baud가 무의미하고, 잘못된 termios 패딩이 수신을 막을 수 있어
    /// 포트 open + DTR 토글 + raw read/write만 한다.
    /// </summary>
    public sealed class PosixSerialSession : IDisposable
    {
        const int O_RDWR = 0x2;
        const int O_NOCTTY = 0x20000;
        const int O_NONBLOCK = 0x4;

        // Darwin ttycom.h
        const ulong TIOCCDTR = 0x20007478;
        const ulong TIOCSDTR = 0x20007479;
        const ulong TIOCMGET = 0x4004746a;
        const ulong TIOCMBIS = 0x8004746c;
        const int TIOCM_DTR = 0x0002;
        const int TIOCM_RTS = 0x0004;

        [DllImport("libc", SetLastError = true)]
        static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int oflag);

        [DllImport("libc", SetLastError = true)]
        static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        static extern long read(int fd, byte[] buffer, ulong count);

        [DllImport("libc", SetLastError = true)]
        static extern long write(int fd, byte[] buffer, ulong count);

        [DllImport("libc", SetLastError = true)]
        static extern int ioctl(int fd, ulong request);

        [DllImport("libc", SetLastError = true)]
        static extern int ioctl(int fd, ulong request, ref int value);

        int _fd = -1;
        readonly object _ioLock = new();

        public bool IsOpen
        {
            get
            {
                lock (_ioLock)
                    return _fd >= 0;
            }
        }

        public string PortName { get; private set; } = "";
        public int BaudRate { get; private set; } = 115200;
        public long BytesRead { get; private set; }

        public static List<string> ListUsbPorts()
        {
            var list = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                if (Directory.Exists("/dev"))
                {
                    foreach (var path in Directory.GetFiles("/dev", "cu.usb*"))
                        list.Add(path);
                }
            }
            catch
            {
                // ignore
            }

            return new List<string>(list);
        }

        public void Open(string portName, int baudRate = 115200)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("portName required", nameof(portName));

            Close();

            // Teensy USB CDC: baud/termios 불필요. 잘못된 tcsetattr이 수신을 깨뜨렸음.
            var fd = open(portName, O_RDWR | O_NOCTTY | O_NONBLOCK);
            if (fd < 0)
            {
                var err = Marshal.GetLastWin32Error();
                throw new IOException(
                    $"open({portName}) failed errno={err}. " +
                    "Arduino Serial Monitor가 같은 포트를 열고 있으면 닫으세요.");
            }

            try
            {
                PulseDtr(fd);
            }
            catch
            {
                // DTR 실패해도 open은 유지 (일부 드라이버는 ioctl 미지원)
            }

            lock (_ioLock)
            {
                _fd = fd;
                PortName = portName;
                BaudRate = baudRate;
                BytesRead = 0;
            }
        }

        static void PulseDtr(int fd)
        {
            // Arduino Serial Monitor와 같이 DTR을 한번 떨어졌다 올려 Teensy CDC를 “연결” 상태로.
            ioctl(fd, TIOCCDTR);
            Thread.Sleep(50);

            var bits = TIOCM_DTR | TIOCM_RTS;
            ioctl(fd, TIOCMBIS, ref bits);
            ioctl(fd, TIOCSDTR);
            Thread.Sleep(100);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            int fd;
            lock (_ioLock)
            {
                if (_fd < 0)
                    return 0;
                fd = _fd;
            }

            byte[] target = buffer;
            var useTmp = offset != 0;
            if (useTmp)
                target = new byte[count];

            var n = read(fd, target, (ulong)count);
            if (n < 0)
            {
                var err = Marshal.GetLastWin32Error();
                // EAGAIN / EWOULDBLOCK
                if (err == 35 || err == 11)
                    return 0;
                throw new IOException($"read failed errno={err}");
            }

            if (n > 0)
            {
                if (useTmp)
                    Buffer.BlockCopy(target, 0, buffer, offset, (int)n);
                BytesRead += n;
            }

            return (int)n;
        }

        public void WriteLine(string text)
        {
            var payload = Encoding.ASCII.GetBytes((text ?? "") + "\n");
            WriteAll(payload);
        }

        void WriteAll(byte[] payload)
        {
            int fd;
            lock (_ioLock)
            {
                if (_fd < 0)
                    throw new InvalidOperationException("Serial not open");
                fd = _fd;
            }

            var total = 0;
            while (total < payload.Length)
            {
                var remain = payload.Length - total;
                var chunk = new byte[remain];
                Buffer.BlockCopy(payload, total, chunk, 0, remain);

                var n = write(fd, chunk, (ulong)chunk.Length);
                if (n < 0)
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err == 35 || err == 11)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    throw new IOException($"write failed errno={err}");
                }

                if (n == 0)
                {
                    Thread.Sleep(5);
                    continue;
                }

                total += (int)n;
            }
        }

        public void Close()
        {
            int fd;
            lock (_ioLock)
            {
                fd = _fd;
                _fd = -1;
            }

            if (fd >= 0)
                close(fd);
        }

        public void Dispose() => Close();
    }
}
#else
using System;
using System.Collections.Generic;

namespace PinkSoft.Core.BdsCheck
{
    /// <summary>비-macOS 스텁.</summary>
    public sealed class PosixSerialSession : IDisposable
    {
        public bool IsOpen => false;
        public string PortName => "";
        public int BaudRate => 115200;
        public long BytesRead => 0;

        public static List<string> ListUsbPorts() => new();

        public void Open(string portName, int baudRate = 115200) =>
            throw new PlatformNotSupportedException("Teensy serial monitor supports macOS only.");

        public int Read(byte[] buffer, int offset, int count) => 0;
        public void WriteLine(string text) { }
        public void Close() { }
        public void Dispose() { }
    }
}
#endif
