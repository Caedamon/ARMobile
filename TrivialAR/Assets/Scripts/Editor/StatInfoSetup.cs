#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Game > Add Stat Info Popup
//
// Adds the stat info popup to your GameUI prefab (tap areas over the stat rows on the roster card, a hint under
// the card, and the popup). Only creates what is missing, so your edits are safe. Open the GameUI prefab afterwards
// to restyle them; the stat descriptions are on the GameUI component ("Stat Info").
namespace KaijuTools
{
    public static class StatInfoSetup
    {
        [MenuItem("Tools/Kaiju/Game/Add Stat Info Popup")]
        public static void Run()
        {
            string path = "Assets/Prefabs/Game/GameUI.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                path = null;
                foreach (var guid in AssetDatabase.FindAssets("GameUI t:Prefab"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith("/GameUI.prefab")) { path = p; break; }
                }
            }
            if (path == null)
            {
                Debug.LogWarning("[StatInfo] No GameUI prefab found. Run Tools > Kaiju > Game > Create Game Prefabs first.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { Debug.LogWarning("[StatInfo] No GameUI component on " + path); return; }

                var so = new SerializedObject(ui);
                bool missing = so.FindProperty("_statPopup").objectReferenceValue == null;
                var taps = so.FindProperty("_statTap");
                if (taps == null || taps.arraySize != 4) missing = true;
                else for (int i = 0; i < 4; i++) if (taps.GetArrayElementAtIndex(i).objectReferenceValue == null) missing = true;

                if (missing)
                {
                    ui.AddStatInfoUi();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[StatInfo] Added the stat info popup to " + path);
                }
                else Debug.Log("[StatInfo] " + path + " already has the stat info popup. Nothing changed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
