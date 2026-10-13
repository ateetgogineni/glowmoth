using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Glowmoth
{
    /// <summary>A built button and its parts, so screens can recolour/update them later.</summary>
    public sealed class UIButton
    {
        public Button Button;
        public Image Background;
        public Image Icon;
        public Text Label;
    }

    /// <summary>
    /// Helpers that build uGUI elements from code with the runtime atlas sprites and the built-in font.
    /// Layout uses a 1080x1920 reference canvas; positions are in reference pixels.
    /// </summary>
    public static class UIKit
    {
        static Font font;

        /// <summary>Pre-built "0".."99" strings so HUD counters never allocate when they change.</summary>
        public static readonly string[] Numbers = BuildNumbers(100);

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    // Unity 2022.2+ ships "LegacyRuntime.ttf"; older versions call it "Arial.ttf".
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
                    if (font == null)
                    {
                        try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font = null; }
                    }
                }
                return font;
            }
        }

        static string[] BuildNumbers(int count)
        {
            var s = new string[count];
            for (int i = 0; i < count; i++) s[i] = i.ToString();
            return s;
        }

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors the element at a normalised point of its parent, offset by `pos`, with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sliced)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.6f; // ~40px rounded corners on the reference canvas
            }
            return img;
        }

        public static Text MakeText(Transform parent, string name, string value, int size, Color color,
                                    TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Rounded button with an optional icon and/or label. Plays the UI click sound.</summary>
        public static UIButton MakeButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color bg,
                                          UnityAction onClick, string label = null, Sprite icon = null, Color? fg = null, int fontSize = 56)
        {
            var result = new UIButton();
            Image img = MakeImage(parent, name, SpriteFactory.Panel, bg, true, true);
            Place(img.rectTransform, anchor, pos, size);
            result.Background = img;

            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxSynth.Click, 0.6f);
                onClick();
            });
            img.gameObject.AddComponent<PressFx>();
            result.Button = button;

            Color c = fg ?? Color.white;
            if (icon != null)
            {
                result.Icon = MakeImage(img.transform, "Icon", icon, c);
                float s = Mathf.Min(size.x, size.y) * 0.5f;
                Place(result.Icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s));
            }
            if (label != null)
            {
                result.Label = MakeText(img.transform, "Label", label, fontSize, c);
                Stretch(result.Label.rectTransform);
            }
            return result;
        }
    }

    /// <summary>Squash on press, spring back on release. Uses unscaled time so it works while paused.</summary>
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float target = 1f, current = 1f;

        public void OnPointerDown(PointerEventData e) { target = 0.92f; }
        public void OnPointerUp(PointerEventData e) { target = 1f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }

        void OnDisable()
        {
            target = current = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (current == target) return; // idle buttons cost one comparison per frame
            current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(current, current, 1f);
        }
    }

    /// <summary>Fades a screen in/out with a CanvasGroup and a slight scale; deactivates it when hidden.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFader : MonoBehaviour
    {
        CanvasGroup group;
        float target;

        void Awake() { group = GetComponent<CanvasGroup>(); }

        public bool IsShown => target > 0f;

        public void Show()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (!gameObject.activeSelf)
            {
                group.alpha = 0f;
                gameObject.SetActive(true);
            }
            target = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            target = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void HideInstant()
        {
            Hide();
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (group.alpha == target)
            {
                if (target == 0f) gameObject.SetActive(false);
                return;
            }
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            float s = 0.97f + 0.03f * group.alpha;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Keeps UI inside the device safe area (notches, rounded corners, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        RectTransform rt;
        Rect applied;

        void Awake()
        {
            rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = applied.position, max = applied.position + applied.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
