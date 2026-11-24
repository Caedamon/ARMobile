#if UNITY_EDITOR
using System.Collections.Generic;
using Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using BookComponent = echo17.EndlessBook.EndlessBook;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Game > Create Book Prefabs
//
// Makes the AR Book as real prefabs you can open and edit (Assets/Prefabs/Book):
//   FighterPage.prefab, GravePage.prefab, BlankPage.prefab, BookStage.prefab
// adds the draggable Book icon + Book screen to your GameUI prefab (only if they are missing),
// and plugs the BookStage into the GameController prefab.
// Existing prefabs are NOT touched, so your edits are safe. The "overwrite" item rebuilds the Book prefabs.
namespace KaijuTools
{
    public static class BookPrefabBuilder
    {
        const string BookDir = "Assets/Prefabs/Book";
        const string ControllerPrefab = "Assets/Prefabs/AR/GameController.prefab";

        static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f, 1f);
        static readonly Color ParchmentDark = new Color(0.82f, 0.78f, 0.70f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.12f, 0.07f, 1f);
        static readonly Color InkSoft = new Color(0.32f, 0.24f, 0.15f, 1f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.15f);
        static readonly Color BarFill = new Color(0.72f, 0.46f, 0.10f, 1f);
        static readonly string[] StatNames = { "MIGHT", "EGO", "EDUCATION", "INSIGHT" };

        [MenuItem("Tools/Kaiju/Game/Create Book Prefabs")]
        public static void Create() { Run(false); }

        [MenuItem("Tools/Kaiju/Game/Create Book Prefabs (overwrite - loses your edits)")]
        public static void CreateOverwrite()
        {
            if (!EditorUtility.DisplayDialog("Overwrite Book prefabs?",
                "This rebuilds FighterPage, GravePage, BlankPage and BookStage from scratch and throws away any edits you made to them. (GameUI is never overwritten.)",
                "Overwrite", "Cancel")) return;
            Run(true);
        }

