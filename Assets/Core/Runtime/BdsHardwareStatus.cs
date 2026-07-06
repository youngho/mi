namespace PinkSoft.Core
{
    public readonly struct BdsHardwareStatus
    {
        public string InputSourceName { get; init; }
        public bool IsHardwareInput { get; init; }
        public bool IsReaderConnected { get; init; }
        public string ReaderState { get; init; }
        public long BytesReceived { get; init; }
        public long PointsParsed { get; init; }
        public int QueueDepth { get; init; }
        public bool IsCalibrated { get; init; }
        public bool CalibrationModeActive { get; init; }
    }
}
