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
                            DeathMatchClicked, ResultContinueClicked,
                            BookTapped, BookPrevClicked, BookNextClicked, BookCloseClicked;
        /// <summary>The Book icon was dragged onto the table and let go at this screen position.</summary>
        public event Action<Vector2> BookDropped;
        /// <summary>The Duel button on the hub.</summary>
        public event Action DuelClicked;
        /// <summary>A "+" on the Stats screen: 0 MIGHT, 1 EGO, 2 EDUCATION, 3 INSIGHT.</summary>
        public event Action<int> StatPointSpent;

        // colours
        static readonly Color Dim      = new Color(0.03f, 0.04f, 0.07f, 0.62f);
        static readonly Color Strip    = new Color(0.03f, 0.04f, 0.07f, 0.78f);
        static readonly Color Green    = new Color(0.17f, 0.52f, 0.30f, 1f);
        static readonly Color Blue     = new Color(0.20f, 0.36f, 0.62f, 1f);
        static readonly Color Grey     = new Color(0.30f, 0.32f, 0.38f, 1f);
        static readonly Color BarBack  = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color BarFill  = new Color(0.96f, 0.76f, 0.26f, 1f);
        static readonly Color SoftWhite = new Color(0.92f, 0.94f, 0.98f, 1f);

        [SerializeField] Canvas _canvas;
        [SerializeField] CanvasScaler _scaler;
        [SerializeField] RectTransform _safe;
        Rect _lastSafe;

        [SerializeField] GameObject _titlePanel, _placePanel, _rosterPanel, _hubPanel, _resultPanel;

        // title
        [SerializeField] TMP_Text _titleText;
        [SerializeField] Button _continueBtn;
        [SerializeField] TMP_Text _continueLabel;
        [SerializeField] Button _newGameButton, _placeInFrontButton, _pickButton, _backButton, _deathMatchButton, _resultButton;

        // roster
        [SerializeField] GameObject _card, _pickBtn, _backBtn;
        [SerializeField] TMP_Text _cardName, _cardTier, _cardStyle;
        [SerializeField] RectTransform[] _statFill = new RectTransform[4];
        [SerializeField] TMP_Text[] _statValue = new TMP_Text[4];
        static readonly string[] StatNames = { "MIGHT", "EGO", "EDUCATION", "INSIGHT" };

        // hub
        [SerializeField] TMP_Text _hubName, _hubInfo, _hubProf;

        // result
        [SerializeField] TMP_Text _resultTitle, _resultSub, _resultBtnLabel;

        // book: an icon on the hub panel that you drag onto the table; the panel holds Back / Close / Next
        [SerializeField] GameObject _bookPanel;
        [SerializeField] RectTransform _bookIcon, _bookGhost;
        [SerializeField] Button _bookPrevButton, _bookNextButton, _bookCloseButton;
        [SerializeField] TMP_Text _bookLabel;

        // stat info: tap a stat on the roster card to see what it does (edit the wording in the Inspector)
        [SerializeField] RectTransform[] _statTap = new RectTransform[4];
        [SerializeField] GameObject _statPopup;
        [SerializeField] TMP_Text _statPopupTitle, _statPopupBody;
        [SerializeField, TextArea(3, 8)] string[] _statInfo = DefaultStatInfo();

        // settings: music + effects volume. The sliders are read from raw touch/mouse input like the buttons,
        // so they work on a phone even if the EventSystem misses the touch.
        [SerializeField] GameObject _settingsPanel;
        [SerializeField] Button _settingsCloseButton, _settingsTitleButton, _settingsHubButton;
        [SerializeField] RectTransform _musicTrack, _musicFill, _musicHandle, _fxTrack, _fxFill, _fxHandle;
        [SerializeField] TMP_Text _musicValue, _fxValue;

        // duel + stats (spend level-up points). Built by AddDuelAndStatsUi; restyle them in the GameUI prefab.
        [SerializeField] Button _duelButton;
        [SerializeField] Button _statsHubButton, _statsCloseButton;
        [SerializeField] GameObject _statsPanel;
        [SerializeField] TMP_Text _statsTitle, _statsPointsText;
        [SerializeField] TMP_Text[] _statsValue = new TMP_Text[4];
        [SerializeField] Button[] _statsPlus = new Button[4];
        FighterData _hubFighter;
        float _lastSpend = -10f;

        static string[] DefaultStatInfo()
        {
            return new[]
            {
                "Raw strength and toughness.\n\nEvery point makes each hit land about 5% harder AND adds 16 hit points to the health bar. Might decides how much punishment a fighter can dish out and soak up.",
                "Stubborn pride and stage presence.\n\nA big ego keeps swinging when hit: every point gives a 4% chance to shrug off most of the knockback in the middle of an attack instead of being thrown off balance. Proud fighters also lean toward picking up a shield.",
                "Book smarts and training.\n\nEducated fighters learn faster: every point adds about 5% more XP from a win and speeds up weapon proficiency by roughly 6%. It does nothing in the swing itself; it pays off over a whole career.",
                "Reading the fight.\n\nEvery point sharpens crit chance (+0.5%) and crit damage (+1%), attack tempo (+2%), dodge (+2.5%) and shield-block (+3%) chances, shortens the pause between swings and lets a fighter spot loose weapons from further away. Clever fighters like dual wielding.",
            };
        }

        // ------------------------------------------------------------------ build
        void Awake()
        {
            _instance = this;
            EnsureEventSystem();

            // Normal case: the menus come from the GameUI prefab (edit them there).
            // Fallback: if this component has no menus yet, build the default ones in code.
            if (_titlePanel == null) BuildAll();
            if (_bookPanel == null || _bookIcon == null) AddBookUi();   // an older GameUI prefab without the Book icon/screen: add it
            if (!HasStatInfoUi()) AddStatInfoUi();                       // same for the stat info popup
            if (_settingsPanel == null) AddSettingsUi();                 // and the Settings screen
            if (!HasDuelStatsUi()) AddDuelAndStatsUi();                  // and the Duel button + Stats screen

            WireButtons();
            HideAll();
        }

        /// <summary>
        /// Builds the default menus. Used by Tools > Kaiju > Game > Create Game Prefabs (to make the prefab)
        /// and as a fallback at runtime. Panels stay visible here so you can see and edit them in the prefab.
        /// </summary>
        public void BuildAll()
        {
            BuildCanvas();
            BuildTitle();
            BuildPlace();
            BuildRoster();
            BuildHub();
            BuildResult();
            AddBookUi();
            AddStatInfoUi();
            AddSettingsUi();
            AddDuelAndStatsUi();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafe) ApplySafeArea();
            AdaptScaler();
            HandleSettingsSliders();
            HandleStatTaps();
            HandleDirectTaps();
            HandleBookDrag();
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
            return _instance != null && (_instance.ButtonAt(screenPos) != null || _instance.IsStatUiAt(screenPos) || _instance.ModalOpen);
        }

        void Raise(Action handler)
        {
            if (Time.unscaledTime - _lastFire < 0.3f) return;
            _lastFire = Time.unscaledTime;
            if (handler != null) handler();
        }

        void WireButtons()
        {
            Wire(_continueBtn, () => Raise(ContinueClicked));
            Wire(_newGameButton, () => Raise(NewGameClicked));
            Wire(_placeInFrontButton, () => Raise(PlaceInFrontClicked));
            Wire(_pickButton, () => Raise(PickClicked));
            Wire(_backButton, () => Raise(BackClicked));
            Wire(_deathMatchButton, () => Raise(DeathMatchClicked));
            Wire(_resultButton, () => Raise(ResultContinueClicked));
            Wire(_bookPrevButton, () => Raise(BookPrevClicked));
            Wire(_bookNextButton, () => Raise(BookNextClicked));
            Wire(_bookCloseButton, () => Raise(BookCloseClicked));
            Wire(_settingsTitleButton, OpenSettings);
            Wire(_settingsHubButton, OpenSettings);
            Wire(_settingsCloseButton, CloseSettings);
            Wire(_duelButton, () => Raise(DuelClicked));
            Wire(_statsHubButton, OpenStats);
            Wire(_statsCloseButton, CloseStats);
            if (_statsPlus != null)
                for (int i = 0; i < _statsPlus.Length; i++)
                {
                    int idx = i;
                    Wire(_statsPlus[i], () => RaiseSpend(idx));
                }
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();   // only removes listeners added from code
            b.onClick.AddListener(action);
        }

        Button ButtonAt(Vector2 screenPos)
        {
            if (_canvas == null) return null;
            var buttons = _canvas.GetComponentsInChildren<Button>(false);
            // while Settings or Stats is open only its own buttons work
            GameObject modalPanel = SettingsOpen ? _settingsPanel : (StatsOpen ? _statsPanel : null);
            bool modal = modalPanel != null;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var b = buttons[i];
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                if (modal && !b.transform.IsChildOf(modalPanel.transform)) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, screenPos, null)) return b;
            }
            return null;
        }

        void HandleDirectTaps()
        {
            Vector2 pos;
            if (_popupConsumed) { _pressedButton = null; return; }   // this tap only closed / opened the stat popup
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
            if (Application.isPlaying) ApplySafeArea();
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

            TMP_Text l;
            var nb = NewButton(rt, "NewGameButton", "New Game", 0.5f, 0.31f, new Vector2(720f, 140f), Blue, out l);
            _newGameButton = nb;
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
            _placeInFrontButton = b;
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
            _pickButton = pick;
            _pickBtn = pick.gameObject;

            var back2 = NewButton(rt, "BackButton", "Back to selection", 0.5f, 0.075f, new Vector2(760f, 110f), Grey, out l);
            _backButton = back2;
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
            _deathMatchButton = dm;

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
            _resultButton = b;
        }

        // ------------------------------------------------------------------ screens
        public void HideAll()
        {
            _titlePanel.SetActive(false);
            _placePanel.SetActive(false);
            _rosterPanel.SetActive(false);
            _hubPanel.SetActive(false);
            _resultPanel.SetActive(false);
            if (_bookPanel != null) _bookPanel.SetActive(false);
            if (_bookGhost != null) _bookGhost.gameObject.SetActive(false);
            if (_statPopup != null) _statPopup.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_statsPanel != null) _statsPanel.SetActive(false);
            _dragSlider = -1;
            _bookDragging = false;
        }

        public void ShowTitle(string title, bool canContinue, string continueLabel)
        {
            HideAll();
            if (!string.IsNullOrEmpty(title)) _titleText.text = title;   // empty = keep the text from the prefab
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
            if (_statPopup != null) _statPopup.SetActive(false);
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
            _hubFighter = d;
            RefreshHubStatsButton(d);
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

        // ------------------------------------------------------------------ book
        /// <summary>
        /// Adds the Book icon (on the hub panel, dragged onto the table), its drag ghost and the Book screen
        /// (Back / Close / Next). Only creates what is missing, so it never touches parts you already have.
        /// Used by Tools > Kaiju > Game > Create Book Prefabs.
        /// </summary>
        public void AddBookUi()
        {
            if (_safe == null) return;

            // The first Book version had a plain button on the hub. The Book is now an icon you drag onto the table.
            if (_hubPanel != null)
            {
                var old = _hubPanel.transform.Find("BookButton");
                if (old != null) DestroyImmediate(old.gameObject);
            }

            if (_bookIcon == null && _hubPanel != null)
            {
                var icon = NewImage(_hubPanel.transform, "BookIcon", new Color(0.45f, 0.28f, 0.14f, 1f), true);
                Anchor(icon.rectTransform, 0.84f, 0.50f, new Vector2(220f, 260f));
                _bookIcon = icon.rectTransform;
                var label = NewText(icon.transform, "Label", "BOOK", 56f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Frac(label.rectTransform, 0f, 0.40f, 1f, 0.90f);
                var hint = NewText(icon.transform, "Hint", "drag onto\nthe table", 30f, SoftWhite, TextAlignmentOptions.Center);
                Frac(hint.rectTransform, 0f, 0.05f, 1f, 0.42f);
            }

            if (_bookGhost == null)
            {
                var ghost = NewImage(_safe, "BookDragGhost", new Color(0.45f, 0.28f, 0.14f, 0.85f), false);
                Anchor(ghost.rectTransform, 0.5f, 0.5f, new Vector2(180f, 220f));
                var gl = NewText(ghost.transform, "Label", "BOOK", 48f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Stretch(gl.rectTransform);
                _bookGhost = ghost.rectTransform;
                ghost.gameObject.SetActive(false);
            }

            if (_bookPanel == null)
            {
                _bookPanel = NewPanel("BookPanel", Color.clear, false);
                var rt = _bookPanel.transform;
                TMP_Text l;

                _bookLabel = NewText(rt, "BookPageLabel", "", 40f, SoftWhite, TextAlignmentOptions.Center);
                Anchor(_bookLabel.rectTransform, 0.5f, 0.16f, new Vector2(1000f, 70f));

                _bookPrevButton = NewButton(rt, "BookPrevButton", "< Back", 0.17f, 0.075f, new Vector2(300f, 120f), Grey, out l);
                _bookCloseButton = NewButton(rt, "BookCloseButton", "Close", 0.5f, 0.075f, new Vector2(300f, 120f), Blue, out l);
                _bookNextButton = NewButton(rt, "BookNextButton", "Next >", 0.83f, 0.075f, new Vector2(300f, 120f), Green, out l);
            }
        }

        // ------------------------------------------------------------------ duel button + stats screen (spend level-up points)
        public bool StatsOpen { get { return _statsPanel != null && _statsPanel.activeSelf; } }
        public bool ModalOpen { get { return SettingsOpen || StatsOpen; } }

        static readonly string[] StatBlurb =
        {
            "damage and health",
            "poise and shield choice",
            "XP and weapon training",
            "crits, dodge, speed and sight",
        };

        public bool HasDuelStatsUi()
        {
            if (_duelButton == null || _statsPanel == null || _statsCloseButton == null || _statsPlus == null || _statsPlus.Length != 4 ||
                _statsValue == null || _statsValue.Length != 4) return false;
            for (int i = 0; i < 4; i++) if (_statsPlus[i] == null || _statsValue[i] == null) return false;
            return _statsHubButton != null || _hubPanel == null;
        }

        /// <summary>
        /// Turns the hub's "Duel (coming soon)" button on, adds a Stats button to the hub and the Stats screen
        /// (four stats with a + each, for the points you earn from levelling up). Only creates what is missing.
        /// Used by Tools > Kaiju > Duel > Set Up Duel, and as a run-time fallback.
        /// </summary>
        public void AddDuelAndStatsUi()
        {
            if (_safe == null) return;
            TMP_Text l;

            if (_duelButton == null && _hubPanel != null)
            {
                var found = _hubPanel.transform.Find("DuelButton");
                if (found != null) _duelButton = found.GetComponent<Button>();
                if (_duelButton == null)
                    _duelButton = NewButton(_hubPanel.transform, "DuelButton", "Duel  (nobody dies)", 0.5f, 0.145f, new Vector2(760f, 110f), Blue, out l);
            }
            if (_duelButton != null)
            {
                _duelButton.interactable = true;
                var lab = _duelButton.GetComponentInChildren<TMP_Text>();
                if (lab != null && lab.text.Contains("coming soon")) lab.text = "Duel  (nobody dies)";
                var img = _duelButton.GetComponent<Image>();
                if (img != null && img.color == Grey) img.color = Blue;
            }

            if (_statsHubButton == null && _hubPanel != null)
            {
                _statsHubButton = NewButton(_hubPanel.transform, "StatsButton", "Stats", 0.16f, 0.37f, new Vector2(240f, 110f), Grey, out l);
                l.fontSize = 36f;
            }

            if (_statsPanel == null)
            {
                _statsPanel = NewPanel("StatsPanel", new Color(0.03f, 0.04f, 0.07f, 0.92f), true);
                var rt = _statsPanel.transform;

                _statsTitle = NewText(rt, "StatsTitle", "", 64f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Anchor(_statsTitle.rectTransform, 0.5f, 0.88f, new Vector2(1000f, 110f));
                _statsPointsText = NewText(rt, "StatsPoints", "", 46f, BarFill, TextAlignmentOptions.Center, FontStyles.Bold);
                Anchor(_statsPointsText.rectTransform, 0.5f, 0.80f, new Vector2(1000f, 80f));

                _statsValue = new TMP_Text[4];
                _statsPlus = new Button[4];
                for (int i = 0; i < 4; i++)
                {
                    float top = 0.71f - i * 0.135f;

                    var label = NewText(rt, "StatsLabel" + i, StatNames[i], 52f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
                    Frac(label.rectTransform, 0.07f, top - 0.07f, 0.55f, top);
                    var blurb = NewText(rt, "StatsBlurb" + i, StatBlurb[i], 30f, SoftWhite, TextAlignmentOptions.Left);
                    Frac(blurb.rectTransform, 0.07f, top - 0.115f, 0.62f, top - 0.07f);

                    _statsValue[i] = NewText(rt, "StatsValue" + i, "0", 60f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                    Frac(_statsValue[i].rectTransform, 0.55f, top - 0.115f, 0.74f, top);

                    _statsPlus[i] = NewButton(rt, "StatsPlus" + i, "+", 0.86f, top - 0.0575f, new Vector2(160f, 120f), Green, out l);
                    l.fontSize = 84f;
                }

                _statsCloseButton = NewButton(rt, "StatsCloseButton", "Done", 0.5f, 0.10f, new Vector2(720f, 130f), Blue, out l);
                _statsPanel.SetActive(false);
            }
        }

        void OpenStats()
        {
            if (_statsPanel == null || _hubFighter == null) return;
            RefreshStatsUi(_hubFighter);
            _statsPanel.transform.SetAsLastSibling();
            _statsPanel.SetActive(true);
        }

        void CloseStats()
        {
            if (_statsPanel == null) return;
            _statsPanel.SetActive(false);
            if (_hubFighter != null) RefreshHubStatsButton(_hubFighter);
        }

        void RaiseSpend(int stat)
        {
            if (Time.unscaledTime - _lastSpend < 0.25f) return;   // the same tap can arrive twice (EventSystem + direct)
            _lastSpend = Time.unscaledTime;
            if (StatPointSpent != null) StatPointSpent(stat);
        }

        /// <summary>Redraws the Stats screen (called by the GameController after a point is spent).</summary>
        public void RefreshStatsUi(FighterData d)
        {
            if (d == null) return;
            _hubFighter = d;
            int[] v = { d.might, d.ego, d.education, d.insight };
            if (_statsTitle != null) _statsTitle.text = d.displayName + "   Lv " + d.level;
            if (_statsPointsText != null)
            {
                _statsPointsText.text = d.statPoints > 0 ? "Points to spend:  " + d.statPoints : "No points to spend. Win fights to level up!";
                _statsPointsText.color = d.statPoints > 0 ? BarFill : SoftWhite;
            }
            for (int i = 0; i < 4; i++)
            {
                bool atCap = v[i] >= ProgressionRules.StatCap;
                if (_statsValue != null && _statsValue[i] != null) _statsValue[i].text = v[i] + (atCap ? "  max" : "");
                if (_statsPlus != null && _statsPlus[i] != null) _statsPlus[i].gameObject.SetActive(d.statPoints > 0 && !atCap);
            }
            RefreshHubStatsButton(d);
        }

        // The hub's Stats button shows how many points are waiting, and so does the info line.
        void RefreshHubStatsButton(FighterData d)
        {
            if (_hubInfo != null)
            {
                _hubInfo.text = FighterTiers.Label(d.tier) + "  -  Level " + d.level + "  (XP " + d.xp + " / " + ProgressionRules.XpToNext(d.level) + ")  -  " +
                                d.wins + " win" + (d.wins == 1 ? "" : "s") + (d.statPoints > 0 ? "  -  " + d.statPoints + " point" + (d.statPoints == 1 ? "" : "s") + " to spend" : "");
            }
            if (_statsHubButton != null)
            {
                var lab = _statsHubButton.GetComponentInChildren<TMP_Text>();
                if (lab != null) lab.text = d.statPoints > 0 ? "Stats (+" + d.statPoints + ")" : "Stats";
                var img = _statsHubButton.GetComponent<Image>();
                if (img != null) img.color = d.statPoints > 0 ? Green : Grey;
            }
        }

        // ------------------------------------------------------------------ settings (music / effects volume)
        public bool SettingsOpen { get { return _settingsPanel != null && _settingsPanel.activeSelf; } }

        public bool HasSettingsUi()
        {
            return _settingsPanel != null && _settingsCloseButton != null && _musicTrack != null && _musicFill != null &&
                   _musicHandle != null && _fxTrack != null && _fxFill != null && _fxHandle != null &&
                   (_titlePanel == null || _settingsTitleButton != null) && (_hubPanel == null || _settingsHubButton != null);
        }

        /// <summary>
        /// Adds the Settings screen (Music and Effects sliders + Close) and a Settings button on the title screen and on the hub.
        /// Only creates what is missing. Used by Tools > Kaiju > Audio > Set Up Audio, and as a run-time fallback.
        /// </summary>
        public void AddSettingsUi()
        {
            if (_safe == null) return;
            TMP_Text l;

            if (_settingsTitleButton == null && _titlePanel != null)
                _settingsTitleButton = NewButton(_titlePanel.transform, "SettingsButton", "Settings", 0.5f, 0.20f, new Vector2(720f, 120f), Grey, out l);

            if (_settingsHubButton == null && _hubPanel != null)
            {
                _settingsHubButton = NewButton(_hubPanel.transform, "SettingsButton", "Settings", 0.16f, 0.50f, new Vector2(240f, 110f), Grey, out l);
                l.fontSize = 36f;
            }

            if (_settingsPanel == null)
            {
                _settingsPanel = NewPanel("SettingsPanel", new Color(0.03f, 0.04f, 0.07f, 0.9f), true);
                var rt = _settingsPanel.transform;

                var title = NewText(rt, "SettingsTitle", "SETTINGS", 100f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Anchor(title.rectTransform, 0.5f, 0.80f, new Vector2(1000f, 150f));

                BuildSliderRow(rt, "Music", "Music", 0.62f, out _musicTrack, out _musicFill, out _musicHandle, out _musicValue);
                BuildSliderRow(rt, "Effects", "Effects", 0.45f, out _fxTrack, out _fxFill, out _fxHandle, out _fxValue);

                var hint = NewText(rt, "SettingsHint", "Drag the sliders", 34f, SoftWhite, TextAlignmentOptions.Center);
                Anchor(hint.rectTransform, 0.5f, 0.33f, new Vector2(1000f, 60f));

                _settingsCloseButton = NewButton(rt, "SettingsCloseButton", "Close", 0.5f, 0.20f, new Vector2(720f, 130f), Blue, out l);
                _settingsPanel.SetActive(false);
            }
        }

        // One labelled slider: "Music ............ 70%" with a tall invisible track (easy to hit with a thumb) holding a thin bar, a fill and a handle.
        void BuildSliderRow(Transform parent, string name, string label, float y, out RectTransform track, out RectTransform fill,
                            out RectTransform handle, out TMP_Text value)
        {
            var lab = NewText(parent, name + "Label", label, 56f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Frac(lab.rectTransform, 0.10f, y, 0.60f, y + 0.06f);

            value = NewText(parent, name + "Value", "70%", 52f, BarFill, TextAlignmentOptions.Right, FontStyles.Bold);
            Frac(value.rectTransform, 0.62f, y, 0.90f, y + 0.06f);

            var trackImg = NewImage(parent, name + "Slider", new Color(1f, 1f, 1f, 0f), true);   // the touch area
            Frac(trackImg.rectTransform, 0.10f, y - 0.075f, 0.90f, y - 0.005f);
            track = trackImg.rectTransform;

            var bar = NewImage(track, "Bar", BarBack, false);
            Frac(bar.rectTransform, 0f, 0.38f, 1f, 0.62f);

            var fillImg = NewImage(bar.transform, "Fill", BarFill, false);
            Frac(fillImg.rectTransform, 0f, 0f, 0.7f, 1f);
            fill = fillImg.rectTransform;

            var h = NewImage(track, "Handle", Color.white, false);
            h.rectTransform.anchorMin = new Vector2(0.7f, 0.5f);
            h.rectTransform.anchorMax = new Vector2(0.7f, 0.5f);
            h.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            h.rectTransform.anchoredPosition = Vector2.zero;
            h.rectTransform.sizeDelta = new Vector2(64f, 90f);
            handle = h.rectTransform;
        }

        void OpenSettings()
        {
            if (_settingsPanel == null) return;
            RefreshSettingsUi();
            _settingsPanel.transform.SetAsLastSibling();
            _settingsPanel.SetActive(true);
            _dragSlider = -1;
        }

        void CloseSettings()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.SetActive(false);
            _dragSlider = -1;
            GameAudio.SaveNow();
        }

        void RefreshSettingsUi()
        {
            SetSliderVisual(_musicFill, _musicHandle, _musicValue, GameAudio.MusicSlider);
            SetSliderVisual(_fxFill, _fxHandle, _fxValue, GameAudio.EffectsSlider);
        }

        static void SetSliderVisual(RectTransform fill, RectTransform handle, TMP_Text label, float v)
        {
            if (fill != null) fill.anchorMax = new Vector2(v, 1f);
            if (handle != null)
            {
                handle.anchorMin = new Vector2(v, 0.5f);
                handle.anchorMax = new Vector2(v, 0.5f);
                handle.anchoredPosition = Vector2.zero;
            }
            if (label != null) label.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        int _dragSlider = -1;   // 0 = music, 1 = effects, -1 = none

        void HandleSettingsSliders()
        {
            if (!SettingsOpen) { _dragSlider = -1; return; }
            Vector2 pos;

            if (_dragSlider < 0)
            {
                if (!PointerPressed(out pos)) return;
                if (_musicTrack != null && RectTransformUtility.RectangleContainsScreenPoint(_musicTrack, pos, null)) _dragSlider = 0;
                else if (_fxTrack != null && RectTransformUtility.RectangleContainsScreenPoint(_fxTrack, pos, null)) _dragSlider = 1;
                if (_dragSlider >= 0) SetSliderFromPointer(_dragSlider, pos);
                return;
            }

            bool released = PointerReleased(out pos);
            if (!released && !PointerHeld(out pos)) { EndSliderDrag(); return; }   // touch was cancelled
            SetSliderFromPointer(_dragSlider, pos);
            if (released) EndSliderDrag();
        }

        void SetSliderFromPointer(int which, Vector2 screenPos)
        {
            var track = which == 0 ? _musicTrack : _fxTrack;
            if (track == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screenPos, null, out local)) return;
            float v = Mathf.Clamp01(Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x));
            if (which == 0) GameAudio.SetMusicSlider(v); else GameAudio.SetEffectsSlider(v);
            RefreshSettingsUi();
        }

        void EndSliderDrag()
        {
            int was = _dragSlider;
            _dragSlider = -1;
            GameAudio.SaveNow();
            if (was == 1) GameAudio.PlayPreview();   // let go of Effects: play a sample so you can hear the level
        }

        // ------------------------------------------------------------------ stat info popup
        /// <summary>
        /// Adds invisible tap areas over the four stat rows of the roster card, a hint under the card and the popup itself.
        /// Only creates what is missing. Used by Tools > Kaiju > Game > Add Stat Info Popup, and as a run-time fallback.
        /// </summary>
        public void AddStatInfoUi()
        {
            if (_safe == null || _card == null) return;

            if (_statTap == null || _statTap.Length != 4) _statTap = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                if (_statTap[i] != null) continue;
                float top = 0.59f - i * 0.145f;
                float bottom = top - 0.125f;
                var tap = NewImage(_card.transform, "StatTap" + i, new Color(1f, 1f, 1f, 0f), true);
                Frac(tap.rectTransform, 0.03f, bottom, 0.97f, top);
                _statTap[i] = tap.rectTransform;
            }

            if (_card.transform.Find("StatHint") == null)
            {
                var hint = NewText(_card.transform, "StatHint", "Tap a stat to see what it does", 30f, SoftWhite, TextAlignmentOptions.Center);
                Frac(hint.rectTransform, 0.05f, -0.075f, 0.95f, 0f);
            }

            if (_statPopup == null)
            {
                var dim = NewImage(_safe, "StatPopup", new Color(0.02f, 0.03f, 0.05f, 0.72f), true);
                Stretch(dim.rectTransform);
                _statPopup = dim.gameObject;

                var box = NewImage(dim.transform, "Box", new Color(0.10f, 0.12f, 0.18f, 0.97f), false);
                Anchor(box.rectTransform, 0.5f, 0.5f, new Vector2(900f, 640f));

                _statPopupTitle = NewText(box.transform, "Title", "MIGHT", 68f, BarFill, TextAlignmentOptions.Center, FontStyles.Bold);
                Frac(_statPopupTitle.rectTransform, 0.05f, 0.82f, 0.95f, 0.97f);

                _statPopupBody = NewText(box.transform, "Body", "", 40f, Color.white, TextAlignmentOptions.TopLeft);
                _statPopupBody.enableAutoSizing = true;
                _statPopupBody.fontSizeMin = 24f;
                _statPopupBody.fontSizeMax = 40f;
                Frac(_statPopupBody.rectTransform, 0.07f, 0.14f, 0.93f, 0.80f);

                var close = NewText(box.transform, "CloseHint", "tap anywhere to close", 30f, SoftWhite, TextAlignmentOptions.Center);
                Frac(close.rectTransform, 0.05f, 0.02f, 0.95f, 0.12f);

                dim.gameObject.SetActive(false);
            }
        }

        bool HasStatInfoUi()
        {
            if (_statPopup == null || _statTap == null || _statTap.Length != 4) return false;
            for (int i = 0; i < 4; i++) if (_statTap[i] == null) return false;
            return true;
        }

        bool _popupConsumed;
        int _consumedFrame = -1;

        bool IsStatUiAt(Vector2 pos)
        {
            if (_consumedFrame == Time.frameCount) return true;
            if (_statPopup != null && _statPopup.activeSelf) return true;
            if (_card != null && _card.activeInHierarchy && _statTap != null)
                foreach (var r in _statTap)
                    if (r != null && RectTransformUtility.RectangleContainsScreenPoint(r, pos, null)) return true;
            return false;
        }

        // Touch / click a stat row: show what the stat does. Touch / click anywhere else while it is showing: close it.
        void HandleStatTaps()
        {
            _popupConsumed = false;
            Vector2 pos;
            if (!PointerPressed(out pos)) return;

            if (_statPopup != null && _statPopup.activeSelf)
            {
                _statPopup.SetActive(false);
                _popupConsumed = true;
                _consumedFrame = Time.frameCount;
                return;
            }

            if (_card == null || !_card.activeInHierarchy || _statTap == null) return;
            for (int i = 0; i < _statTap.Length; i++)
            {
                if (_statTap[i] == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(_statTap[i], pos, null))
                {
                    ShowStatInfo(i);
                    _popupConsumed = true;
                    _consumedFrame = Time.frameCount;
                    return;
                }
            }
        }

        void ShowStatInfo(int i)
        {
            if (_statPopup == null) return;
            string[] fallback = DefaultStatInfo();
            string text = (_statInfo != null && i < _statInfo.Length && !string.IsNullOrEmpty(_statInfo[i])) ? _statInfo[i] : fallback[i];
            if (_statPopupTitle != null) _statPopupTitle.text = StatNames[i];
            if (_statPopupBody != null) _statPopupBody.text = text;
            _statPopup.transform.SetAsLastSibling();
            _statPopup.SetActive(true);
        }

        // ------------------------------------------------------------------ dragging the Book icon onto the table
        // Done from raw touch/mouse input (like the buttons), so it works even if the EventSystem misses it.
        bool _bookDragging;
        Vector2 _bookDragStart;
        float _bookDragMoved;

        void HandleBookDrag()
        {
            if (_bookIcon == null || _hubPanel == null) return;
            Vector2 pos;

            if (!_bookDragging)
            {
                if (_hubPanel.activeInHierarchy && !ModalOpen && PointerPressed(out pos) &&
                    RectTransformUtility.RectangleContainsScreenPoint(_bookIcon, pos, null))
                {
                    _bookDragging = true;
                    _bookDragStart = pos;
                    _bookDragMoved = 0f;
                    ShowGhost(pos);
                }
                return;
            }

            bool released = PointerReleased(out pos);
            if (!released && !PointerHeld(out pos))
            {
                // the touch was cancelled
                _bookDragging = false;
                if (_bookGhost != null) _bookGhost.gameObject.SetActive(false);
                return;
            }

            _bookDragMoved = Mathf.Max(_bookDragMoved, (pos - _bookDragStart).magnitude);
            ShowGhost(pos);

            if (released)
            {
                _bookDragging = false;
                if (_bookGhost != null) _bookGhost.gameObject.SetActive(false);
                if (_bookDragMoved < 25f) Raise(BookTapped);        // a plain tap: put the book at the default spot
                else if (BookDropped != null) BookDropped(pos);
            }
        }

        void ShowGhost(Vector2 pos)
        {
            if (_bookGhost == null) return;
            _bookGhost.gameObject.SetActive(true);
            _bookGhost.position = new Vector3(pos.x, pos.y, 0f);   // overlay canvas: screen pixels
        }

        static bool PointerHeld(out Vector2 pos)
        {
            pos = default(Vector2);
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { pos = ts.primaryTouch.position.ReadValue(); return true; }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) { pos = mouse.position.ReadValue(); return true; }
            return false;
        }

        public void ShowBook(string pageLabel, bool canGoBack, bool canGoForward)
        {
            HideAll();
            if (_bookPanel == null) return;
            _bookPanel.SetActive(true);
            SetBookControls(pageLabel, canGoBack, canGoForward);
        }

        public void SetBookControls(string pageLabel, bool canGoBack, bool canGoForward)
        {
            if (_bookLabel != null) _bookLabel.text = pageLabel;
            if (_bookPrevButton != null) _bookPrevButton.interactable = canGoBack;
            if (_bookNextButton != null) _bookNextButton.interactable = canGoForward;
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
