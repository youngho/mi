using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinkSoft.Core.BdsCheck
{
    /// <summary>
    /// BdsCheck용 Teensy USB Serial (115200) 모니터.
    /// 포트 선택 → Connect → ASCII 로그. inject/status 명령 전송.
    /// </summary>
    public sealed class TeensySerialMonitor : MonoBehaviour
    {
        public const int DefaultBaud = 115200;
        const int MaxLogChars = 24_000;
        const int UiDrainPerFrame = 64;

        [Header("UI")]
        [SerializeField] Dropdown portDropdown = null!;
        [SerializeField] Button refreshButton = null!;
        [SerializeField] Button connectButton = null!;
        [SerializeField] Text connectButtonLabel = null!;
        [SerializeField] Text statusLabel = null!;
        [SerializeField] Text logText = null!;
        [SerializeField] ScrollRect logScroll = null!;
        [SerializeField] InputField commandInput = null!;
        [SerializeField] Button sendButton = null!;
        [SerializeField] Button injectButton = null!;
        [SerializeField] Button statusCmdButton = null!;

        [Header("Serial")]
        [SerializeField] int baudRate = DefaultBaud;
        [SerializeField] bool autoScroll = true;

        readonly ConcurrentQueue<string> _pendingLines = new();
        readonly StringBuilder _log = new(4096);
        readonly List<string> _portNames = new();

        PosixSerialSession? _session;
        Thread? _reader;
        volatile bool _readerRunning;
        string _selectedPort = "";
        bool _uiDirty;
        bool _refocusCommand;
        string _lastSentCommand = "";
        float _lastSentAt;

        public bool IsConnected => _session != null && _session.IsOpen;

        void Awake() => WireUi();

        void Start()
        {
            EnsureLogLayout();
            RefreshPortList();
            SetStatus("대기 — USB 포트 선택 후 연결");
            
            WireShortcutButton("statusButton", "status");
            WireShortcutButton("inject 30 30Button", "inject 30 30");
            WireShortcutButton("startButton", "start");
            WireShortcutButton("stopButton", "stop");
        }

        void WireShortcutButton(string buttonName, string command)
        {
            var go = GameObject.Find(buttonName);
            if (go != null)
            {
                var btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => SendLine(command));
                }
            }
        }

        void Update()
        {
            DrainPendingLines();
            HandleCommandEnterKey();
        }

        void LateUpdate()
        {
            if (_uiDirty)
            {
                ResizeAndScrollLog();
                _uiDirty = false;
            }

            if (_refocusCommand && commandInput != null)
            {
                _refocusCommand = false;
                commandInput.ActivateInputField();
                commandInput.Select();
            }
        }

        void OnDestroy() => Disconnect();

        void OnApplicationQuit() => Disconnect();

        /// <summary>
        /// ContentSizeFitter는 legacy Text 줄바꿈 높이를 자주 못 잡는다.
        /// content/log를 top-stretch로 맞추고 높이는 ResizeAndScrollLog에서 직접 넣는다.
        /// </summary>
        void EnsureLogLayout()
        {
            if (logScroll == null || logText == null)
                return;

            foreach (var fitter in logScroll.GetComponentsInChildren<ContentSizeFitter>(true))
                Destroy(fitter);

            logScroll.horizontal = false;
            logScroll.vertical = true;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.inertia = false;

            var content = logScroll.content;
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.offsetMin = new Vector2(0f, content.offsetMin.y);
                content.offsetMax = new Vector2(0f, 0f);
            }

            var tr = logText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            tr.offsetMin = new Vector2(6f, tr.offsetMin.y);
            tr.offsetMax = new Vector2(-6f, 0f);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logText.alignment = TextAnchor.UpperLeft;
        }

        void ResizeAndScrollLog()
        {
            if (logScroll == null || logText == null)
                return;

            var viewport = logScroll.viewport != null
                ? logScroll.viewport
                : (RectTransform)logScroll.transform;
            var content = logScroll.content;
            var viewH = Mathf.Max(1f, viewport.rect.height);
            var viewW = Mathf.Max(1f, viewport.rect.width);

            var textRt = logText.rectTransform;
            var textW = Mathf.Max(1f, viewW - 12f);
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);

            var settings = logText.GetGenerationSettings(new Vector2(textW, 0f));
            var prefH = logText.cachedTextGeneratorForLayout.GetPreferredHeight(logText.text, settings)
                        / logText.pixelsPerUnit;
            if (prefH < 1f)
                prefH = logText.preferredHeight;
            var textH = Mathf.Max(prefH + 8f, viewH);
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textH);

            if (content != null)
            {
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewW);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textH);
            }

            Canvas.ForceUpdateCanvases();

            if (!autoScroll)
                return;

            // 하단 고정: normalized 0 + content pivot top일 때 anchoredPosition으로 보정
            logScroll.verticalNormalizedPosition = 0f;
            if (content != null && textH > viewH)
            {
                var pos = content.anchoredPosition;
                pos.y = textH - viewH;
                content.anchoredPosition = pos;
            }

            Canvas.ForceUpdateCanvases();
        }

        void WireUi()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(RefreshPortList);
                refreshButton.onClick.AddListener(RefreshPortList);
            }

            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(ToggleConnect);
                connectButton.onClick.AddListener(ToggleConnect);
            }

            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(SendCommandFromInput);
                sendButton.onClick.AddListener(SendCommandFromInput);
            }

            if (injectButton != null)
            {
                injectButton.onClick.RemoveAllListeners();
                injectButton.onClick.AddListener(() => SendLine("inject 30 30"));
            }

            if (statusCmdButton != null)
            {
                statusCmdButton.onClick.RemoveAllListeners();
                statusCmdButton.onClick.AddListener(() => SendLine("status"));
            }

            if (portDropdown != null)
            {
                portDropdown.onValueChanged.RemoveListener(OnPortChanged);
                portDropdown.onValueChanged.AddListener(OnPortChanged);
            }

            if (commandInput != null)
            {
                commandInput.lineType = InputField.LineType.SingleLine;
                commandInput.onEndEdit.RemoveListener(OnCommandEndEdit);
                commandInput.onEndEdit.AddListener(OnCommandEndEdit);
            }
        }

        void HandleCommandEnterKey()
        {
            if (commandInput == null || !commandInput.isFocused)
                return;

            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame)
                return;

            SendCommandFromInput();
        }

        void OnCommandEndEdit(string _)
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed ||
                kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                SendCommandFromInput();
        }

        void OnPortChanged(int index)
        {
            if (index < 0 || index >= _portNames.Count)
            {
                _selectedPort = "";
                return;
            }

            _selectedPort = _portNames[index];
        }

        public void RefreshPortList()
        {
            var previous = _selectedPort;
            _portNames.Clear();

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _portNames.AddRange(PosixSerialSession.ListUsbPorts());
#else
            SetStatus("시리얼 모니터는 macOS 전용입니다.");
#endif

            if (portDropdown != null)
            {
                portDropdown.ClearOptions();
                var options = new List<Dropdown.OptionData>(_portNames.Count);
                foreach (var name in _portNames)
                    options.Add(new Dropdown.OptionData(ShortPortLabel(name)));
                if (options.Count == 0)
                    options.Add(new Dropdown.OptionData("(포트 없음 — Refresh)"));
                portDropdown.AddOptions(options);

                var select = 0;
                if (!string.IsNullOrEmpty(previous))
                {
                    var idx = _portNames.IndexOf(previous);
                    if (idx >= 0)
                        select = idx;
                }

                portDropdown.value = select;
                portDropdown.RefreshShownValue();
                OnPortChanged(select);
            }

            SetStatus(_portNames.Count == 0
                ? "USB 시리얼 포트 없음 (Teensy 연결 후 Refresh)"
                : $"{_portNames.Count}개 포트 · baud {baudRate}");
        }

        static string ShortPortLabel(string full)
        {
            if (string.IsNullOrEmpty(full))
                return full;
            var slash = full.LastIndexOf('/');
            return slash >= 0 && slash < full.Length - 1 ? full[(slash + 1)..] : full;
        }

        public void ToggleConnect()
        {
            if (IsConnected)
                Disconnect();
            else
                Connect();
        }

        public void Connect()
        {
            if (IsConnected)
                return;

            if (string.IsNullOrEmpty(_selectedPort) || _portNames.Count == 0)
            {
                SetStatus("포트가 없습니다. Refresh 후 선택하세요.");
                return;
            }

#if !(UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX)
            SetStatus("이 빌드에서는 시리얼 미지원 (macOS 전용)");
            return;
#else
            try
            {
                var session = new PosixSerialSession();
                session.Open(_selectedPort, baudRate);
                _session = session;
                _readerRunning = true;
                _reader = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "TeensySerialReader"
                };
                _reader.Start();
                AppendLocal($"[연결] {_selectedPort} @ {baudRate}");
                AppendLocal("[안내] Arduino Serial Monitor는 반드시 닫을 것 (포트 독점)");
                SetStatus($"연결됨 · {ShortPortLabel(_selectedPort)}");
                RefreshConnectButton();
            }
            catch (Exception ex)
            {
                _session = null;
                SetStatus($"연결 실패: {ex.Message}");
                AppendLocal($"[오류] {ex.Message}");
                RefreshConnectButton();
            }
