using System;
using System.Collections;
using System.Collections.Generic;
using echo17.EndlessBook;
using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BookComponent = echo17.EndlessBook.EndlessBook;

namespace Game
{
    /// <summary>
    /// The AR Book on the table (a stage, like the roster and the tombstone).
    /// It draws each page prefab (FighterPage, GravePage, BlankPage) into a texture, hands the textures to the
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
        [Tooltip("The graveyard shows the most recent fallen fighters. Each page picture costs about 3 MB of memory.")]
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

        [Header("Sound timing (seconds)")]
        [Tooltip("Relative to the moment the opening book touches the table (the end of the open animation). The thud in the clip lands 0.06 s after it starts, so -0.06 puts the thud exactly on the landing. More negative = earlier, positive = later.")]
        [SerializeField] float openSoundOffset = -0.06f;
        [Tooltip("Relative to the moment the closing book touches the table (the end of the close animation). Same idea as above.")]
        [SerializeField] float closeSoundOffset = -0.06f;
        [Tooltip("Delay after a page starts turning. The swoosh in the clip builds up for about half a second, so 0 lands it late in the turn.")]
        [SerializeField] float pageTurnSoundDelay = 0f;

        [Header("Debug")]
        [SerializeField] bool debugLogs = false;

        public event Action Opened;
        public event Action PageChanged;
        public event Action Failed;

        public bool IsOpen { get; private set; }
        public bool IsBusy { get { return _closing || (book != null && (book.IsTurningPages || book.CurrentState != BookComponent.StateEnum.OpenMiddle)); } }
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
            book.TurnForward(turnSeconds, onCompleted: (a, b, c) => Raise(PageChanged));
            Raise(PageChanged);
        }

        public void TurnBackward()
        {
            if (!CanTurnBack) return;
            PlayAt(pageTurnSound, pageTurnSoundDelay, pageTurnVolume);
            book.TurnBackward(turnSeconds, onCompleted: (a, b, c) => Raise(PageChanged));
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
            _pageCount = specs.Count;
            if (debugLogs) Debug.Log("[Book] " + _pageCount + " pages (" + (fallen != null ? fallen.Count : 0) + " fallen)");

            // 2. draw them
            MakeCamera();
            for (int i = 0; i < specs.Count; i++)
            {
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
            if (debugLogs) Debug.Log("[Book] Open. state=" + book.CurrentState + " page=" + book.CurrentPageNumber);
            Raise(Opened);
            Raise(PageChanged);
        }

        enum Kind { Fighter, Grave, Empty, Blank }

        struct Spec
        {
            public Kind kind;
            public FighterData fighter;
            public FallenFighter fallen;
        }

        List<Spec> BuildSpecs(FighterData current, List<FallenFighter> fallen)
        {
            var list = new List<Spec>();
            list.Add(current != null ? new Spec { kind = Kind.Fighter, fighter = current } : new Spec { kind = Kind.Blank });

            int shown = 0;
            if (fallen != null)
            {
                for (int i = fallen.Count - 1; i >= 0 && shown < maxFallenPages; i--, shown++)
                    list.Add(new Spec { kind = Kind.Grave, fallen = fallen[i] });
            }
            if (shown == 0) list.Add(new Spec { kind = Kind.Empty });

            if (list.Count % 2 == 1) list.Add(new Spec { kind = Kind.Blank });   // the book needs pairs of pages
            return list;
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

            _rt = new RenderTexture(pageTextureSize, pageTextureSize, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _rt.useMipMap = false;
            _rt.Create();

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
                default:           prefab = blankPagePrefab; break;
            }

            GameObject go = null;
            if (prefab != null)
            {
                go = Instantiate(prefab, _sheet);
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

                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
                for (int i = 0; i < Mathf.Max(1, settleFrames); i++) yield return null;
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
            }

            _cam.targetTexture = _rt;
            _cam.Render();

            // Copy into a normal texture so the page survives the phone losing its graphics memory (app switch).
            var tex = new Texture2D(pageTextureSize, pageTextureSize, TextureFormat.RGBA32, true);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            tex.ReadPixels(new Rect(0, 0, pageTextureSize, pageTextureSize), 0, 0);
            tex.Apply(true, true);          // build mipmaps, free the CPU copy
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            _textures.Add(tex);

            if (go != null) Destroy(go);

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
