using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 메인 진행 CTA — Hover 105% 스케일, Pressed 눌림, Disabled 잠금 아이콘.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PrimaryCtaButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float hoverScale = 1.05f;
        [SerializeField] float pressScale = 0.97f;
        [SerializeField] float animSpeed = 14f;
        [SerializeField] Vector2 pressOffset = new(0f, -4f);
        [SerializeField] Image? faceImage;
        [SerializeField] Sprite? normalSprite;
        [SerializeField] Sprite? disabledSprite;
        [SerializeField] GameObject? lockIcon;
        [SerializeField] Shadow? dropShadow;

        Button _button = null!;
        RectTransform _rt = null!;
        Vector3 _baseScale = Vector3.one;
        Vector2 _basePos;
        float _targetScale = 1f;
        Vector2 _targetPos;
        bool _hovered;
        bool _pressed;

        void Awake()
        {
            _button = GetComponent<Button>();
            _rt = GetComponent<RectTransform>();
            _baseScale = _rt.localScale;
            _basePos = _rt.anchoredPosition;
            _targetPos = _basePos;
            if (faceImage == null)
                faceImage = GetComponent<Image>();
            ApplyInteractableVisual(_button.interactable);
        }

        void OnEnable()
        {
            _basePos = _rt.anchoredPosition;
            _targetPos = _basePos;
            _targetScale = 1f;
            _hovered = false;
            _pressed = false;
            ApplyInteractableVisual(_button != null && _button.interactable);
        }

        void Update()
        {
            if (_button == null)
                return;

            var interactable = _button.interactable;
            ApplyInteractableVisual(interactable);

            if (!interactable)
            {
                _targetScale = 1f;
                _targetPos = _basePos;
            }

            var s = Mathf.Lerp(_rt.localScale.x / Mathf.Max(0.0001f, _baseScale.x), _targetScale, Time.unscaledDeltaTime * animSpeed);
            _rt.localScale = _baseScale * s;
            _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, _targetPos, Time.unscaledDeltaTime * animSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable)
                return;
            _hovered = true;
            RefreshTargets();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            RefreshTargets();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable)
                return;
            _pressed = true;
            RefreshTargets();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            RefreshTargets();
        }

        void RefreshTargets()
        {
            if (_button == null || !_button.interactable)
            {
                _targetScale = 1f;
                _targetPos = _basePos;
                return;
            }

            if (_pressed)
            {
                _targetScale = pressScale;
                _targetPos = _basePos + pressOffset;
            }
            else if (_hovered)
            {
                _targetScale = hoverScale;
                _targetPos = _basePos;
            }
            else
            {
                _targetScale = 1f;
                _targetPos = _basePos;
            }
        }

        void ApplyInteractableVisual(bool interactable)
        {
            if (faceImage != null)
            {
                if (interactable && normalSprite != null)
                    faceImage.sprite = normalSprite;
                else if (!interactable && disabledSprite != null)
                    faceImage.sprite = disabledSprite;

                // ColorTint 보조: 활성은 선명, 비활성은 살짝 톤다운
                faceImage.color = interactable ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
            }

            if (lockIcon != null && lockIcon.activeSelf != !interactable)
                lockIcon.SetActive(!interactable);

            if (dropShadow != null)
                dropShadow.enabled = interactable;
        }
    }
}
