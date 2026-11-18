using System;
using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// All menu screens, built in code (no scene editing). Screen-space overlay, portrait layout
    /// designed at 1080x1920 and scaled. Positions are given as fractions of the (safe-area) screen.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        public event Action ContinueClicked, NewGameClicked, PlaceInFrontClicked, PickClicked, BackClicked,
                            DeathMatchClicked, ResultContinueClicked;

        // colours
        static readonly Color Dim      = new Color(0.03f, 0.04f, 0.07f, 0.62f);
        static readonly Color Strip    = new Color(0.03f, 0.04f, 0.07f, 0.78f);
        static readonly Color Green    = new Color(0.17f, 0.52f, 0.30f, 1f);
        static readonly Color Blue     = new Color(0.20f, 0.36f, 0.62f, 1f);
        static readonly Color Grey     = new Color(0.30f, 0.32f, 0.38f, 1f);
        static readonly Color BarBack  = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color BarFill  = new Color(0.96f, 0.76f, 0.26f, 1f);
        static readonly Color SoftWhite = new Color(0.92f, 0.94f, 0.98f, 1f);

        Canvas _canvas;
        CanvasScaler _scaler;
        RectTransform _safe;
        Rect _lastSafe;

        GameObject _titlePanel, _placePanel, _rosterPanel, _hubPanel, _resultPanel;

        // title
        TMP_Text _titleText;
        Button _continueBtn;
        TMP_Text _continueLabel;

        // roster
        GameObject _card, _pickBtn, _backBtn;
        TMP_Text _cardName, _cardTier, _cardStyle;
        readonly RectTransform[] _statFill = new RectTransform[4];
        readonly TMP_Text[] _statValue = new TMP_Text[4];
        static readonly string[] StatNames = { "MIGHT", "EGO", "EDUCATION", "INSIGHT" };

        // hub
        TMP_Text _hubName, _hubInfo, _hubProf;

        // result
        TMP_Text _resultTitle, _resultSub, _resultBtnLabel;

        // ------------------------------------------------------------------ build
        void Awake()
        {
            _instance = this;
            EnsureEventSystem();
            BuildCanvas();
            BuildTitle();
            BuildPlace();
            BuildRoster();
            BuildHub();
            BuildResult();
            HideAll();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafe) ApplySafeArea();
            AdaptScaler();
            HandleDirectTaps();
        }

        // The layout is designed for portrait (1080x1920). Portrait: scale by width. Landscape: scale by height,
        // so titles aren't cut off and cards/buttons don't overlap.
        void AdaptScaler()
        {
            if (_scaler == null) return;
            float target = Screen.width > Screen.height ? 1f : 0f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, target)) _scaler.matchWidthOrHeight = target;
        }

        // ------------------------------------------------------------------ direct tap handling
        // Buttons are also hit-tested here from raw touch/mouse input, so they work even if Unity's EventSystem
        // never delivers the click on a device. The EventSystem path still works; Raise() ignores a second click
        // within 0.3 s so a button never fires twice.
        static GameUI _instance;
        Button _pressedButton;
        float _lastFire = -10f;

        public static bool IsPointOverButton(Vector2 screenPos)
        {
            return _instance != null && _instance.ButtonAt(screenPos) != null;
        }

        void Raise(Action handler)
        {
            if (Time.unscaledTime - _lastFire < 0.3f) return;
            _lastFire = Time.unscaledTime;
            if (handler != null) handler();
        }

        Button ButtonAt(Vector2 screenPos)
        {
            if (_canvas == null) return null;
            var buttons = _canvas.GetComponentsInChildren<Button>(false);
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var b = buttons[i];
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, screenPos, null)) return b;
            }
            return null;
        }

        void HandleDirectTaps()
        {
            Vector2 pos;
            if (PointerPressed(out pos)) _pressedButton = ButtonAt(pos);

            if (_pressedButton != null && PointerReleased(out pos))
            {
                var over = ButtonAt(pos);
                if (over == _pressedButton) over.onClick.Invoke();
                _pressedButton = null;
            }
        }

        static bool PointerPressed(out Vector2 pos)
        {
            pos = default(Vector2);
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) { pos = mouse.position.ReadValue(); return true; }
            return false;
        }

        static bool PointerReleased(out Vector2 pos)
        {
            pos = default(Vector2);
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasReleasedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame) { pos = mouse.position.ReadValue(); return true; }
            return false;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        void BuildCanvas()
        {
            // A GameUI_Canvas saved into a prefab by accident would show up as a dead duplicate menu: remove it.
            var stale = transform.Find("GameUI_Canvas");
            if (stale != null) DestroyImmediate(stale.gameObject);

            var go = new GameObject("GameUI_Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            _scaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            _safe = NewRect("SafeArea", go.transform);
            Stretch(_safe);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            _lastSafe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = _lastSafe.position;
            Vector2 max = _lastSafe.position + _lastSafe.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;
            _safe.anchorMin = min;
            _safe.anchorMax = max;
            _safe.offsetMin = Vector2.zero;
            _safe.offsetMax = Vector2.zero;
        }

        void BuildTitle()
        {
            _titlePanel = NewPanel("TitlePanel", Dim, true);
            var rt = _titlePanel.transform;
            _titleText = NewText(rt, "Title", "ARENA", 130f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(_titleText.rectTransform, 0.5f, 0.72f, new Vector2(1000f, 200f));

            var sub = NewText(rt, "Subtitle", "Pick a fighter. Win the arena.", 44f, SoftWhite, TextAlignmentOptions.Center);
            Anchor(sub.rectTransform, 0.5f, 0.64f, new Vector2(1000f, 80f));

            _continueBtn = NewButton(rt, "ContinueButton", "Continue", 0.5f, 0.42f, new Vector2(720f, 140f), Green, out _continueLabel);
            _continueBtn.onClick.AddListener(() => Raise(ContinueClicked));

            TMP_Text l;
            var nb = NewButton(rt, "NewGameButton", "New Game", 0.5f, 0.31f, new Vector2(720f, 140f), Blue, out l);
            nb.onClick.AddListener(() => Raise(NewGameClicked));
        }

        void BuildPlace()
        {
            _placePanel = NewPanel("PlacePanel", Color.clear, false);
            var rt = _placePanel.transform;

            var strip = NewImage(rt, "HintStrip", Strip, false);
            Anchor(strip.rectTransform, 0.5f, 0.24f, new Vector2(1000f, 170f));

            var hint = NewText(strip.transform, "Hint", "Aim at a table and tap to place the arena", 44f, Color.white, TextAlignmentOptions.Center);
            Stretch(hint.rectTransform);

            TMP_Text l;
            var b = NewButton(rt, "PlaceInFrontButton", "Place in front of me", 0.5f, 0.13f, new Vector2(720f, 120f), Grey, out l);
            b.onClick.AddListener(() => Raise(PlaceInFrontClicked));
        }

        void BuildRoster()
        {
            _rosterPanel = NewPanel("RosterPanel", Color.clear, false);
            var rt = _rosterPanel.transform;

            var header = NewText(rt, "Header", "Choose your fighter", 64f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(header.rectTransform, 0.5f, 0.945f, new Vector2(1000f, 100f));
            var hint = NewText(rt, "Hint", "Tap a fighter to inspect them", 38f, SoftWhite, TextAlignmentOptions.Center);
            Anchor(hint.rectTransform, 0.5f, 0.905f, new Vector2(1000f, 70f));

            // ---- stat card
            var cardImg = NewImage(rt, "Card", Strip, false);
            _card = cardImg.gameObject;
            Anchor(cardImg.rectTransform, 0.5f, 0.70f, new Vector2(960f, 700f));
            var card = cardImg.rectTransform;

            _cardName  = NewText(card, "Name",  "", 62f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Frac(_cardName.rectTransform, 0.05f, 0.84f, 0.95f, 0.98f);
            _cardTier  = NewText(card, "Tier",  "", 40f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Frac(_cardTier.rectTransform, 0.05f, 0.73f, 0.95f, 0.84f);
            _cardStyle = NewText(card, "Style", "", 36f, SoftWhite, TextAlignmentOptions.Left);
            Frac(_cardStyle.rectTransform, 0.05f, 0.63f, 0.95f, 0.73f);

            for (int i = 0; i < 4; i++)
            {
                float top = 0.59f - i * 0.145f;
                float bottom = top - 0.125f;

                var label = NewText(card, "Label" + i, StatNames[i], 34f, SoftWhite, TextAlignmentOptions.Left);
                Frac(label.rectTransform, 0.05f, bottom, 0.36f, top);

                var back = NewImage(card, "Bar" + i, BarBack, false);
                Frac(back.rectTransform, 0.38f, bottom + 0.03f, 0.84f, top - 0.03f);

                var fill = NewImage(back.transform, "Fill", BarFill, false);
                Frac(fill.rectTransform, 0f, 0f, 0.5f, 1f);
                _statFill[i] = fill.rectTransform;

                var val = NewText(card, "Value" + i, "0", 40f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                Frac(val.rectTransform, 0.85f, bottom, 0.96f, top);
                _statValue[i] = val;
            }

            // ---- buttons
            TMP_Text l;
            var pick = NewButton(rt, "PickButton", "Pick this fighter", 0.5f, 0.16f, new Vector2(760f, 140f), Green, out l);
            pick.onClick.AddListener(() => Raise(PickClicked));
            _pickBtn = pick.gameObject;

            var back2 = NewButton(rt, "BackButton", "Back to selection", 0.5f, 0.075f, new Vector2(760f, 110f), Grey, out l);
            back2.onClick.AddListener(() => Raise(BackClicked));
            _backBtn = back2.gameObject;
        }

        void BuildHub()
        {
            _hubPanel = NewPanel("HubPanel", Color.clear, false);
            var rt = _hubPanel.transform;

            _hubName = NewText(rt, "HubName", "", 64f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(_hubName.rectTransform, 0.5f, 0.935f, new Vector2(1000f, 100f));
            _hubInfo = NewText(rt, "HubInfo", "", 38f, SoftWhite, TextAlignmentOptions.Center);
            Anchor(_hubInfo.rectTransform, 0.5f, 0.89f, new Vector2(1000f, 70f));
            _hubProf = NewText(rt, "HubProf", "", 28f, SoftWhite, TextAlignmentOptions.Center);
            Anchor(_hubProf.rectTransform, 0.5f, 0.845f, new Vector2(1040f, 90f));

            TMP_Text l;
            var dm = NewButton(rt, "DeathMatchButton", "Death Match", 0.5f, 0.24f, new Vector2(760f, 140f), Green, out l);
            dm.onClick.AddListener(() => Raise(DeathMatchClicked));

            var duel = NewButton(rt, "DuelButton", "Duel  (coming soon)", 0.5f, 0.145f, new Vector2(760f, 110f), Grey, out l);
            duel.interactable = false;
            var group = NewButton(rt, "GroupButton", "Group Fight  (coming soon)", 0.5f, 0.065f, new Vector2(760f, 110f), Grey, out l);
            group.interactable = false;
        }

        void BuildResult()
        {
            // No full-screen dimmer: the tombstone / winner stays visible in AR behind the text.
            _resultPanel = NewPanel("ResultPanel", Color.clear, false);
            var rt = _resultPanel.transform;

            _resultTitle = NewText(rt, "ResultTitle", "", 100f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(_resultTitle.rectTransform, 0.5f, 0.93f, new Vector2(1000f, 160f));

            var strip = NewImage(rt, "ResultStrip", Strip, false);
            Anchor(strip.rectTransform, 0.5f, 0.815f, new Vector2(1000f, 300f));
            _resultSub = NewText(strip.transform, "ResultSub", "", 42f, SoftWhite, TextAlignmentOptions.Center);
            Stretch(_resultSub.rectTransform);

            var b = NewButton(rt, "ResultButton", "Continue", 0.5f, 0.10f, new Vector2(760f, 140f), Blue, out _resultBtnLabel);
            b.onClick.AddListener(() => Raise(ResultContinueClicked));
        }

        // ------------------------------------------------------------------ screens
        public void HideAll()
        {
            _titlePanel.SetActive(false);
            _placePanel.SetActive(false);
            _rosterPanel.SetActive(false);
            _hubPanel.SetActive(false);
            _resultPanel.SetActive(false);
        }

        public void ShowTitle(string title, bool canContinue, string continueLabel)
        {
            HideAll();
            _titleText.text = title;
            _continueBtn.interactable = canContinue;
            _continueLabel.text = continueLabel;
            _titlePanel.SetActive(true);
        }

        public void ShowPlace()
        {
            HideAll();
            _placePanel.SetActive(true);
        }

        public void ShowRoster()
        {
            HideAll();
            _rosterPanel.SetActive(true);
            HideRosterCard();
        }

        public void ShowRosterCard(FighterData d)
        {
            _cardName.text = d.displayName;
            _cardName.color = FighterTiers.Tint(d.tier);
            _cardTier.text = FighterTiers.Label(d.tier) + "   -   Level " + d.level;
            _cardTier.color = FighterTiers.Tint(d.tier);
            _cardStyle.text = "Prefers:  " + d.PreferredStyleLabel +
                (d.hasPreferredClass ? "   (proficiency Lv " + ProgressionRules.ProfLevel(d, ProgressionRules.StyleKey(d.preferredClass)) + ")" : "");

            SetStat(0, d.might);
            SetStat(1, d.ego);
            SetStat(2, d.education);
            SetStat(3, d.insight);

            _card.SetActive(true);
            _pickBtn.SetActive(true);
            _backBtn.SetActive(true);
        }

        public void HideRosterCard()
        {
            _card.SetActive(false);
            _pickBtn.SetActive(false);
            _backBtn.SetActive(false);
        }

        void SetStat(int i, int value)
        {
            _statValue[i].text = value.ToString();
            var r = _statFill[i];
            r.anchorMax = new Vector2(Mathf.Clamp01(value / (float)FighterGenerator.StatMax), 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        public void ShowHub(FighterData d, int totalWins)
        {
            HideAll();
            _hubName.text = d.displayName;
            _hubName.color = FighterTiers.Tint(d.tier);
            _hubInfo.text = FighterTiers.Label(d.tier) + "  -  Level " + d.level + "  (XP " + d.xp + " / " + ProgressionRules.XpToNext(d.level) + ")  -  " + d.wins + " win" + (d.wins == 1 ? "" : "s");
            _hubProf.text = ProgressionRules.ProfLine(d);
            _hubPanel.SetActive(true);
        }

        public void ShowResult(string title, Color titleColor, string subtitle, string buttonLabel)
        {
            HideAll();
            _resultTitle.text = title;
            _resultTitle.color = titleColor;
            _resultSub.text = subtitle;
            _resultBtnLabel.text = buttonLabel;
            _resultPanel.SetActive(true);
        }

        // ------------------------------------------------------------------ builders
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Fixed-size element centred at (ax, ay) as fractions of the parent.
        static void Anchor(RectTransform rt, float ax, float ay, Vector2 size)
        {
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        // Element filling a fractional rectangle of the parent.
        static void Frac(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        GameObject NewPanel(string name, Color background, bool blocksTaps)
        {
            var img = NewImage(_safe, name, background, blocksTaps);
            Stretch(img.rectTransform);
            return img.gameObject;
        }

        static Image NewImage(Transform parent, string name, Color color, bool raycast)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        static TMP_Text NewText(Transform parent, string name, string text, float size, Color color,
                                TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }

        static Button NewButton(Transform parent, string name, string label, float ax, float ay, Vector2 size,
                                Color color, out TMP_Text labelText)
        {
            var img = NewImage(parent, name, color, true);
            Anchor(img.rectTransform, ax, ay, size);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            labelText = NewText(img.transform, "Label", label, 46f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(labelText.rectTransform);
            return btn;
        }
    }
}
