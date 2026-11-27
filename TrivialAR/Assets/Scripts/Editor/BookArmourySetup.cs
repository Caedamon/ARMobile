#if UNITY_EDITOR
using System.Collections.Generic;
using Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Weaponary;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Book > Set Up Index, Stats and Armoury
//
// Adds the new Book pages and plugs them in:
//   - four page prefabs in Assets/Prefabs/Book: IndexPage, TextPage, WeaponPicturePage, WeaponInfoPage (edit them freely)
//   - a flat (unlit) material for the weapon pictures: Assets/Materials/Book/WeaponFlat.mat
//   - the BookStage prefab gets them, plus the list of weapons for the Armoury (from every Weapon Definition asset)
//   - the four tabs under the Book on the GameUI prefab
//   - empty weapon descriptions get a starting text (descriptions you already wrote are never touched)
// Nothing existing is overwritten. To rebuild one of the new page prefabs, delete it and run this again.
namespace KaijuTools
{
    public static class BookArmourySetup
    {
        const string BookDir = "Assets/Prefabs/Book";
        const string MaterialDir = "Assets/Materials/Book";

        static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.12f, 0.07f, 1f);
        static readonly Color InkSoft = new Color(0.32f, 0.24f, 0.15f, 1f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.15f);
        static readonly Color BarFill = new Color(0.72f, 0.46f, 0.10f, 1f);
        static readonly Color Rule = new Color(0.32f, 0.24f, 0.15f, 0.45f);

