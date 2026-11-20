#if UNITY_EDITOR
using Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Game > Create Game Prefabs
//
// Builds the menus and the two stages as REAL prefabs you can open and edit (Assets/Prefabs/Game):
//   GameUI.prefab, RosterStage.prefab, TombstoneStage.prefab  (+ materials in Assets/Materials/Game)
// and plugs them into the GameController prefab. Existing prefabs are NOT touched (so your edits are safe);
// the "overwrite" variant rebuilds them from scratch.
namespace KaijuTools
{
    public static class GamePrefabBuilder
    {
        const string PrefabDir = "Assets/Prefabs/Game";
        const string MatDir = "Assets/Materials/Game";
        const string ControllerPrefab = "Assets/Prefabs/AR/GameController.prefab";

        [MenuItem("Tools/Kaiju/Game/Create Game Prefabs")]
        public static void Create() { Run(false); }

        [MenuItem("Tools/Kaiju/Game/Create Game Prefabs (overwrite - loses your edits)")]
        public static void CreateOverwrite()
        {
            if (!EditorUtility.DisplayDialog("Overwrite game prefabs?",
                "This rebuilds GameUI, RosterStage and TombstoneStage from scratch and throws away any edits you made to them.",
                "Overwrite", "Cancel")) return;
            Run(true);
        }

        static void Run(bool overwrite)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Game");
            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets/Materials", "Game");

            var floorMat = Mat("StageFloor", new Color(0.10f, 0.11f, 0.15f));
            var stoneMat = Mat("TombstoneStone", new Color(0.55f, 0.57f, 0.60f));
            var baseMat = Mat("TombstoneBase", new Color(0.30f, 0.31f, 0.34f));

            var ui = MakeUi(overwrite);
            var roster = MakeRoster(floorMat, overwrite);
            var tomb = MakeTombstone(floorMat, stoneMat, baseMat, overwrite);
            bool assigned = Assign(ui, roster, tomb, floorMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GamePrefabs] Done. Prefabs are in " + PrefabDir + ". Plugged into the GameController: " + (assigned ? "OK" : "NOT DONE (see warnings)"));
        }