        static void Run(bool overwrite)
        {
            var bookPrefab = FindBookPrefab();
            if (bookPrefab == null)
            {
                Debug.LogError("[BookPrefabs] Could not find the EndlessBook 'Book' prefab (expected Assets/EndlessBook/Book/Prefabs/Book.prefab). Import the EndlessBook URP package first.");
                return;
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Book");

            var fighterPage = MakeFighterPage(overwrite);
            var gravePage = MakeGravePage(overwrite);
            var blankPage = MakeBlankPage(overwrite);
            var stage = MakeBookStage(bookPrefab, fighterPage, gravePage, blankPage, overwrite);

            bool soundsOk = AddSounds(stage);
            bool uiOk = AddBookUiToPrefab();
            bool assigned = stage != null && Assign(stage);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BookPrefabs] Done. Prefabs are in " + BookDir + ".  GameUI has the Book screen: " + (uiOk ? "OK" : "NOT DONE (see warnings)") +
                      ".  Plugged into the GameController: " + (assigned ? "OK" : "NOT DONE (see warnings)") +
                      ".  Sounds: " + (soundsOk ? "OK" : "NOT DONE (see warnings)"));
        }

        // ------------------------------------------------------------------ page prefabs
        static FighterPageView MakeFighterPage(bool overwrite)
        {
            string path = BookDir + "/FighterPage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<FighterPageView>(path);

            var root = NewPageRoot("FighterPage", Parchment);
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<FighterPageView>();

                var nameT  = Txt(rt, "Name",  "Hrolf Ironjaw", 70f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.08f, 0.86f, 0.92f, 0.95f);
                var tierT  = Txt(rt, "Tier",  "High quality",  42f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.08f, 0.81f, 0.92f, 0.865f);
                var levelT = Txt(rt, "Level", "Level 3    XP 40 / 200", 34f, InkSoft, TextAlignmentOptions.Left, FontStyles.Normal, 0.08f, 0.76f, 0.92f, 0.81f);

                var xpBack = Img(rt, "XpBar", BarBack, 0.08f, 0.735f, 0.92f, 0.755f);
                var xpFill = Img(xpBack.transform, "Fill", BarFill, 0f, 0f, 0.4f, 1f);

                var values = new TMP_Text[4];
                var fills = new RectTransform[4];
                for (int i = 0; i < 4; i++)
                {
                    float top = 0.70f - i * 0.065f;
                    float bottom = top - 0.055f;
                    Txt(rt, "Label" + i, StatNames[i], 32f, InkSoft, TextAlignmentOptions.Left, FontStyles.Normal, 0.08f, bottom, 0.37f, top);
                    var back = Img(rt, "Bar" + i, BarBack, 0.38f, bottom + 0.012f, 0.82f, top - 0.012f);
                    fills[i] = Img(back.transform, "Fill", BarFill, 0f, 0f, 0.5f, 1f).rectTransform;
                    values[i] = Txt(rt, "Value" + i, "5", 38f, Ink, TextAlignmentOptions.Right, FontStyles.Bold, 0.83f, bottom, 0.92f, top);
                }

                var style = Txt(rt, "Style", "Prefers: Two-handed", 32f, Ink, TextAlignmentOptions.Left, FontStyles.Italic, 0.08f, 0.38f, 0.92f, 0.43f);
                var prof = Txt(rt, "Proficiency", "Proficiency\nUnarmed 1\nOne-handed 2", 30f, InkSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0.08f, 0.14f, 0.92f, 0.37f);
                var record = Txt(rt, "Record", "Record:  2 wins  in  3 fights", 34f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.08f, 0.05f, 0.92f, 0.12f);

                var so = new SerializedObject(view);
                so.FindProperty("nameText").objectReferenceValue = nameT;
                so.FindProperty("tierText").objectReferenceValue = tierT;
                so.FindProperty("levelText").objectReferenceValue = levelT;
                so.FindProperty("xpFill").objectReferenceValue = xpFill.rectTransform;
                var sv = so.FindProperty("statValues");
                var sf = so.FindProperty("statFills");
                sv.arraySize = 4;
                sf.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    sv.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                    sf.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
                }
                so.FindProperty("styleText").objectReferenceValue = style;
                so.FindProperty("proficiencyText").objectReferenceValue = prof;
                so.FindProperty("recordText").objectReferenceValue = record;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<FighterPageView>(path);
        }

        static GravePageView MakeGravePage(bool overwrite)
        {
            string path = BookDir + "/GravePage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<GravePageView>(path);

            var root = NewPageRoot("GravePage", ParchmentDark);
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<GravePageView>();

                var title = Txt(rt, "Title",   "R.I.P.",            120f, Ink,     TextAlignmentOptions.Center, FontStyles.Bold,   0.08f, 0.74f, 0.92f, 0.90f);
                var nameT = Txt(rt, "Name",    "Hrolf Ironjaw",      68f, Ink,     TextAlignmentOptions.Center, FontStyles.Bold,   0.08f, 0.58f, 0.92f, 0.72f);
                var det   = Txt(rt, "Details", "High quality   Level 3\n2 wins", 40f, InkSoft, TextAlignmentOptions.Center, FontStyles.Normal, 0.08f, 0.42f, 0.92f, 0.56f);
                var death = Txt(rt, "Death",   "Died against Astrid the Unlucky\nin match 4", 44f, Ink, TextAlignmentOptions.Center, FontStyles.Italic, 0.08f, 0.18f, 0.92f, 0.40f);

                var so = new SerializedObject(view);
                so.FindProperty("titleText").objectReferenceValue = title;
                so.FindProperty("nameText").objectReferenceValue = nameT;
                so.FindProperty("detailsText").objectReferenceValue = det;
                so.FindProperty("deathText").objectReferenceValue = death;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GravePageView>(path);
        }

        static GameObject MakeBlankPage(bool overwrite)
        {
            string path = BookDir + "/BlankPage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = NewPageRoot("BlankPage", Parchment);
            try { PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // A page: a Canvas (BookStage redirects it to its own camera at run time) with a parchment background.
        static GameObject NewPageRoot(string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(824f, 1024f);   // the part of EndlessBook's square page picture that is shown
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Img(go.transform, "Background", background, 0f, 0f, 1f, 1f);
            return go;
        }

        // ------------------------------------------------------------------ BookStage
        static BookStage MakeBookStage(GameObject bookPrefab, FighterPageView fighterPage, GravePageView gravePage,
                                       GameObject blankPage, bool overwrite)
        {
            string path = BookDir + "/BookStage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<BookStage>(path);

            var root = new GameObject("BookStage");
            try
            {
                var stage = root.AddComponent<BookStage>();

                var pivot = new GameObject("BookPivot").transform;
                pivot.SetParent(root.transform, false);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(bookPrefab, pivot);
                var book = inst.GetComponent<BookComponent>();
                if (book == null) book = inst.GetComponentInChildren<BookComponent>();

                var so = new SerializedObject(stage);
                so.FindProperty("book").objectReferenceValue = book;
                so.FindProperty("pivot").objectReferenceValue = pivot;
                so.FindProperty("fighterPagePrefab").objectReferenceValue = fighterPage;
                so.FindProperty("gravePagePrefab").objectReferenceValue = gravePage;
                so.FindProperty("blankPagePrefab").objectReferenceValue = blankPage;
                so.FindProperty("pageMaterialTemplate").objectReferenceValue = FindPageMaterial();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<BookStage>(path);
        }

        static GameObject FindBookPrefab()
        {
            var preferred = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndlessBook/Book/Prefabs/Book.prefab");
            if (preferred != null) return preferred;
            foreach (var guid in AssetDatabase.FindAssets("Book t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith("/Book.prefab")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null && go.GetComponentInChildren<BookComponent>() != null) return go;
            }
            return null;
        }

        static Material FindPageMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/EndlessBook/Book/Materials/PageFront.mat");
            if (m != null) return m;
            foreach (var guid in AssetDatabase.FindAssets("PageFront t:Material"))
            {
                m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) return m;
            }
            return null;
        }

        // ------------------------------------------------------------------ sounds on the BookStage prefab (fills only what is empty)
        const string SoundDir = "Assets/EndlessBook/Book/Sounds/";

        static bool AddSounds(BookStage stage)
        {
            if (stage == null) return false;
            string path = AssetDatabase.GetAssetPath(stage);
            if (string.IsNullOrEmpty(path)) return false;

            var open = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundDir + "BookOpen.wav");
            var close = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundDir + "BookClose.wav");
            var turn = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundDir + "PageTurn.wav");
            if (open == null || close == null || turn == null)
                Debug.LogWarning("[BookPrefabs] Some book sounds were not found in " + SoundDir + " (BookOpen, BookClose, PageTurn).");

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bs = root.GetComponent<BookStage>();
                if (bs == null) return false;

                var src = root.GetComponent<AudioSource>();
                if (src == null)
                {
                    src = root.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;
                }

                var so = new SerializedObject(bs);
                bool changed = false;
                changed |= SetIfEmpty(so, "audioSource", src);
                changed |= SetIfEmpty(so, "openSound", open);
                changed |= SetIfEmpty(so, "closeSound", close);
                changed |= SetIfEmpty(so, "pageTurnSound", turn);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                if (changed) Debug.Log("[BookPrefabs] Added the book sounds (open, close, page turn) to " + path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool SetIfEmpty(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null || value == null || p.objectReferenceValue != null) return false;
            p.objectReferenceValue = value;
            return true;
        }

        // ------------------------------------------------------------------ GameUI: add the Book screen (only if missing)
        static bool AddBookUiToPrefab()
        {
            string path = FindAsset("GameUI", "Assets/Prefabs/Game/GameUI.prefab");
            if (path == null)
            {
                Debug.LogWarning("[BookPrefabs] No GameUI prefab found. Run Tools > Kaiju > Game > Create Game Prefabs first, then run this again.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { Debug.LogWarning("[BookPrefabs] No GameUI component on " + path); return false; }

                var so = new SerializedObject(ui);
                bool missing = so.FindProperty("_bookPanel").objectReferenceValue == null ||
                               so.FindProperty("_bookIcon").objectReferenceValue == null ||
                               so.FindProperty("_bookGhost").objectReferenceValue == null;
                if (missing)
                {
                    ui.AddBookUi();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[BookPrefabs] Added the draggable Book icon (hub panel) and the Book screen to " + path + " (an old BookButton is removed).");
                }
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static string FindAsset(string name, string preferred)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(preferred) != null) return preferred;
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/" + name + ".prefab")) return p;
            }
            return null;
        }

        // ------------------------------------------------------------------ plug into the GameController
        static bool Assign(BookStage stage)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefab) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(ControllerPrefab);
                try
                {
                    var gc = root.GetComponent<GameController>();
                    if (gc == null) { Debug.LogWarning("[BookPrefabs] No GameController on " + ControllerPrefab); return false; }
                    SetRef(gc, stage);
                    PrefabUtility.SaveAsPrefabAsset(root, ControllerPrefab);
                    return true;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var sceneGc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
            if (sceneGc == null) { Debug.LogWarning("[BookPrefabs] No GameController found (prefab or open scene)."); return false; }
            SetRef(sceneGc, stage);
            EditorUtility.SetDirty(sceneGc);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        static void SetRef(GameController gc, BookStage stage)
        {
            var so = new SerializedObject(gc);
            var p = so.FindProperty("bookStagePrefab");
            if (p.objectReferenceValue == null || p.objectReferenceValue != stage) p.objectReferenceValue = stage;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ small helpers
        static Image Img(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Frac(img.rectTransform, x0, y0, x1, y1);
            return img;
        }

        static TMP_Text Txt(Transform parent, string name, string text, float size, Color color,
                             TextAlignmentOptions align, FontStyles style, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.enableAutoSizing = true;          // long names shrink to fit instead of spilling off the page
            t.fontSizeMin = 14f;
            t.fontSizeMax = size;
            Frac(t.rectTransform, x0, y0, x1, y1);
            return t;
        }

        static void Frac(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