        [MenuItem("Tools/Kaiju/Book/Set Up Index, Stats and Armoury")]
        public static void Run()
        {
            string stagePath = FindPrefab("BookStage");
            if (stagePath == null)
            {
                Debug.LogWarning("[BookArmoury] No BookStage prefab found. Run Tools > Kaiju > Game > Create Book Prefabs first, then run this again.");
                return;
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Book");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "Book");

            var indexPage = MakeIndexPage();
            var textPage = MakeTextPage();
            var picturePage = MakePicturePage();
            var infoPage = MakeInfoPage();
            var flat = MakeFlatMaterial();
            var weaponList = FindWeapons();
            int described = FillDescriptions(weaponList);

            bool stageOk = PlugIntoBookStage(stagePath, indexPage, textPage, picturePage, infoPage, flat, weaponList);
            bool uiOk = AddTabsToGameUi();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BookArmoury] Done.  BookStage: " + (stageOk ? "OK" : "NOT DONE (see warnings)") +
                      ".  Tabs on GameUI: " + (uiOk ? "OK" : "NOT DONE (see warnings)") +
                      ".  Weapons in the Armoury: " + weaponList.Count + ".  Descriptions written: " + described +
                      ".  Weapon picture material: " + (flat != null ? "OK" : "MISSING (see warnings)"));
        }

        // ------------------------------------------------------------------ page prefabs
        static IndexPageView MakeIndexPage()
        {
            string path = BookDir + "/IndexPage.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return AssetDatabase.LoadAssetAtPath<IndexPageView>(path);

            var root = NewPageRoot("IndexPage");
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<IndexPageView>();

                var title = Txt(rt, "Title", "Index", 100f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.82f, 0.92f, 0.95f);
                Img(rt, "Rule", Rule, 0.15f, 0.805f, 0.85f, 0.81f);

                var names = new TMP_Text[6];
                var pages = new TMP_Text[6];
                for (int i = 0; i < 6; i++)
                {
                    float top = 0.74f - i * 0.10f;
                    names[i] = Txt(rt, "Entry" + i, "Section", 54f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.10f, top - 0.08f, 0.70f, top);
                    pages[i] = Txt(rt, "Page" + i, "p. 1", 46f, InkSoft, TextAlignmentOptions.Right, FontStyles.Normal, 0.70f, top - 0.08f, 0.90f, top);
                }
                var hint = Txt(rt, "Hint", "Tap a tab under the book to jump there.", 32f, InkSoft, TextAlignmentOptions.Center, FontStyles.Italic, 0.08f, 0.05f, 0.92f, 0.13f);

                var so = new SerializedObject(view);
                so.FindProperty("titleText").objectReferenceValue = title;
                so.FindProperty("hintText").objectReferenceValue = hint;
                Assign(so.FindProperty("entryNames"), names);
                Assign(so.FindProperty("entryPages"), pages);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<IndexPageView>(path);
        }

        static TextPageView MakeTextPage()
        {
            string path = BookDir + "/TextPage.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return AssetDatabase.LoadAssetAtPath<TextPageView>(path);

            var root = NewPageRoot("TextPage");
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<TextPageView>();

                var title = Txt(rt, "Title", "Stats", 80f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.88f, 0.92f, 0.97f);

                var heads = new TMP_Text[3];
                var bodies = new TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    float top = 0.85f - i * 0.285f;
                    heads[i] = Txt(rt, "Heading" + i, "HEADING", 52f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.08f, top - 0.055f, 0.92f, top);
                    bodies[i] = Txt(rt, "Body" + i, "Words about it.", 34f, InkSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0.08f, top - 0.27f, 0.92f, top - 0.06f);
                    if (i < 2) Img(rt, "Rule" + i, Rule, 0.08f, top - 0.282f, 0.92f, top - 0.277f);
                }

                var so = new SerializedObject(view);
                so.FindProperty("titleText").objectReferenceValue = title;
                Assign(so.FindProperty("headings"), heads);
                Assign(so.FindProperty("bodies"), bodies);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<TextPageView>(path);
        }

        static WeaponPicturePageView MakePicturePage()
        {
            string path = BookDir + "/WeaponPicturePage.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return AssetDatabase.LoadAssetAtPath<WeaponPicturePageView>(path);

            var root = NewPageRoot("WeaponPicturePage");
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<WeaponPicturePageView>();

                var title = Txt(rt, "Title", "Sword", 76f, Ink, TextAlignmentOptions.Center, FontStyles.Bold, 0.08f, 0.88f, 0.92f, 0.97f);

                // The picture (square, see through where there is no weapon). Keep this rectangle about square so the weapon is not stretched.
                var go = new GameObject("Picture", typeof(RectTransform));
                go.transform.SetParent(rt, false);
                var raw = go.AddComponent<RawImage>();
                raw.color = Color.white;
                raw.raycastTarget = false;
                Frac(raw.rectTransform, 0.06f, 0.13f, 0.94f, 0.87f);

                var so = new SerializedObject(view);
                so.FindProperty("titleText").objectReferenceValue = title;
                so.FindProperty("picture").objectReferenceValue = raw;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<WeaponPicturePageView>(path);
        }

        static WeaponInfoPageView MakeInfoPage()
        {
            string path = BookDir + "/WeaponInfoPage.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return AssetDatabase.LoadAssetAtPath<WeaponInfoPageView>(path);

            var root = NewPageRoot("WeaponInfoPage");
            try
            {
                var rt = root.transform;
                var view = root.AddComponent<WeaponInfoPageView>();

                var nameT = Txt(rt, "Name", "Sword", 70f, Ink, TextAlignmentOptions.Left, FontStyles.Bold, 0.08f, 0.88f, 0.92f, 0.97f);
                var typeT = Txt(rt, "Type", "One-handed", 38f, InkSoft, TextAlignmentOptions.Left, FontStyles.Italic, 0.08f, 0.83f, 0.92f, 0.88f);

                string[] labels = { "Damage", "Reach", "Speed" };
                var fills = new RectTransform[3];
                var values = new TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    float top = 0.79f - i * 0.065f;
                    Txt(rt, "Label" + i, labels[i], 36f, InkSoft, TextAlignmentOptions.Left, FontStyles.Normal, 0.08f, top - 0.055f, 0.34f, top);
                    var back = Img(rt, "Bar" + i, BarBack, 0.35f, top - 0.043f, 0.78f, top - 0.012f);
                    fills[i] = Img(back.transform, "Fill", BarFill, 0f, 0f, 0.5f, 1f).rectTransform;
                    values[i] = Txt(rt, "Value" + i, "25", 40f, Ink, TextAlignmentOptions.Right, FontStyles.Bold, 0.79f, top - 0.055f, 0.92f, top);
                }

                var moves = Txt(rt, "Moves", "Moves:  Chop, Slice, Stab", 32f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Italic, 0.08f, 0.545f, 0.92f, 0.60f);
                Img(rt, "Rule", Rule, 0.08f, 0.535f, 0.92f, 0.54f);
                var desc = Txt(rt, "Description", "What the weapon is like.", 36f, InkSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal, 0.08f, 0.06f, 0.92f, 0.52f);

                var so = new SerializedObject(view);
                so.FindProperty("nameText").objectReferenceValue = nameT;
                so.FindProperty("typeText").objectReferenceValue = typeT;
                so.FindProperty("attacksText").objectReferenceValue = moves;
                so.FindProperty("descriptionText").objectReferenceValue = desc;
                Assign(so.FindProperty("statFills"), fills);
                Assign(so.FindProperty("statValues"), values);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<WeaponInfoPageView>(path);
        }

        // A page: a Canvas (BookStage redirects it to its own camera at run time) with a parchment background.
        static GameObject NewPageRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(824f, 1024f);   // the part of EndlessBook's square page picture that is shown
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Img(go.transform, "Background", Parchment, 0f, 0f, 1f, 1f);
            return go;
        }

        // ------------------------------------------------------------------ weapon picture material
        static Material MakeFlatMaterial()
        {
            string path = MaterialDir + "/WeaponFlat.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[BookArmoury] The URP Unlit shader was not found, so the weapon pictures will use the models' own materials (lit by the scene). " +
                                 "Make a material with an Unlit shader and put it in BookStage > Weapon Flat Material.");
                return null;
            }
            var m = new Material(shader) { name = "WeaponFlat" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ weapons and descriptions
        static List<WeaponDefinition> FindWeapons()
        {
            var list = new List<WeaponDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && w.visualPrefab != null) list.Add(w);
            }
            list.Sort((a, b) =>
            {
                int c = ClassOrder(a.weaponClass).CompareTo(ClassOrder(b.weaponClass));
                return c != 0 ? c : string.CompareOrdinal(a.displayName, b.displayName);
            });
            return list;
        }

        // One-handed, then sword and shield, then two-handed, then dual wield.
        static int ClassOrder(WeaponClass c)
        {
            switch (c)
            {
                case WeaponClass.OneHand:          return 0;
                case WeaponClass.OneHandAndShield: return 1;
                case WeaponClass.TwoHand:          return 2;
                case WeaponClass.DualOneHand:      return 3;
                default:                           return 4;
            }
        }

        static int FillDescriptions(List<WeaponDefinition> weapons)
        {
            int written = 0;
            foreach (var w in weapons)
            {
                if (!string.IsNullOrEmpty(w.description)) continue;   // never touch what you wrote yourself
                string text = DefaultDescription(w);
                var so = new SerializedObject(w);
                so.FindProperty("description").stringValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(w);
                written++;
            }
            return written;
        }

        static string DefaultDescription(WeaponDefinition w)
        {
            switch (w.displayName)
            {
                case "Sword":
                    return "A plain, honest blade. Balanced and quick, with nothing clever about it. One-handed fighters land more critical hits the more they train with it.";
                case "Axe":
                    return "A woodcutter's axe that found a better job. It hits a little harder than a sword and comes down with a heavy chop. One-handed, so critical hits improve with training.";
                case "Warhammer":
                    return "Made to crack armour and skulls alike. The hardest hitter of the one-handed weapons, and not graceful about it.";
                case "Greatsword":
                    return "A huge blade for strong arms. Every hit lands hard and the long reach keeps enemies back. Two-handed fighters become harder to knock off balance with training.";
                case "Greataxe":
                    return "The heaviest weapon in the Armoury. One swing can end a fight, and it takes both hands and a lot of nerve.";
                case "Spear":
                    return "Reach is the whole idea: hit them before they can hit you. Two-handed, so a trained fighter hardly flinches when struck.";
                case "Dual Knives":
                    return "Two small blades, very fast. Each cut is weak, but there are many of them. Dual wielders gain attack speed with training, and clever fighters love them.";
                case "Sword & Round Shield":
                    return "A sword and a wooden round shield that turns a blow aside with a dull thud. Sword and shield fighters block more often the more they train.";
                case "Axe & Barbarian Shield":
                    return "A rough axe and a rough shield, made for people who do not plan to retreat. A solid mix of attack and defence.";
                case "Black Knight Sword & Shield":
                    return "Dark steel for a cold fighter: a well-made sword and a metal shield that rings when it blocks.";
                case "Club & Spike Shield":
                    return "A club to bash with and a spiked shield to keep them honest. Nothing fancy, but it hurts.";
                case "Hammer & Paladin Shield":
                    return "A holy hammer and a wooden paladin shield. It hits hard for a shield fighter, and the shield soaks up the rest.";
                case "Knight's Sword & Tower Shield":
                    return "A knight's sword behind a tall steel tower shield. When it blocks, it rings.";
                case "Vampire Sword & Badge Shield":
                    return "A thin dark blade and a badge shield with a sinister reputation. Nobody remembers who owned it first.";
            }

            switch (w.weaponClass)
            {
                case WeaponClass.TwoHand:          return "A two-handed weapon: slow but heavy. Two-handed fighters become harder to knock off balance with training.";
                case WeaponClass.DualOneHand:      return "A pair of weapons, one in each hand. Dual wielders gain attack speed with training.";
                case WeaponClass.OneHandAndShield: return "A weapon and a shield. Sword and shield fighters block more often the more they train.";
                default:                           return "A one-handed weapon. One-handed fighters land more critical hits the more they train.";
            }
        }

        // ------------------------------------------------------------------ plug into the BookStage prefab
        static bool PlugIntoBookStage(string path, IndexPageView indexPage, TextPageView textPage, WeaponPicturePageView picturePage,
                                      WeaponInfoPageView infoPage, Material flat, List<WeaponDefinition> weapons)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var stage = root.GetComponent<BookStage>();
                if (stage == null) { Debug.LogWarning("[BookArmoury] No BookStage component on " + path); return false; }

                var so = new SerializedObject(stage);
                SetIfEmpty(so, "indexPagePrefab", indexPage);
                SetIfEmpty(so, "textPagePrefab", textPage);
                SetIfEmpty(so, "weaponPicturePagePrefab", picturePage);
                SetIfEmpty(so, "weaponInfoPagePrefab", infoPage);
                SetIfEmpty(so, "weaponFlatMaterial", flat);

                var list = so.FindProperty("weapons");
                if (list != null && list.arraySize == 0)
                {
                    list.arraySize = weapons.Count;
                    for (int i = 0; i < weapons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void SetIfEmpty(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[BookArmoury] BookStage has no field '" + prop + "'. Did all the scripts get replaced?"); return; }
            if (p.objectReferenceValue == null && value != null) p.objectReferenceValue = value;
        }

        // ------------------------------------------------------------------ the tabs on the GameUI prefab
        static bool AddTabsToGameUi()
        {
            string path = FindPrefab("GameUI");
            if (path == null)
            {
                Debug.LogWarning("[BookArmoury] No GameUI prefab found. Run Tools > Kaiju > Game > Create Game Prefabs first.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { Debug.LogWarning("[BookArmoury] No GameUI component on " + path); return false; }
                if (ui.HasBookTabs()) return true;

                ui.AddBookTabs();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[BookArmoury] Added the Book tabs to " + path);
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ small helpers
        static string FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/" + name + ".prefab")) return p;
            }
            return null;
        }

        static void Assign(SerializedProperty array, Object[] items)
        {
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

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
            t.enableAutoSizing = true;          // long text shrinks to fit instead of spilling off the page
            t.fontSizeMin = 16f;
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
