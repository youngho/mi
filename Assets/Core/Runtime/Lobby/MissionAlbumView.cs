using System;
using PinkSoft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 전술 태블릿: 가운데 미션 카드 스트립 + 하단 선택 상세.
    /// </summary>
    public sealed class MissionAlbumView : MonoBehaviour
    {
        [Header("Featured (center)")]
        [SerializeField] Text featuredBadgeText = null!;
        [SerializeField] Text featuredTitleText = null!;
        [SerializeField] Text featuredBodyText = null!;
        [SerializeField] Text featuredMetaText = null!;
        [SerializeField] Image featuredCardImage = null!;
        [SerializeField] Button deployButton = null!;

        [Header("Strip")]
        [SerializeField] RectTransform stripContent = null!;
        [SerializeField] ScrollRect stripScroll = null!;
        [SerializeField] GameObject tilePrefab = null!;

        [Header("Look")]
        [SerializeField] Color cardIdle = Color.white;
        [SerializeField] Color cardSelected = new(0.72f, 0.94f, 1f, 1f);
        [SerializeField] Color accent = new(0.35f, 0.82f, 0.95f, 1f);
        [SerializeField] Color textPrimary = new(0.95f, 0.94f, 0.92f, 1f);
        [SerializeField] Color textMuted = new(0.62f, 0.64f, 0.66f, 1f);

        PinkSoftApiClient.MissionMeta[] _missions = Array.Empty<PinkSoftApiClient.MissionMeta>();
        int _selected;
        Action<PinkSoftApiClient.MissionMeta>? _onDeploy;
        readonly System.Collections.Generic.List<TileView> _tiles = new();

        public int SelectedIndex => _selected;
        public PinkSoftApiClient.MissionMeta? Selected =>
            _missions.Length == 0 ? null : _missions[Mathf.Clamp(_selected, 0, _missions.Length - 1)];

        public void BindDeploy(Action<PinkSoftApiClient.MissionMeta> onDeploy)
        {
            _onDeploy = onDeploy;
            if (deployButton != null)
            {
                deployButton.onClick.RemoveAllListeners();
                deployButton.onClick.AddListener(() =>
                {
                    if (Selected != null)
                        _onDeploy?.Invoke(Selected);
                });
            }
        }

        public void SetMissions(PinkSoftApiClient.MissionMeta[] missions, int recommendedIndex = 0)
        {
            _missions = missions ?? Array.Empty<PinkSoftApiClient.MissionMeta>();
            _selected = _missions.Length == 0 ? 0 : Mathf.Clamp(recommendedIndex, 0, _missions.Length - 1);
            RebuildStrip();
            RefreshFeatured();
            ScrollSelectedIntoView();
        }

        public void Select(int index)
        {
            if (_missions.Length == 0)
                return;
            _selected = Mathf.Clamp(index, 0, _missions.Length - 1);
            RefreshFeatured();
            for (var i = 0; i < _tiles.Count; i++)
                _tiles[i].SetSelected(i == _selected);
            ScrollSelectedIntoView();
        }

        void RebuildStrip()
        {
            foreach (Transform child in stripContent)
                Destroy(child.gameObject);
            _tiles.Clear();

            if (tilePrefab == null || stripContent == null)
                return;

            for (var i = 0; i < _missions.Length; i++)
            {
                var go = Instantiate(tilePrefab, stripContent);
                go.SetActive(true);
                var tile = go.GetComponent<TileView>() ?? go.AddComponent<TileView>();
                var idx = i;
                tile.Setup(_missions[i], accent, textPrimary, textMuted, cardIdle, cardSelected,
                    () => Select(idx));
                tile.SetSelected(i == _selected);
                _tiles.Add(tile);
            }
        }

        void RefreshFeatured()
        {
            if (_missions.Length == 0)
            {
                if (featuredBadgeText != null) featuredBadgeText.text = "미션";
                if (featuredTitleText != null) featuredTitleText.text = "카탈로그 없음";
                if (featuredBodyText != null) featuredBodyText.text = "서버에서 미션을 불러오지 못했습니다.";
                if (featuredMetaText != null) featuredMetaText.text = "";
                if (deployButton != null) deployButton.interactable = false;
                return;
            }

            var m = _missions[_selected];
            if (featuredBadgeText != null)
                featuredBadgeText.text = "선택됨";
            if (featuredTitleText != null)
                featuredTitleText.text = string.IsNullOrEmpty(m.title) ? m.missionId : m.title;
            if (featuredBodyText != null)
                featuredBodyText.text = string.IsNullOrEmpty(m.description)
                    ? "상세 설명이 없습니다."
                    : m.description;
            if (featuredMetaText != null)
            {
                var time = m.timeLimit > 0 ? $"{m.timeLimit}초" : "—";
                var target = m.targetScore > 0 ? m.targetScore.ToString() : "—";
                featuredMetaText.text =
                    $"{m.category}  ·  Lv.{m.requiredLevel}+  ·  {time}  ·  목표 {target}\n{m.missionId}";
            }

            // 상세 패널은 씬 스프라이트를 유지. 색을 덮어쓰지 않는다.
            if (deployButton != null)
                deployButton.interactable = true;
        }

        void ScrollSelectedIntoView()
        {
            if (stripScroll == null || stripContent == null || _tiles.Count == 0)
                return;
            Canvas.ForceUpdateCanvases();
            var n = _tiles.Count;
            if (n <= 1)
            {
                stripScroll.horizontalNormalizedPosition = 0f;
                return;
            }

            stripScroll.horizontalNormalizedPosition = (float)_selected / (n - 1);
        }

        /// <summary>스트립 타일. 프리팹 또는 런타임 생성.</summary>
        public sealed class TileView : MonoBehaviour
        {
            Image? _bg;
            Text? _title;
            Text? _sub;
            Outline? _outline;
            Color _idle;
            Color _selected;

            public void Setup(
                PinkSoftApiClient.MissionMeta mission,
                Color accent,
                Color primary,
                Color muted,
                Color idle,
                Color selected,
                Action onClick)
            {
                _idle = idle;
                _selected = selected;
                _bg = GetComponent<Image>();
                if (_bg == null)
                    _bg = gameObject.AddComponent<Image>();

                var btn = GetComponent<Button>();
                if (btn == null)
                    btn = gameObject.AddComponent<Button>();
                btn.targetGraphic = _bg;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick());

                _title = transform.Find("Title")?.GetComponent<Text>();
                _sub = transform.Find("Sub")?.GetComponent<Text>();
                if (_title != null)
                {
                    _title.text = string.IsNullOrEmpty(mission.title) ? mission.missionId : mission.title;
                    _title.color = primary;
                }

                if (_sub != null)
                {
                    _sub.text = mission.category;
                    _sub.color = muted;
                }

                _outline = GetComponent<Outline>();
                if (_outline == null)
                    _outline = gameObject.AddComponent<Outline>();
                _outline.effectColor = accent;
                _outline.effectDistance = new Vector2(2.5f, -2.5f);
                SetSelected(false);
            }

            public void SetSelected(bool on)
            {
                if (_bg != null)
                    _bg.color = on ? _selected : _idle;
                if (_outline != null)
                    _outline.enabled = on;
            }
        }
    }
}
