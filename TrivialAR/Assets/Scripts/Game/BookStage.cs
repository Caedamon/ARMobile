using System;
using System.Collections;
using System.Collections.Generic;
using echo17.EndlessBook;
using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Weaponary;
using BookComponent = echo17.EndlessBook.EndlessBook;

namespace Game
{
    /// <summary>
    /// The AR Book on the table (a stage, like the roster and the tombstone).
    /// It draws each page prefab (Fighter, Index, Grave, Text, WeaponPicture, WeaponInfo, Blank) into a texture, hands the textures to the
    /// EndlessBook as page materials, then opens the book. Layout lives in the prefabs; size and facing are the
    /// fields below, so you can tune the book in the Inspector without touching code.
    /// </summary>
    public sealed class BookStage : MonoBehaviour
    {
        [Header("Parts (set by Tools > Kaiju > Game > Create Book Prefabs)")]
        [SerializeField] BookComponent book;
        [Tooltip("The book sits inside this object. Size, facing and position below are applied to it.")]
        [SerializeField] Transform pivot;
        [SerializeField] FighterPageView fighterPagePrefab;
        [SerializeField] GravePageView gravePagePrefab;
        [Tooltip("Shown for padding pages and spare space. Needs a Canvas; no script needed.")]
        [SerializeField] GameObject blankPagePrefab;
        [Tooltip("A copy of this material is made for every page. The EndlessBook 'PageFront' material works.")]
        [SerializeField] Material pageMaterialTemplate;

        [Header("Index, Stats and Armoury pages (set by Tools > Kaiju > Book > Set Up Index, Stats and Armoury)")]
        [SerializeField] IndexPageView indexPagePrefab;
        [SerializeField] TextPageView textPagePrefab;
        [SerializeField] WeaponPicturePageView weaponPicturePagePrefab;
        [SerializeField] WeaponInfoPageView weaponInfoPagePrefab;
        [Tooltip("Every weapon of the Armoury, in the order shown (two pages each: picture on the left, stats and description on the right).")]
        [SerializeField] List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        [Tooltip("The words on the Stats pages (up to 3 headings per page). Change them here; the numbers come from the combat balance, so keep them in step.")]
        [SerializeField] BookTextPage[] statsTexts = BookTexts.DefaultStats();
        [Tooltip("The Rules of the Arena: how a run works, then one page per kind of fight. The first section after the Index.")]
        [SerializeField] BookTextPage[] howToPlayTexts = BookTexts.DefaultHowToPlay();
        [Tooltip("Name of the how-to-play section: on its bookmark and in the Index.")]
        [SerializeField] string howToName = "Arena Rules";
        [Tooltip("Names shown in the Index (and on the tabs under the book, see GameUI > Book Tab Names). Memento Mori = remember that you will die.")]
        [SerializeField] string graveyardTitle = "Memento Mori";
        [SerializeField] string statsName = "Stats";
        [SerializeField] string armouryName = "Armoury";
        [SerializeField] string codexName = "Codex";

        [Header("Codex (archetype pages, set by Tools > Kaiju > Book > Set Up Codex)")]
        [Tooltip("The archetypes in the order they are shown: two pages each (portrait on the left, lore and modifiers on the right).")]
        [SerializeField] ArchetypeLibrary codexLibrary;
        [Tooltip("Where the models (prefabs) of the archetypes come from. The same catalog the fights use.")]
        [SerializeField] FighterCatalog codexCatalog;
        [Tooltip("Height of a portrait picture in pixels (it is three quarters as wide).")]
        [SerializeField] int codexPortraitSize = 512;
        [Tooltip("How much of the model's height the portrait shows. 1 = the whole body, 0.3 = head and shoulders.")]
        [SerializeField, Range(0.2f, 1f)] float codexFrameHeight = 1f;
        [Tooltip("The pose the models stand in for their portrait (an idle clip), so they do not stand in a T-pose. Set by Set Up Codex from the Animation Pack. Empty = the pose the prefab was saved in.")]
        [SerializeField] AnimationClip codexPose;
        [Tooltip("How far into the pose clip (seconds).")]
        [SerializeField] float codexPoseTime = 0f;
        [Tooltip("Codex pages are drawn when you get near them, not all at once. This many pages ahead of the open spread are kept ready...")]
        [SerializeField, Range(2, 16)] int codexPagesAhead = 6;
        [Tooltip("...and this many behind it. Pages further away are thrown away again to save memory.")]
        [SerializeField, Range(0, 16)] int codexPagesBehind = 4;

        [Header("Weapon pictures")]
        [Tooltip("A material with an Unlit shader (URP/Unlit). The weapon pictures are drawn flat with it. Made by the setup tool; keeping it here also makes sure the shader is in the build.")]
        [SerializeField] Material weaponFlatMaterial;
        [Tooltip("Size of the weapon picture in pixels. 512 is plenty.")]
        [SerializeField] int weaponPictureSize = 512;
        [Tooltip("The page picture for the weapon (left) page can be smaller than the text pages to save memory.")]
        [SerializeField] int weaponPageTextureSize = 512;
        [Tooltip("How far the weapon leans on the page (degrees). 0 = standing straight up.")]
        [SerializeField, Range(0f, 60f)] float weaponTilt = 20f;
        [Tooltip("Shield models are built with the inside facing the camera; this turns them around so the OUTSIDE shows. Untick if a shield shows the wrong side.")]
        [SerializeField] bool showShieldOutside = true;
        [Tooltip("Weapons whose picture comes out upside down. Type the weapon's name exactly as in its Weapon Definition.")]
        [SerializeField] List<string> upsideDownWeapons = new List<string>();

        [Header("Size and facing (tune these)")]
        [Tooltip("Length of the longest side of the CLOSED book on the table, in metres. The open book is about 1.5x wider.")]
        [SerializeField] float bookSizeMeters = 0.60f;
        [Tooltip("Turns the book around the vertical axis. Change this if the book opens facing away from you (try 180).")]
        [SerializeField] float facingYaw = 0f;
        [Tooltip("Tilts the top of the book toward you (degrees) so the pages are easier to read. 0 = flat on the table.")]
        [SerializeField] float tiltTowardViewer = 12f;
        [Tooltip("Position on the table (x sideways, y height, z toward/away from you). Stage space: +Z points away from you.")]
        [SerializeField] Vector3 bookOffset = new Vector3(0f, 0.01f, -0.02f);
        [Tooltip("Ignore the measuring above and use this scale as it is.")]
        [SerializeField] bool useManualScale = false;
        [SerializeField] float manualScale = 0.3f;

