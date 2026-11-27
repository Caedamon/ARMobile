#if UNITY_EDITOR
using Game;
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Schedule > Set Up Fight Schedule
//
// Adds the Fight Schedule to your GameUI prefab: a "Fight Schedule" button on the hub and the schedule screen with its three
// cards. The old Death Match / Duel / Group buttons on the hub are switched OFF (not deleted); switch them back on in the
// prefab if you want them. Only creates what is missing, so your edits are safe. It also works without running this
// (the menus add the schedule themselves when the game starts), but then you can't restyle it in the prefab.
namespace KaijuTools
{
    public static class ScheduleSetup
    {
        [MenuItem("Tools/Kaiju/Schedule/Set Up Fight Schedule")]
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
                Debug.LogWarning("[Schedule] No GameUI prefab found. Run Tools > Kaiju > Game > Create Game Prefabs first.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { Debug.LogWarning("[Schedule] No GameUI component on " + path); return; }

                if (ui.HasScheduleUi())
                {
                    Debug.Log("[Schedule] " + path + " already has the fight schedule. Nothing changed.");
                    return;
                }

                ui.AddScheduleUi();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Schedule] Added the fight schedule to " + path + ". The old Death Match / Duel / Group buttons on the hub are switched off (not deleted).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
