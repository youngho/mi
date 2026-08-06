using UnityEngine;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 타자기처럼 잉크가 군데군데 덜 찍힌 집결 코드 표시.
    /// </summary>
    public sealed class TypewriterCodeLabel : MonoBehaviour
    {
        [SerializeField] RectTransform glyphRoot = null!;
        [SerializeField] Font typewriterFont = null!;
        [SerializeField] int fontSize = 72;
        [SerializeField] Color inkColor = new(0.18f, 0.14f, 0.10f, 0.92f);
        [SerializeField] float letterSpacing = 18f;
        [SerializeField] float maxJitterPx = 2.5f;
        [SerializeField] float maxTiltDeg = 3.5f;

        Text[] _glyphs = System.Array.Empty<Text>();

        public void SetCode(string code)
        {
            EnsureGlyphs(5);
            code = (code ?? "").ToUpperInvariant();
            for (var i = 0; i < _glyphs.Length; i++)
            {
                var ch = i < code.Length ? code[i].ToString() : "-";
                ApplyGlyph(_glyphs[i], ch, i, code.Length > 0 ? code.Length : 5);
            }
        }

        void EnsureGlyphs(int count)
        {
            if (_glyphs.Length == count && _glyphs[0] != null)
                return;

            if (glyphRoot == null)
                glyphRoot = (RectTransform)transform;

            for (var i = glyphRoot.childCount - 1; i >= 0; i--)
            {
                var child = glyphRoot.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            _glyphs = new Text[count];
            var font = typewriterFont != null
                ? typewriterFont
                : ResolveTypewriterFont();

            for (var i = 0; i < count; i++)
            {
                var go = new GameObject($"Glyph{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(glyphRoot, false);
                var t = go.GetComponent<Text>();
                t.font = font;
                t.fontSize = fontSize;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.supportRichText = false;
                _glyphs[i] = t;
            }
        }

        void ApplyGlyph(Text text, string ch, int index, int total)
        {
            // 문자별 시드 — 같은 코드면 같은 잉크 패턴 유지
            var seed = (ch[0] * 73856093) ^ (index * 19349663) ^ (total * 83492791);
            var rng = new System.Random(seed);

            var inkFail = rng.NextDouble(); // 덜 찍힘
            var alpha = inkFail < 0.12 ? 0.28f + (float)rng.NextDouble() * 0.25f
                : inkFail < 0.35 ? 0.55f + (float)rng.NextDouble() * 0.25f
                : 0.78f + (float)rng.NextDouble() * 0.22f;

            // 일부 획만 연한 느낌 — 살짝 밝게(잉크 부족)
            var wash = inkFail > 0.7 ? 0.12f : 0f;
            var c = inkColor;
            c.r = Mathf.Clamp01(c.r + wash);
            c.g = Mathf.Clamp01(c.g + wash * 0.9f);
            c.b = Mathf.Clamp01(c.b + wash * 0.7f);
            c.a = alpha;
            text.color = c;
            text.text = ch;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(fontSize * 0.95f, fontSize * 1.15f);

            var mid = (total - 1) * 0.5f;
            var x = (index - mid) * (fontSize * 0.72f + letterSpacing);
            var y = ((float)rng.NextDouble() * 2f - 1f) * maxJitterPx;
            rt.anchoredPosition = new Vector2(x + ((float)rng.NextDouble() * 2f - 1f) * maxJitterPx * 0.6f, y);
            rt.localRotation = Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() * 2f - 1f) * maxTiltDeg);
            rt.localScale = Vector3.one * (0.92f + (float)rng.NextDouble() * 0.14f);
        }

        public static Font ResolveTypewriterFont()
        {
            // macOS / Windows 공통 후보
            string[] names =
            {
                "American Typewriter",
                "Courier New",
                "Courier",
                "Special Elite",
                "Liberation Mono",
                "monospace"
            };
            foreach (var n in names)
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 64);
                if (f != null && f.fontNames != null && f.fontNames.Length > 0)
                    return f;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