        // ------------------------------------------------------------------ GameUI
        static GameUI MakeUi(bool overwrite)
        {
            string path = PrefabDir + "/GameUI.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<GameUI>(path);

            var go = new GameObject("GameUI");
            try
            {
                go.AddComponent<GameUI>().BuildAll();
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return AssetDatabase.LoadAssetAtPath<GameUI>(path);
        }

        // ------------------------------------------------------------------ RosterStage
        static RosterStage MakeRoster(Material floorMat, bool overwrite)
        {
            string path = PrefabDir + "/RosterStage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<RosterStage>(path);

            var root = new GameObject("RosterStage");
            try
            {
                var rs = root.AddComponent<RosterStage>();
                var floor = Floor(root.transform, floorMat);

                var slots = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 0.22f;
                    float z = 0.12f - 0.06f * Mathf.Abs(i - 1);
                    slots[i] = Marker(root.transform, "Slot_" + i, new Vector3(x, 0.01f, z));
                }
                var front = Marker(root.transform, "FrontSlot", new Vector3(0f, 0.01f, -0.14f));
                var hub = Marker(root.transform, "HubSlot", new Vector3(0f, 0.01f, -0.04f));

                var so = new SerializedObject(rs);
                var arr = so.FindProperty("candidateSlots");
                arr.arraySize = 3;
                for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                so.FindProperty("frontSlot").objectReferenceValue = front;
                so.FindProperty("hubSlot").objectReferenceValue = hub;
                so.FindProperty("floorObject").objectReferenceValue = floor;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<RosterStage>(path);
        }

        // ------------------------------------------------------------------ TombstoneStage
        // Double the old size (about 14 cm wide, 28 cm tall with the arch). Text sits higher and auto-shrinks to fit.
        static TombstoneStage MakeTombstone(Material floorMat, Material stoneMat, Material baseMat, bool overwrite)
        {
            string path = PrefabDir + "/TombstoneStage.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return AssetDatabase.LoadAssetAtPath<TombstoneStage>(path);

            var root = new GameObject("TombstoneStage");
            try
            {
                var ts = root.AddComponent<TombstoneStage>();
                Floor(root.transform, floorMat);

                var stone = new GameObject("Stone").transform;
                stone.SetParent(root.transform, false);

                Part(PrimitiveType.Cube, "Base", stone, new Vector3(0f, 0.018f, 0f), new Vector3(0.20f, 0.024f, 0.09f), baseMat);
                Part(PrimitiveType.Cube, "Slab", stone, new Vector3(0f, 0.120f, 0f), new Vector3(0.14f, 0.18f, 0.032f), stoneMat);
                var cap = Part(PrimitiveType.Cylinder, "Cap", stone, new Vector3(0f, 0.210f, 0f), new Vector3(0.14f, 0.016f, 0.14f), stoneMat);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                float z = -0.0175f;   // just in front of the slab's face (the viewer is at -Z)
                var title   = Text(stone, "Title",    "R.I.P.",                                   new Vector3(0f, 0.228f, z), 0.20f,  0.05f);
                var nameTxt = Text(stone, "Name",     "Hrolf Ironjaw",                            new Vector3(0f, 0.180f, z), 0.13f,  0.04f);
                var details = Text(stone, "Details",  "High quality  Lv 2  -  3 wins",            new Vector3(0f, 0.143f, z), 0.085f, 0.03f);
                var killed  = Text(stone, "KilledBy", "Died against Astrid the Unlucky\nin match 4", new Vector3(0f, 0.100f, z), 0.075f, 0.05f);

                var so = new SerializedObject(ts);
                so.FindProperty("stone").objectReferenceValue = stone;
                so.FindProperty("titleText").objectReferenceValue = title;
                so.FindProperty("nameText").objectReferenceValue = nameTxt;
                so.FindProperty("detailsText").objectReferenceValue = details;
                so.FindProperty("killedByText").objectReferenceValue = killed;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<TombstoneStage>(path);
        }

        // ------------------------------------------------------------------ plug into the GameController
        static bool Assign(GameUI ui, RosterStage roster, TombstoneStage tomb, Material mat)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefab) != null)
            {
                var root = PrefabUtility.LoadPrefabContents(ControllerPrefab);
                try
                {
                    var gc = root.GetComponent<GameController>();
                    if (gc == null) { Debug.LogWarning("[GamePrefabs] No GameController on " + ControllerPrefab); return false; }
                    if (root.transform.childCount > 0)
                        Debug.LogWarning("[GamePrefabs] The GameController prefab still has child objects (baked menus). Run Tools > Kaiju > Game > Repair GameController Prefab.");
                    SetRefs(gc, ui, roster, tomb, mat);
                    PrefabUtility.SaveAsPrefabAsset(root, ControllerPrefab);
                    return true;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var sceneGc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
            if (sceneGc == null) { Debug.LogWarning("[GamePrefabs] No GameController found (prefab or open scene)."); return false; }
            SetRefs(sceneGc, ui, roster, tomb, mat);
            EditorUtility.SetDirty(sceneGc);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        static void SetRefs(GameController gc, GameUI ui, RosterStage roster, TombstoneStage tomb, Material mat)
        {
            var so = new SerializedObject(gc);
            so.FindProperty("gameUiPrefab").objectReferenceValue = ui;
            so.FindProperty("rosterStagePrefab").objectReferenceValue = roster;
            so.FindProperty("tombstoneStagePrefab").objectReferenceValue = tomb;
            var m = so.FindProperty("stageMaterial");
            if (m.objectReferenceValue == null) m.objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ small helpers
        static GameObject Floor(Transform parent, Material mat)
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            f.name = "Floor";
            var col = f.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            f.transform.SetParent(parent, false);
            f.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            f.transform.localScale = new Vector3(0.9f, 0.003f, 0.9f);
            if (mat != null) f.GetComponent<Renderer>().sharedMaterial = mat;
            return f;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            var col = p.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat;
            return p;
        }

        static TextMeshPro Text(Transform parent, string name, string text, Vector3 pos, float maxFont, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.12f, 0.12f, 0.14f);
            t.enableAutoSizing = true;      // shrinks long names to fit the stone
            t.fontSizeMin = 0.02f;
            t.fontSizeMax = maxFont;
            t.fontSize = maxFont;
            t.rectTransform.sizeDelta = new Vector2(0.125f, height);
            return t;
        }

        static Transform Marker(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        static Material Mat(string name, Color c)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[GamePrefabs] URP Lit shader not found."); return null; }
            m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