        [Header("Page pictures")]
        [Tooltip("EndlessBook page pictures are SQUARE. Bigger = sharper but more memory (768 = about 3 MB per page).")]
        [SerializeField] int pageTextureSize = 768;
        [Tooltip("EndlessBook only shows the left 80.5% of its square page picture (its page template is 824 of 1024 pixels wide). Your page prefab is drawn into that part. Leave this alone unless you change the book model.")]
        [SerializeField, Range(0.5f, 1f)] float pageContentWidth = 0.805f;
        [SerializeField] Color pageBackground = new Color(0.93f, 0.87f, 0.72f, 1f);
        [Tooltip("Pages are lit by the scene. Self-lit pages stay readable in any room light.")]
        [SerializeField] bool selfLit = true;
        [SerializeField] float emissionStrength = 0.7f;
        [Tooltip("If the page pictures appear upside down or mirrored on the book, tick these.")]
        [SerializeField] bool flipPagesVertically = false;
        [SerializeField] bool flipPagesHorizontally = false;
        [Tooltip("Frames to wait after filling a page before drawing it (lets the text settle). 1 is safe.")]
        [SerializeField] int settleFrames = 1;
        [Tooltip("The graveyard shows the most recent fallen fighters, TWO pages each (so 12 = the last 6 heroes). Each page picture costs about 3 MB of memory.")]
        [SerializeField] int maxFallenPages = 12;

        [Header("Timing")]
        [SerializeField] float openSeconds = 1.2f;
        [SerializeField] float closeSeconds = 1.0f;
        [SerializeField] float turnSeconds = 0.6f;

        [Header("Sounds (assigned by Create Book Prefabs; swap them freely)")]
        [Tooltip("Plays the book sounds. Added automatically if missing. Set Spatial Blend to 1 for sound that comes from the book.")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip closeSound;
        [Tooltip("Played once for every page turn.")]
        [SerializeField] AudioClip pageTurnSound;
        [SerializeField, Range(0f, 1f)] float soundVolume = 1f;
        [Tooltip("The page turn clip is much quieter than the open/close clips. 1 is the loudest it can go here.")]
        [SerializeField, Range(0f, 1f)] float pageTurnVolume = 1f;
        [Tooltip("Played when the book turns MANY pages at once (a tab, a jump). Set by Tools > Kaiju > Book > Set Up Book Flip Sound ('PagesFlipping').")]
        [SerializeField] AudioClip pagesFlippingSound;
        [SerializeField, Range(0f, 1f)] float pagesFlippingVolume = 1f;
        [Tooltip("A jump of at least this many pages plays the flipping sound instead of the single page turn.")]
        [SerializeField] int manyPages = 4;
        [Tooltip("Where in the flipping clip to start (seconds). The clip has one sharp click at 1.0 s; set this to 1.1 to start after it.")]
        [SerializeField] float pagesFlippingStart = 0f;

        [Header("Sound timing (seconds)")]
        [Tooltip("Relative to the moment the opening book touches the table (the end of the open animation). The thud in the clip lands 0.06 s after it starts, so -0.06 puts the thud exactly on the landing. More negative = earlier, positive = later.")]
        [SerializeField] float openSoundOffset = -0.06f;
        [Tooltip("Relative to the moment the closing book touches the table (the end of the close animation). Same idea as above.")]
        [SerializeField] float closeSoundOffset = -0.06f;
        [Tooltip("Delay after a page starts turning. The swoosh in the clip builds up for about half a second, so 0 lands it late in the turn.")]
        [SerializeField] float pageTurnSoundDelay = 0f;

        [Header("Side tabs (bookmarks)")]
        [Tooltip("Bookmarks on the edge of the open book, one under the other: Index, Arena Rules, Stats, Armoury, Codex, Memento Mori. Each belongs to the first page of its section: on the left edge once you have turned to or past that page, on the right edge before. They turn over with the page. Tap one to go there. Untick to use the buttons under the book instead.")]
        [SerializeField] bool worldTabs = true;
        [Tooltip("Tab colours, in book order: Index, Arena Rules, Stats, Armoury, Codex, Memento Mori. Fewer than six colours: the defaults are used.")]
        [SerializeField] Color[] tabColors = DefaultTabColors();

        const int SectionCount = 6;
        static Color[] DefaultTabColors()
        {
            return new[]
            {
                new Color(0.55f, 0.16f, 0.16f, 1f), new Color(0.78f, 0.52f, 0.12f, 1f), new Color(0.16f, 0.36f, 0.62f, 1f),
                new Color(0.20f, 0.50f, 0.26f, 1f), new Color(0.10f, 0.45f, 0.48f, 1f), new Color(0.36f, 0.14f, 0.46f, 1f)
            };
        }
        [Tooltip("How far a tab sticks out, as a fraction of the open book's width.")]
        [SerializeField] float tabLength = 0.16f;
        [Tooltip("Height of a tab along the book's edge, as a fraction of the page height.")]
        [SerializeField] float tabHeight = 0.075f;
        [Tooltip("Gap between tabs, as a fraction of the page height.")]
        [SerializeField] float tabGap = 0.02f;
        [Tooltip("Gap above the first tab, as a fraction of the page height.")]
        [SerializeField] float tabTopMargin = 0.06f;
        [Tooltip("Where on the book's thickness the tabs sit: 0 = bottom of the book, 1 = top.")]
        [SerializeField, Range(0f, 1f)] float tabLevel = 0.5f;
        [Tooltip("Tabs start at the top edge of the page and go down. Untick if they start at the bottom.")]
        [SerializeField] bool tabsStartAtTop = true;
        [Tooltip("How far the tab of the section you are in slides out (fraction of tab length).")]
        [SerializeField] float tabActiveSlide = 0.2f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        public event Action Opened;
        public event Action PageChanged;
        public event Action Failed;

        public bool IsOpen { get; private set; }
        public bool IsBusy { get { return _closing || _jumping || (book != null && (book.IsTurningPages || book.CurrentState != BookComponent.StateEnum.OpenMiddle)); } }
        public bool CanTurnBack { get { return IsOpen && !IsBusy && !book.IsFirstPageGroup; } }
        public bool CanTurnForward { get { return IsOpen && !IsBusy && !book.IsLastPageGroup; } }

        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly List<Material> _materials = new List<Material>();
        Camera _cam;
        RenderTexture _rt;
        RectTransform _sheet;       // the square canvas the page prefabs are drawn onto
        bool _closing;
        int _pageCount;

        // ------------------------------------------------------------------ public API
        public string PageLabel
        {
            get
            {
                if (!IsOpen || book == null) return "";
                return "Pages " + book.CurrentLeftPageNumber + " - " + book.CurrentRightPageNumber + "   of   " + _pageCount;
            }
        }

        /// <summary>Draws the pages and opens the book. fallen is oldest first (as saved); the book shows the newest first.</summary>
        public void Open(FighterData current, List<FallenFighter> fallen)
        {
            StartCoroutine(OpenRoutine(current, fallen));
        }

        public void TurnForward()
        {
            if (!CanTurnForward) return;
            PlayAt(pageTurnSound, pageTurnSoundDelay, pageTurnVolume);
            _turnDur = turnSeconds;
            book.TurnForward(turnSeconds, onCompleted: (a, b, c) => Raise(PageChanged), onPageTurnStart: OnSheetTurn);
            Raise(PageChanged);
        }

        public void TurnBackward()
        {
            if (!CanTurnBack) return;
            PlayAt(pageTurnSound, pageTurnSoundDelay, pageTurnVolume);
            _turnDur = turnSeconds;
            book.TurnBackward(turnSeconds, onCompleted: (a, b, c) => Raise(PageChanged), onPageTurnStart: OnSheetTurn);
            Raise(PageChanged);
        }

        public enum Section { Index, HowToPlay, Stats, Armoury, Codex, Graveyard }

        readonly int[] _sectionPage = new int[SectionCount];

        /// <summary>Turns to the first page of a section (the tabs under the book).</summary>
        public void JumpTo(Section section)
        {
            if (!IsOpen || IsBusy) return;
            int page = _sectionPage[(int)section];
            if (page < 1) return;

            // Codex pages are drawn on demand: have the pages we land on ready before the book starts turning.
            if (IsCodexPage(page) && !(_codexTex.ContainsKey(page) && _codexTex.ContainsKey(page + 1)))
            {
                StartCoroutine(JumpAfterCodexRoutine(section, page));
                return;
            }

            int away = Mathf.Abs(page - book.CurrentLeftPageNumber);
            if (away < 2) { book.SetPageNumber(page); Raise(PageChanged); return; }   // already looking at it

            float seconds = Mathf.Clamp(away * 0.05f, 0.6f, 1.6f);
            if (pagesFlippingSound != null && away >= manyPages) PlayFlipping(seconds);   // many pages at once: the 'flipping' sound
            else PlayAt(pageTurnSound, pageTurnSoundDelay, pageTurnVolume);
            int sheets = Mathf.Max(1, away / 2);
            _turnDur = seconds / (((sheets - 1f) / Mathf.Max(1, book.MaxPagesTurningCount)) + 1f);   // how long ONE sheet takes (same formula as the book)
            book.TurnToPage(page, BookComponent.PageTurnTimeTypeEnum.TotalTurnTime, seconds, onCompleted: (a, b, c) => Raise(PageChanged), onPageTurnStart: OnSheetTurn);
            Raise(PageChanged);
        }

        /// <summary>Closes the book, then calls done (also if the animation never reports back).</summary>
        public void Close(Action done)
        {
            if (_closing) return;
            _closing = true;
            StartCoroutine(CloseRoutine(done));
        }

        // ------------------------------------------------------------------ opening
        IEnumerator OpenRoutine(FighterData current, List<FallenFighter> fallen)
        {
            if (book == null)
            {
                Debug.LogError("[Book] BookStage has no EndlessBook assigned. Run Tools > Kaiju > Game > Create Book Prefabs.", this);
                Raise(Failed);
                yield break;
            }

            ApplyPlacement();
            yield return null;          // let the book's standins load before measuring
            FitSize();

            // 1. what goes on each page
            var specs = BuildSpecs(current, fallen);
            _specs = specs;
            _pageCount = specs.Count;
            MeasureWeapons();
            if (debugLogs) Debug.Log("[Book] " + _pageCount + " pages (" + (fallen != null ? fallen.Count : 0) + " fallen)");

            // 2. draw them
            MakeCamera();
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].kind == Kind.CodexLeft || specs[i].kind == Kind.CodexRight)
                {
                    _materials.Add(CodexPlaceholder());   // drawn later, when the book gets near
                    continue;
                }
                yield return DrawPage(specs[i], i + 1);
                if (_materials.Count != i + 1)
                {
                    Debug.LogError("[Book] Could not draw page " + (i + 1) + ".", this);
                    Raise(Failed);
                    yield break;
                }
            }
            DisposeCamera();