#endif
        }

        public void Disconnect()
        {
            _readerRunning = false;
            var reader = _reader;
            _reader = null;
            if (reader != null && reader.IsAlive)
            {
                try { reader.Join(500); }
                catch { /* ignore */ }
            }

            var session = _session;
            _session = null;
            if (session != null)
            {
                var bytes = session.BytesRead;
                try { session.Dispose(); }
                catch { /* ignore */ }
                AppendLocal($"[해제] 시리얼 닫힘 · 수신 {bytes}B");
            }

            SetStatus("해제됨");
            RefreshConnectButton();
        }

        void ReadLoop()
        {
            var buf = new byte[512];
            var line = new StringBuilder(256);
            var lastDiag = Environment.TickCount;
            while (_readerRunning)
            {
                var session = _session;
                if (session == null || !session.IsOpen)
                    break;

                try
                {
                    var n = session.Read(buf, 0, buf.Length);
                    if (n <= 0)
                    {
                        var now = Environment.TickCount;
                        if (unchecked(now - lastDiag) > 3000)
                        {
                            lastDiag = now;
                            _pendingLines.Enqueue($"[대기] 수신 {session.BytesRead}B — 보드 로그/명령 확인");
                        }

                        Thread.Sleep(20);
                        continue;
                    }

                    lastDiag = Environment.TickCount;
                    for (var i = 0; i < n; i++)
                    {
                        var c = (char)buf[i];
                        if (c == '\r')
                            continue;
                        if (c == '\n')
                        {
                            _pendingLines.Enqueue(line.ToString());
                            line.Clear();
                        }
                        else
                        {
                            line.Append(c);
                            if (line.Length > 2000)
                            {
                                _pendingLines.Enqueue(line.ToString());
                                line.Clear();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_readerRunning)
                        _pendingLines.Enqueue($"[읽기오류] {ex.Message}");
                    break;
                }
            }

            if (line.Length > 0)
                _pendingLines.Enqueue(line.ToString());
        }

        void DrainPendingLines()
        {
            var n = 0;
            while (n < UiDrainPerFrame && _pendingLines.TryDequeue(out var line))
            {
                AppendLine(line);
                n++;
            }
        }

        void AppendLocal(string line) => AppendLine(line);

        void AppendLine(string line)
        {
            if (_log.Length > 0)
                _log.Append('\n');
            _log.Append(line);
            if (_log.Length > MaxLogChars)
                _log.Remove(0, _log.Length - MaxLogChars);

            if (logText != null)
                logText.text = _log.ToString();
            _uiDirty = true;
        }

        void SendCommandFromInput()
        {
            if (commandInput == null)
                return;
            var cmd = commandInput.text?.Trim() ?? "";
            if (cmd.Length == 0)
                return;
            // Enter 경로(Update + onEndEdit) 중복 전송 방지
            if (cmd == _lastSentCommand && Time.unscaledTime - _lastSentAt < 0.15f)
            {
                commandInput.text = "";
                _refocusCommand = true;
                return;
            }

            _lastSentCommand = cmd;
            _lastSentAt = Time.unscaledTime;
            SendLine(cmd);
            commandInput.text = "";
            _refocusCommand = true;
        }

        public void SendLine(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;
            if (!IsConnected || _session == null)
            {
                SetStatus("연결 후에 명령을 보낼 수 있습니다.");
                return;
            }

            try
            {
                _session.WriteLine(command);
                AppendLocal($"> {command}");
            }
            catch (Exception ex)
            {
                AppendLocal($"[전송오류] {ex.Message}");
                SetStatus($"전송 실패: {ex.Message}");
            }
        }

        void SetStatus(string msg)
        {
            if (statusLabel != null)
                statusLabel.text = msg;
        }

        void RefreshConnectButton()
        {
            if (connectButtonLabel != null)
                connectButtonLabel.text = IsConnected ? "해제" : "연결";
        }
    }
}
