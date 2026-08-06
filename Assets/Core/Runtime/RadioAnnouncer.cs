using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace PinkSoft.Core
{
    /// <summary>
    /// 집결 코드 무전 멘트를 화면 자막 + OS TTS로 재생한다.
    /// </summary>
    public sealed class RadioAnnouncer : MonoBehaviour
    {
        [SerializeField] GameObject radioToast = null!;
        [SerializeField] Text radioText = null!;
        [SerializeField] float toastSeconds = 5.5f;
        [SerializeField] bool speakWithOsTts = true;

        AudioSource? _click;
        Coroutine? _hideRoutine;
        static bool _announcedThisSession;

        public static RadioAnnouncer? Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            EnsureClickSource();
            if (radioToast != null)
                radioToast.SetActive(false);
        }

        public void BindUi(GameObject? toast, Text? text)
        {
            if (toast != null)
                radioToast = toast;
            if (text != null)
                radioText = text;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ResetSessionFlag() => _announcedThisSession = false;

        /// <summary>Rendezvous 진입·게임 시작 시 집결 무전.</summary>
        public void AnnounceRendezvous(string code, bool force = false)
        {
            if (!force && _announcedThisSession)
                return;
            if (string.IsNullOrEmpty(code))
                return;

            _announcedThisSession = true;
            var phrase = RendezvousCode.ToRadioPhrase(code);
            ShowToast($"〔무전〕 {phrase}");
            PlayRadioClick();
            if (speakWithOsTts)
                SpeakOs(phrase);
        }

        void ShowToast(string message)
        {
            if (radioToast == null || radioText == null)
            {
                Debug.Log($"[Radio] {message}");
                return;
            }

            radioText.text = message;
            radioToast.SetActive(true);
            if (_hideRoutine != null)
                StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(toastSeconds);
            if (radioToast != null)
                radioToast.SetActive(false);
            _hideRoutine = null;
        }

        void EnsureClickSource()
        {
            _click = gameObject.GetComponent<AudioSource>();
            if (_click == null)
                _click = gameObject.AddComponent<AudioSource>();
            _click.playOnAwake = false;
            _click.spatialBlend = 0f;
        }

        void PlayRadioClick()
        {
            if (_click == null)
                return;
            if (_click.clip == null)
                _click.clip = BuildStaticBurst(0.18f);
            _click.pitch = Random.Range(0.92f, 1.08f);
            _click.Play();
        }

        static AudioClip BuildStaticBurst(float seconds)
        {
            var rate = 22050;
            var samples = Mathf.CeilToInt(seconds * rate);
            var clip = AudioClip.Create("RadioStatic", samples, 1, rate, false);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)samples;
                var env = t < 0.15f ? t / 0.15f : (t > 0.7f ? (1f - t) / 0.3f : 1f);
                data[i] = (Random.value * 2f - 1f) * 0.35f * env;
            }

            clip.SetData(data, 0);
            return clip;
        }

        static void SpeakOs(string phrase)
        {
            try
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/say",
                    Arguments = $"\"{EscapeShell(phrase)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                var ps = "Add-Type -AssemblyName System.Speech; " +
                         "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                         "$s.Rate = -1; $s.Speak('" + phrase.Replace("'", "''") + "');";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"" + ps.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
#else
                Debug.Log($"[Radio TTS skipped] {phrase}");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Radio] TTS failed: {ex.Message}");
            }
        }

        static string EscapeShell(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