            // 3. hand them to the book (the book prefab starts with 2 pages)
            for (int i = 0; i < _materials.Count; i++)
            {
                var data = new PageData { material = _materials[i] };
                if (i + 1 <= book.LastPageNumber) book.SetPageData(i + 1, data);
                else book.AddPageData(_materials[i]);
            }
            book.SetPageNumber(1);

            // 4. open
            bool done = false;
            PlayAt(openSound, openSeconds + openSoundOffset, 1f);   // the thud lands when the book touches the table
            book.SetState(BookComponent.StateEnum.OpenMiddle, openSeconds, (a, b, c) => done = true);
            float timeout = Time.unscaledTime + openSeconds + 2f;
            while (!done && Time.unscaledTime < timeout) yield return null;

            IsOpen = true;
            BuildTabs();
            if (debugLogs) Debug.Log("[Book] Open. state=" + book.CurrentState + " page=" + book.CurrentPageNumber);
            Raise(Opened);
            Raise(PageChanged);
        }

        enum Kind { Fighter, Grave, Empty, Blank, Index, Text, WeaponPicture, WeaponInfo, GraveLeft, GraveRight, CodexLeft, CodexRight }

        struct Spec
        {
            public Kind kind;
            public FighterData fighter;
            public FallenFighter fallen;
            public BookTextPage text;
            public WeaponDefinition weapon;
            public ArchetypeDefinition archetype;
        }

        // Page 1 (left): your fighter.  Page 2 (right): the Index.  Then: Arena Rules (how to play), Stats, Armoury, Memento Mori (the graveyard, last, because it only grows).
        // Every section starts on a left page, so each one opens as a fresh spread.
        List<Spec> BuildSpecs(FighterData current, List<FallenFighter> fallen)
        {
            var list = new List<Spec>();
            list.Add(current != null ? new Spec { kind = Kind.Fighter, fighter = current } : new Spec { kind = Kind.Blank });
            list.Add(new Spec { kind = Kind.Index });
            _sectionPage[(int)Section.Index] = 1;

            _sectionPage[(int)Section.HowToPlay] = list.Count + 1;
            if (howToPlayTexts != null)
                foreach (var p in howToPlayTexts)
                    if (p != null) list.Add(new Spec { kind = Kind.Text, text = p });
            if (list.Count + 1 == _sectionPage[(int)Section.HowToPlay]) list.Add(new Spec { kind = Kind.Blank });
            Pad(list);

            _sectionPage[(int)Section.Stats] = list.Count + 1;
            if (statsTexts != null)
                foreach (var p in statsTexts)
                    if (p != null) list.Add(new Spec { kind = Kind.Text, text = p });
            Pad(list);

            _sectionPage[(int)Section.Armoury] = list.Count + 1;
            int weaponCount = 0;
            foreach (var w in weapons)
            {
                if (w == null) continue;
                list.Add(new Spec { kind = Kind.WeaponPicture, weapon = w });
                list.Add(new Spec { kind = Kind.WeaponInfo, weapon = w });
                weaponCount++;
            }
            if (weaponCount == 0) list.Add(new Spec { kind = Kind.Blank });
            Pad(list);

            // Codex: two pages for every archetype. They are NOT drawn when the book opens (see the Codex pages part below).
            _sectionPage[(int)Section.Codex] = list.Count + 1;
            int codexEntries = 0;
            if (codexLibrary != null && codexLibrary.entries != null)
                foreach (var a in codexLibrary.entries)
                {
                    if (a == null) continue;
                    list.Add(new Spec { kind = Kind.CodexLeft, archetype = a });
                    list.Add(new Spec { kind = Kind.CodexRight, archetype = a });
                    codexEntries++;
                }
            if (codexEntries == 0) list.Add(new Spec { kind = Kind.Blank });
            Pad(list);
            _codexFirst = codexEntries > 0 ? _sectionPage[(int)Section.Codex] : 0;
            _codexLast = codexEntries > 0 ? list.Count : 0;

            _sectionPage[(int)Section.Graveyard] = list.Count + 1;
            int shown = 0;
            if (fallen != null)
            {
                // two pages for every hero: the memorial on the left, the legacy on the right
                int maxHeroes = Mathf.Max(1, maxFallenPages / 2);
                for (int i = fallen.Count - 1; i >= 0 && shown < maxHeroes; i--, shown++)
                {
                    list.Add(new Spec { kind = Kind.GraveLeft, fallen = fallen[i] });
                    list.Add(new Spec { kind = Kind.GraveRight, fallen = fallen[i] });
                }
            }
            if (shown == 0) list.Add(new Spec { kind = Kind.Empty });
            Pad(list);
            return list;
        }

        static void Pad(List<Spec> list)
        {
            if (list.Count % 2 == 1) list.Add(new Spec { kind = Kind.Blank });   // the book needs pairs of pages
        }

        // The biggest damage / reach / speed in the Armoury: the bars on the weapon pages are shown against these.
        float _maxDamage = 1f, _maxReach = 1f, _maxSpeed = 1f;

        void MeasureWeapons()
        {
            _maxDamage = _maxReach = _maxSpeed = 1f;
            foreach (var w in weapons)
            {
                if (w == null) continue;
                float d, r, s;
                WeaponInfoPageView.Measure(w, out d, out r, out s);
                _maxDamage = Mathf.Max(_maxDamage, d);
                _maxReach = Mathf.Max(_maxReach, r);
                _maxSpeed = Mathf.Max(_maxSpeed, s);
            }
        }

        bool IsUpsideDown(WeaponDefinition w)
        {
            return w != null && upsideDownWeapons != null && upsideDownWeapons.Contains(w.displayName);
        }

        // ------------------------------------------------------------------ drawing a page to a texture
        void MakeCamera()
        {
            var go = new GameObject("PageCamera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -200f, 0f);   // far below the table, sees nothing but the page
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 1f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 5f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = pageBackground;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.enabled = false;           // we call Render() ourselves

            EnsureRenderTexture(pageTextureSize);

            // A square canvas filled with the page colour. Each page prefab is placed in its left pageContentWidth part.
            var sheetGo = new GameObject("PageSheet", typeof(RectTransform));
            sheetGo.transform.SetParent(transform, false);
            var canvas = sheetGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;
            var scaler = sheetGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 1024f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sheetGo.transform, false);
            var bg = bgGo.AddComponent<Image>();
            bg.color = pageBackground;
            bg.raycastTarget = false;
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            _sheet = (RectTransform)sheetGo.transform;
        }

        // One render texture at a time; a new one is made when a page needs a different size.
        void EnsureRenderTexture(int size)
        {
            if (_rt != null && _rt.width == size) return;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _rt.useMipMap = false;
            _rt.Create();
        }

        void DisposeCamera()
        {
            if (_cam != null) Destroy(_cam.gameObject);
            if (_sheet != null) Destroy(_sheet.gameObject);
            _sheet = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _cam = null;
            _rt = null;
        }

        IEnumerator DrawPage(Spec spec, int pageNumber)
        {
            GameObject prefab = null;
            switch (spec.kind)
            {
                case Kind.Fighter: prefab = fighterPagePrefab != null ? fighterPagePrefab.gameObject : blankPagePrefab; break;
                case Kind.Grave:
                case Kind.Empty:   prefab = gravePagePrefab != null ? gravePagePrefab.gameObject : blankPagePrefab; break;
                case Kind.Index:         prefab = indexPagePrefab != null ? indexPagePrefab.gameObject : blankPagePrefab; break;
                case Kind.Text:          prefab = textPagePrefab != null ? textPagePrefab.gameObject : blankPagePrefab; break;
                case Kind.WeaponPicture: prefab = weaponPicturePagePrefab != null ? weaponPicturePagePrefab.gameObject : blankPagePrefab; break;
                case Kind.WeaponInfo:    prefab = weaponInfoPagePrefab != null ? weaponInfoPagePrefab.gameObject : blankPagePrefab; break;
                default:           prefab = blankPagePrefab; break;
            }

            // The weapon (left) pages can be a smaller picture than the text pages.
            int size = pageTextureSize;
            if (spec.kind == Kind.WeaponPicture && weaponPageTextureSize > 0) size = Mathf.Min(pageTextureSize, weaponPageTextureSize);
            EnsureRenderTexture(size);
            Texture2D weaponPicture = null;

            GameObject go = null;
            bool graveSpread = (spec.kind == Kind.GraveLeft || spec.kind == Kind.GraveRight) && spec.fallen != null;
            if (prefab != null || graveSpread)
            {
                // the two grave pages of a hero are built in code (no prefab); everything else is a prefab
                go = graveSpread ? GraveSpreadView.Create(_sheet, spec.kind == Kind.GraveRight, spec.fallen) : Instantiate(prefab, _sheet);
                go.name = "PageDraw_" + pageNumber;

                // The page prefab has its own Canvas + scaler so you can edit it on its own; here it is just a panel
                // inside our sheet, so drop those two and put it in the part of the square that the book shows.
                var oldRaycaster = go.GetComponent<GraphicRaycaster>();
                if (oldRaycaster != null) DestroyImmediate(oldRaycaster);
                var oldScaler = go.GetComponent<CanvasScaler>();
                if (oldScaler != null) DestroyImmediate(oldScaler);
                var oldCanvas = go.GetComponent<Canvas>();
                if (oldCanvas != null) DestroyImmediate(oldCanvas);
                var pageRt = go.GetComponent<RectTransform>();
                if (pageRt == null) pageRt = go.AddComponent<RectTransform>();
                pageRt.anchorMin = Vector2.zero;
                pageRt.anchorMax = new Vector2(pageContentWidth, 1f);
                pageRt.offsetMin = Vector2.zero;
                pageRt.offsetMax = Vector2.zero;
                pageRt.localScale = Vector3.one;

                var fp = go.GetComponent<FighterPageView>();
                if (fp != null && spec.fighter != null) fp.Fill(spec.fighter);
                var gp = go.GetComponent<GravePageView>();
                if (gp != null)
                {
                    if (spec.kind == Kind.Grave && spec.fallen != null) gp.Fill(spec.fallen);
                    else gp.FillEmpty();
                }

                var ip = go.GetComponent<IndexPageView>();
                if (ip != null)
                {
                    var names = new List<string> { howToName, statsName, armouryName, codexName, graveyardTitle };
                    var pages = new List<int> { _sectionPage[(int)Section.HowToPlay], _sectionPage[(int)Section.Stats], _sectionPage[(int)Section.Armoury], _sectionPage[(int)Section.Codex], _sectionPage[(int)Section.Graveyard] };
                    ip.Fill(names, pages);
                }
                var tp = go.GetComponent<TextPageView>();
                if (tp != null && spec.text != null) tp.Fill(spec.text);
                var wp = go.GetComponent<WeaponPicturePageView>();
                if (wp != null)
                {
                    string problem;
                    weaponPicture = WeaponSnapshot.Take(transform, spec.weapon, weaponFlatMaterial, Mathf.Max(64, weaponPictureSize), weaponTilt, IsUpsideDown(spec.weapon), showShieldOutside, out problem);
                    if (weaponPicture == null && spec.weapon != null)
                        Debug.LogWarning("[Book] No picture for '" + spec.weapon.displayName + "': " + problem, this);
                    wp.Fill(spec.weapon, weaponPicture);
                }
                var wi = go.GetComponent<WeaponInfoPageView>();
                if (wi != null && spec.weapon != null) wi.Fill(spec.weapon, _maxDamage, _maxReach, _maxSpeed);

                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
                for (int i = 0; i < Mathf.Max(1, settleFrames); i++) yield return null;
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
            }

            _cam.targetTexture = _rt;
            _cam.Render();

            // Copy into a normal texture so the page survives the phone losing its graphics memory (app switch).
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(true, true);          // build mipmaps, free the CPU copy
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            _textures.Add(tex);

            if (go != null) Destroy(go);
            if (weaponPicture != null) Destroy(weaponPicture);   // it is baked into the page now

            _materials.Add(MakePageMaterial(tex, pageNumber));
        }

        Material MakePageMaterial(Texture2D tex, int pageNumber)
        {
            Material m;
            if (pageMaterialTemplate != null) m = new Material(pageMaterialTemplate);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(shader != null ? shader : Shader.Find("Standard"));
            }
            m.name = "BookPage_" + pageNumber;

            Vector2 scale = new Vector2(flipPagesHorizontally ? -1f : 1f, flipPagesVertically ? -1f : 1f);
            Vector2 offset = new Vector2(flipPagesHorizontally ? 1f : 0f, flipPagesVertically ? 1f : 0f);

            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", scale);
                m.SetTextureOffset("_MainTex", offset);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);

            if (selfLit && m.HasProperty("_EmissionMap"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * emissionStrength);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        // ------------------------------------------------------------------ Codex pages (drawn on demand)
        // Every other page is drawn when the book opens and kept. The Codex has two pages for every archetype, so those are drawn only
        // when the book gets near them: a window of pages around the open spread is kept ready, pages further away are thrown away,
        // and the rest show a plain page (the placeholder) for the moment they flip by.
        List<Spec> _specs;
        int _codexFirst, _codexLast;                       // page numbers (1-based) of the first and last Codex page; 0 = no Codex
        Material _codexPlaceholder;
        readonly Dictionary<int, Texture2D> _codexTex = new Dictionary<int, Texture2D>();
        readonly Dictionary<int, Material> _codexMat = new Dictionary<int, Material>();
        bool _codexRendering, _jumping;
        float _codexNextCheck;

        bool IsCodexPage(int page)
        {
            return _codexLast > 0 && page >= _codexFirst && page <= _codexLast;
        }

        Material CodexPlaceholder()
        {
            if (_codexPlaceholder != null) return _codexPlaceholder;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = pageBackground;
            tex.SetPixels(px);
            tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            _textures.Add(tex);
            _codexPlaceholder = MakePageMaterial(tex, 0);
            _codexPlaceholder.name = "BookPage_CodexPlaceholder";
            return _codexPlaceholder;
        }

        // Called every frame (cheap: it only looks every fifth of a second).
        void CodexTick()
        {
            if (_codexLast <= 0 || !IsOpen || _closing || _codexRendering || _jumping || book == null) return;
            if (Time.unscaledTime < _codexNextCheck) return;
            _codexNextCheck = Time.unscaledTime + 0.2f;

            int left = book.CurrentLeftPageNumber;
            int lo = left - codexPagesBehind, hi = left + 1 + codexPagesAhead;

            var want = new List<int>();
            for (int p = Mathf.Max(_codexFirst, lo); p <= Mathf.Min(_codexLast, hi); p++)
                if (!_codexTex.ContainsKey(p)) want.Add(p);
            if (want.Count > 0)
            {
                want.Sort((x, y) => Mathf.Abs(x - left).CompareTo(Mathf.Abs(y - left)));   // the nearest first
                StartCoroutine(RenderCodexPages(want));
                return;
            }

            if (IsBusy) return;   // never throw a page away while the book is turning
            List<int> drop = null;
            foreach (var kv in _codexTex)
                if (kv.Key < lo - 2 || kv.Key > hi + 2) { if (drop == null) drop = new List<int>(); drop.Add(kv.Key); }
            if (drop != null) foreach (int p in drop) ReleaseCodexPage(p);
        }

        void ReleaseCodexPage(int p)
        {
            if (book != null && p <= book.LastPageNumber)
                book.SetPageData(p, new PageData { material = CodexPlaceholder() });
            Material m; Texture2D t;
            if (_codexMat.TryGetValue(p, out m) && m != null) Destroy(m);
            if (_codexTex.TryGetValue(p, out t) && t != null) Destroy(t);
            _codexMat.Remove(p);
            _codexTex.Remove(p);
            if (debugLogs) Debug.Log("[Book] Codex page " + p + " released (" + _codexTex.Count + " kept)");
        }

        IEnumerator JumpAfterCodexRoutine(Section section, int page)
        {
            _jumping = true;
            while (_codexRendering) yield return null;
            var need = new List<int>();
            if (!_codexTex.ContainsKey(page)) need.Add(page);
            if (!_codexTex.ContainsKey(page + 1)) need.Add(page + 1);
            if (need.Count > 0) yield return RenderCodexPages(need);
            _jumping = false;
            JumpTo(section);
        }

        IEnumerator RenderCodexPages(List<int> pages)
        {
            _codexRendering = true;
            MakeCamera();
            foreach (int p in pages)
            {
                if (_closing || _specs == null) break;
                if (_codexTex.ContainsKey(p)) continue;
                yield return DrawCodexPage(p);
            }
            DisposeCamera();
            _codexRendering = false;
        }

        IEnumerator DrawCodexPage(int pageNumber)
        {
            if (_specs == null || pageNumber < 1 || pageNumber > _specs.Count) yield break;
            var spec = _specs[pageNumber - 1];
            if (spec.archetype == null) yield break;
            bool right = spec.kind == Kind.CodexRight;

            EnsureRenderTexture(pageTextureSize);
            Texture2D portrait = right ? null : TakePortrait(spec.archetype);

            var go = CodexSpreadView.Create(_sheet, right, spec.archetype, portrait, SeenAs(spec.archetype), pageContentWidth);
            go.name = "CodexDraw_" + pageNumber;
            var pageRt = (RectTransform)go.transform;
            pageRt.anchorMin = Vector2.zero;
            pageRt.anchorMax = new Vector2(pageContentWidth, 1f);
            pageRt.offsetMin = Vector2.zero;
            pageRt.offsetMax = Vector2.zero;
            pageRt.localScale = Vector3.one;

            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            for (int i = 0; i < Mathf.Max(1, settleFrames); i++) yield return null;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            _cam.targetTexture = _rt;
            _cam.Render();

            int size = pageTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(true, true);
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;

            Destroy(go);
            if (portrait != null) Destroy(portrait);   // it is baked into the page now

            var mat = MakePageMaterial(tex, pageNumber);
            _codexTex[pageNumber] = tex;
            _codexMat[pageNumber] = mat;
            if (book != null && pageNumber <= book.LastPageNumber) book.SetPageData(pageNumber, new PageData { material = mat });
            if (debugLogs) Debug.Log("[Book] Codex page " + pageNumber + " drawn (" + _codexTex.Count + " kept)");
        }

        // The portrait of an archetype: the first of its models that has a prefab (the hero version first, else the opponent one).
        Texture2D TakePortrait(ArchetypeDefinition a)
        {
            GameObject prefab = null;
            if (codexCatalog != null && a.modelIds != null)
                foreach (var id in a.modelIds)
                {
                    var model = codexCatalog.Find(id);
                    if (model == null) continue;
                    prefab = model.playerPrefab != null ? model.playerPrefab : model.enemyPrefab;
                    if (prefab != null) break;
                }
            if (prefab == null) { Debug.LogWarning("[Book] No model found for the archetype '" + a.displayName + "' (check its Model Ids and the Codex Catalog on the book).", this); return null; }

            string problem;
            int ph = Mathf.Max(64, codexPortraitSize);
            int pw = Mathf.Max(48, Mathf.RoundToInt(ph * CodexSpreadView.PortraitAspect));
            var tex = FighterBust.Take(transform, prefab, weaponFlatMaterial, pw, ph, codexFrameHeight, codexPose, codexPoseTime, out problem);
            if (tex == null) Debug.LogWarning("[Book] No portrait for '" + a.displayName + "': " + problem, this);
            return tex;
        }

        string SeenAs(ArchetypeDefinition a)
        {
            if (codexCatalog == null || a.modelIds == null) return "";
            bool hero = false, foe = false;
            foreach (var id in a.modelIds)
            {
                var model = codexCatalog.Find(id);
                if (model == null) continue;
                if (model.playerPrefab != null) hero = true;
                if (model.enemyPrefab != null) foe = true;
            }
            if (hero && foe) return "Hero  /  Opponent";
            return hero ? "Hero" : (foe ? "Opponent" : "");
        }

        // ------------------------------------------------------------------ size and facing
        void ApplyPlacement()
        {
            if (pivot == null) return;
            pivot.localPosition = bookOffset;
            pivot.localRotation = Quaternion.Euler(-tiltTowardViewer, facingYaw, 0f);
        }

        void FitSize()
        {
            if (pivot == null) return;
            if (useManualScale) { pivot.localScale = Vector3.one * manualScale; return; }

            float length = MeasureLongestSide();
            if (length < 1e-4f)
            {
                Debug.LogWarning("[Book] Could not measure the book; using the manual scale (" + manualScale + ").", this);
                pivot.localScale = Vector3.one * manualScale;
                return;
            }
            float scale = bookSizeMeters / length;
            pivot.localScale = Vector3.one * scale;
            if (debugLogs) Debug.Log("[Book] closed book is " + length.ToString("0.000") + " units long -> scale " + scale.ToString("0.0000"));
        }

        // Longest horizontal side of the closed book, in the book's own units (independent of how it is rotated or scaled now).
        float MeasureLongestSide()
        {
            Bounds b = new Bounds();
            bool any = false;
            var tr = book.transform;
            foreach (var r in tr.GetComponentsInChildren<Renderer>(false))
            {
                Mesh m = null;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) m = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) m = mf.sharedMesh;
                }
                if (m == null) continue;

                var mb = m.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var local = tr.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                    else b.Encapsulate(local);
                }
            }
            if (!any) return 0f;
            return Mathf.Max(b.size.x, b.size.z);
        }

        // ------------------------------------------------------------------ many pages at once: the flipping sound
        AudioSource _flipSource;
        Coroutine _flipRoutine;

        void PlayFlipping(float seconds)
        {
            if (_flipRoutine != null) StopCoroutine(_flipRoutine);
            _flipRoutine = StartCoroutine(FlippingRoutine(seconds));
        }

        // Plays the clip for as long as the pages are turning and fades it out at the end (the clip is 3.85 s, the turn is 0.6 - 1.6 s).
        IEnumerator FlippingRoutine(float seconds)
        {
            if (_flipSource == null)
            {
                _flipSource = gameObject.AddComponent<AudioSource>();
                _flipSource.playOnAwake = false;
                _flipSource.spatialBlend = 0f;
            }
            float volume = soundVolume * pagesFlippingVolume * GameAudio.EffectsGain;
            float start = Mathf.Clamp(pagesFlippingStart, 0f, Mathf.Max(0f, pagesFlippingSound.length - 0.5f));
            _flipSource.clip = pagesFlippingSound;
            _flipSource.time = start;
            _flipSource.volume = volume;
            _flipSource.Play();

            float total = Mathf.Min(seconds + 0.15f, pagesFlippingSound.length - start);
            float fade = Mathf.Min(0.35f, total * 0.5f);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (t > total - fade) _flipSource.volume = volume * Mathf.Clamp01((total - t) / fade);
                yield return null;
            }
            _flipSource.Stop();
            _flipRoutine = null;
        }

        // ------------------------------------------------------------------ side tabs
        Transform _tabRoot;
        readonly Transform[] _tabs = new Transform[SectionCount];        // the hinge at the spine; turning it carries the tab over to the other side
        readonly Transform[] _tabBody = new Transform[SectionCount];     // the tab itself, out along the hinge
        readonly Collider[] _tabColliders = new Collider[SectionCount];
        readonly float[] _tabBaseX = new float[SectionCount];
        readonly float[] _angle = new float[SectionCount];               // 0 = on the right edge, 180 = on the left edge
        readonly bool[] _flip = new bool[SectionCount];
        readonly float[] _flipT = new float[SectionCount], _flipDur = new float[SectionCount], _flipFrom = new float[SectionCount], _flipTo = new float[SectionCount];
        float _tabSlideLength;
        float _turnDur = 0.6f;

        public bool WantsWorldTabs { get { return worldTabs; } }
        public bool HasWorldTabs { get { return _tabRoot != null; } }

        string TabLabel(int i)
        {
            switch (i)
            {
                case 0: return "Index";
                case 1: return howToName;
                case 2: return statsName;
                case 3: return armouryName;
                case 4: return codexName;
                default: return graveyardTitle;
            }
        }

        // Size of everything that is visible now (the open book), in the book's own units.
        bool MeasureOpenBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var tr = book.transform;
            foreach (var r in tr.GetComponentsInChildren<Renderer>(false))
            {
                Mesh m = null;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) m = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) m = mf.sharedMesh;
                }
                if (m == null) continue;

                var mb = m.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var local = tr.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return any;
        }

        void DestroyTabs()
        {
            if (_tabRoot != null) Destroy(_tabRoot.gameObject);
            _tabRoot = null;
            for (int i = 0; i < SectionCount; i++) { _tabs[i] = null; _tabBody[i] = null; _tabColliders[i] = null; _flip[i] = false; }
        }

        void BuildTabs()
        {
            DestroyTabs();
            if (!worldTabs || book == null) return;

            Bounds b;
            if (!MeasureOpenBounds(out b)) { Debug.LogWarning("[Book] Could not measure the open book; the buttons under the book are used instead.", this); return; }
            if (b.size.x < b.size.z)
            {
                Debug.LogWarning("[Book] The open book is taller than wide (" + b.size + "), which is not what the tabs expect; the buttons under the book are used instead.", this);
                return;
            }

            float width = b.size.x, pageH = b.size.z;
            float len = tabLength * width;
            float h = tabHeight * pageH;
            float gap = tabGap * pageH;
            float margin = tabTopMargin * pageH;
            float thick = h * 0.18f;
            float y = Mathf.Lerp(b.min.y, b.max.y, tabLevel);
            _tabSlideLength = tabActiveSlide * len;

            var rootGo = new GameObject("BookTabs");
            _tabRoot = rootGo.transform;
            _tabRoot.SetParent(book.transform, false);

            float spine = b.center.x;                          // the hinge line of the book
            float halfW = b.max.x - spine;
            float rightX = halfW + len * 0.5f - len * 0.12f;   // a little of the tab is tucked in under the cover

            for (int i = 0; i < SectionCount; i++)
            {
                float offset = margin + h * 0.5f + i * (h + gap);
                float z = tabsStartAtTop ? b.max.z - offset : b.min.z + offset;

                // hinge at the spine, level with the tab; angle 0 = tab out on the right edge, 180 = out on the left edge (it swings up and over, like a page)
                var hinge = new GameObject("Tab" + i).transform;
                hinge.SetParent(_tabRoot, false);
                hinge.localPosition = new Vector3(spine, y, z);
                _tabs[i] = hinge;

                var body = new GameObject("Body").transform;
                body.SetParent(hinge, false);
                body.localPosition = new Vector3(rightX, 0f, 0f);
                _tabBody[i] = body;
                _tabBaseX[i] = rightX;

                // the coloured plate
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Plate";
                plate.transform.SetParent(body, false);
                plate.transform.localScale = new Vector3(len, thick, h);
                var box = plate.GetComponent<BoxCollider>();
                if (box != null) { box.size = new Vector3(1.05f, 3f, 1.15f); _tabColliders[i] = box; }   // taller than it looks, easier to hit
                var rend = plate.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mat = StageMaterials.Make(tabColors != null && tabColors.Length >= SectionCount ? tabColors[i] : DefaultTabColors()[i]);
                    if (mat != null) rend.sharedMaterial = mat;
                }

                // the name, once on each face, so it reads the right way up on whichever side the tab is lying
                float lift = thick * 0.5f + thick * 0.06f;
                MakeTabLabel(body, i, h, len, lift, Quaternion.Euler(90f, 0f, 0f));
                MakeTabLabel(body, i, h, len, -lift, Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(90f, 0f, 0f));

                _angle[i] = TabOnLeft(i) ? 180f : 0f;
                hinge.localRotation = Quaternion.Euler(0f, 0f, _angle[i]);
            }

            _tabRoot.gameObject.SetActive(false);   // Update shows them while the book is open and still
            if (debugLogs) Debug.Log("[Book] Tabs built. Open book (book units): " + b.size + " at " + b.center);
        }

        void MakeTabLabel(Transform parent, int i, float h, float len, float yLocal, Quaternion rot)
        {
            var label = new GameObject("Label", typeof(TextMeshPro));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, yLocal, 0f);
            label.transform.localRotation = rot;
            label.transform.localScale = Vector3.one * h;
            var tmp = label.GetComponent<TextMeshPro>();
            tmp.text = TabLabel(i);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 100f;
            tmp.rectTransform.sizeDelta = new Vector2(len * 0.9f / h, 0.78f);
        }

        // A bookmark sits on the first page of its section. Once the book shows that page (or a later one) on the left, the page has been turned over and
        // the bookmark lies on the left edge with it; before that it is on the right edge.
        bool TabOnLeft(int section)
        {
            int p = _sectionPage[section];
            return p > 0 && p <= book.CurrentLeftPageNumber;
        }

        // Called by the book when a sheet starts turning. The sheet whose back is the first page of a section carries that section's bookmark with it.
        void OnSheetTurn(Page page, int pageNumberFront, int pageNumberBack, int pageNumberFirstVisible, int pageNumberLastVisible, Page.TurnDirectionEnum direction)
        {
            if (_tabRoot == null) return;
            bool forward = direction == Page.TurnDirectionEnum.TurnForward;
            for (int i = 0; i < SectionCount; i++)
            {
                if (_tabs[i] == null || _sectionPage[i] <= 0) continue;
                if (_sectionPage[i] != pageNumberBack && _sectionPage[i] != pageNumberFront) continue;
                _flip[i] = true;
                _flipT[i] = 0f;
                _flipDur[i] = Mathf.Max(0.1f, _turnDur);
                _flipFrom[i] = _angle[i];
                _flipTo[i] = forward ? 180f : 0f;
            }
        }

        int CurrentSection()
        {
            int left = book.CurrentLeftPageNumber;
            int cur = 0;
            for (int s = 0; s < SectionCount; s++) if (_sectionPage[s] > 0 && left >= _sectionPage[s]) cur = s;
            return cur;
        }

        void Update()
        {
            CodexTick();
            if (_tabRoot == null || book == null) return;

            bool show = IsOpen && !_closing && book.CurrentState == BookComponent.StateEnum.OpenMiddle;
            if (_tabRoot.gameObject.activeSelf != show) _tabRoot.gameObject.SetActive(show);
            if (!show) return;

            // bookmarks: turn over with their page, then sit on the side their page is on
            bool turning = book.IsTurningPages;
            int cur = CurrentSection();
            for (int i = 0; i < SectionCount; i++)
            {
                if (_tabs[i] == null) continue;
                if (_flip[i])
                {
                    _flipT[i] += Time.deltaTime / _flipDur[i];
                    float u = Mathf.Clamp01(_flipT[i]);
                    u = u * u * (3f - 2f * u);
                    _angle[i] = Mathf.Lerp(_flipFrom[i], _flipTo[i], u);
                    if (_flipT[i] >= 1f) { _flip[i] = false; _angle[i] = _flipTo[i]; }
                }
                else if (!turning)
                {
                    _angle[i] = TabOnLeft(i) ? 180f : 0f;   // always ends up right, whatever happened
                }
                _tabs[i].localRotation = Quaternion.Euler(0f, 0f, _angle[i]);

                // the tab of the section you are in slides out a little
                var body = _tabBody[i];
                float targetX = _tabBaseX[i] + (i == cur ? _tabSlideLength : 0f);
                var lp = body.localPosition;
                lp.x = Mathf.Lerp(lp.x, targetX, 1f - Mathf.Exp(-14f * Time.deltaTime));
                body.localPosition = lp;
            }

            // tap on a tab
            if (IsBusy) return;
            Vector2 pos;
            if (!PointerPressed(out pos)) return;
            var cam = Camera.main;
            if (cam == null) return;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(pos), 50f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            int hit = -1;
            foreach (var h in hits)
                for (int i = 0; i < SectionCount; i++)
                    if (_tabColliders[i] != null && h.collider == _tabColliders[i] && h.distance < best) { best = h.distance; hit = i; }
            if (hit >= 0) JumpTo((Section)hit);
        }

        static bool PointerPressed(out Vector2 pos)
        {
            pos = default(Vector2);
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) { pos = touch.primaryTouch.position.ReadValue(); return true; }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) { pos = mouse.position.ReadValue(); return true; }
            return false;
        }

        // ------------------------------------------------------------------ closing
        IEnumerator CloseRoutine(Action done)
        {
            IsOpen = false;
            if (book != null && book.CurrentState != BookComponent.StateEnum.ClosedFront)
            {
                PlayAt(closeSound, closeSeconds + closeSoundOffset, 1f);
                bool finished = false;
                book.SetState(BookComponent.StateEnum.ClosedFront, closeSeconds, (a, b, c) => finished = true, true);
                float timeout = Time.unscaledTime + closeSeconds + 1.5f;
                while (!finished && Time.unscaledTime < timeout) yield return null;
            }
            if (done != null) done();
        }

        // ------------------------------------------------------------------ cleanup
        void OnDestroy()
        {
            DisposeCamera();
            foreach (var m in _materials) if (m != null) Destroy(m);
            foreach (var t in _textures) if (t != null) Destroy(t);
            _materials.Clear();
            _textures.Clear();
            foreach (var kv in _codexMat) if (kv.Value != null) Destroy(kv.Value);
            foreach (var kv in _codexTex) if (kv.Value != null) Destroy(kv.Value);
            _codexMat.Clear();
            _codexTex.Clear();
        }

        /// <summary>Hides the book and removes it once its last sound has finished (destroying it at once would cut the close thud off).</summary>
        public void Dismiss()
        {
            if (pivot != null) pivot.gameObject.SetActive(false);
            float tail = closeSound != null ? closeSound.length + 0.2f : 0f;
            Destroy(gameObject, tail);
        }

        void PlayAt(AudioClip clip, float delaySeconds, float volumeScale)
        {
            if (clip == null) return;
            if (debugLogs) Debug.Log("[Book] sound '" + clip.name + "' in " + Mathf.Max(0f, delaySeconds).ToString("0.00") + "s");
            if (delaySeconds <= 0.01f) { Play(clip, volumeScale); return; }
            StartCoroutine(PlayAfter(clip, delaySeconds, volumeScale));
        }

        IEnumerator PlayAfter(AudioClip clip, float delaySeconds, float volumeScale)
        {
            float t = 0f;
            while (t < delaySeconds) { t += Time.unscaledDeltaTime; yield return null; }
            Play(clip, volumeScale);
        }

        void Play(AudioClip clip, float volumeScale)
        {
            if (clip == null) return;
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;   // plain 2D sound
                }
            }
            float v = soundVolume * volumeScale * GameAudio.EffectsGain;   // the Effects slider in Settings
            if (v <= 0.0005f) return;
            audioSource.PlayOneShot(clip, v);
        }

        static void Raise(Action a) { if (a != null) a(); }
    }
}
