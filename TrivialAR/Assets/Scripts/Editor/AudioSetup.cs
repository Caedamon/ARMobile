#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Combat;
using Fighters;
using Game;
using UnityEditor;
using UnityEngine;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Audio > Set Up Audio
//
// One click, safe to run again (it only fills things that are empty, and never replaces an asset or an assignment you made):
//  1. Music: finds GameTest_loop.ogg and sets it to "Compressed In Memory" (best for seamless looping).
//  2. Creates Assets/Data/CombatSounds.asset and fills each slot with the matching clips (found by file name, anywhere in Assets).
//  3. Plugs that asset into every ArenaFighterSpawner in Assets/Prefabs ("Combat Sounds").
//  4. Adds a GameAudio component to the GameController prefab and gives it the music clip + the slider preview sound.
//  5. Adds the Settings screen (Music / Effects sliders) and the Settings buttons to the GameUI prefab.
namespace KaijuTools
{
    public static class AudioSetup
    {
        const string BankPath = "Assets/Data/CombatSounds.asset";
        const string ControllerPrefab = "Assets/Prefabs/AR/GameController.prefab";

        [MenuItem("Tools/Kaiju/Audio/Set Up Audio")]
        public static void Run()
        {
            var report = new List<string>();
            var music = SetUpMusic(report);
            var bank = SetUpBank(report);
            PlugBank(bank, report);
            SetUpController(music, report);
            SetUpSettingsUi(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (bank != null) { Selection.activeObject = bank; EditorGUIUtility.PingObject(bank); }
            Debug.Log("[Audio] Done:\n - " + string.Join("\n - ", report.ToArray()));
        }

        // ------------------------------------------------------------------ finding clips
        static AudioClip Find(string fileName)
        {
            AudioClip best = null;
            foreach (var guid in AssetDatabase.FindAssets(fileName + " t:AudioClip"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(p), fileName, System.StringComparison.OrdinalIgnoreCase)) continue;
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (c == null) continue;
                if (best == null || p.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase)) best = c;   // prefer the .ogg of a pair
            }
            return best;
        }

        static AudioClip[] FindAll(List<string> report, params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names)
            {
                var c = Find(n);
                if (c != null) list.Add(c);
                else report.Add("could not find a clip named '" + n + "' (skipped)");
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------ 1. music
        static AudioClip SetUpMusic(List<string> report)
        {
            var clip = Find("GameTest_loop");
            if (clip == null) { report.Add("music: GameTest_loop not found in Assets (import Audio/Music/GameTest_loop.ogg first)"); return null; }

            string path = AssetDatabase.GetAssetPath(clip);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp != null)
            {
                var s = imp.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.CompressedInMemory)
                {
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                    report.Add("music: " + path + " set to Compressed In Memory (gapless looping)");
                }
                else report.Add("music: " + path + " found");
            }
            return clip;
        }

        // ------------------------------------------------------------------ 2. the sound bank
        static CombatSounds SetUpBank(List<string> report)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            var bank = AssetDatabase.LoadAssetAtPath<CombatSounds>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<CombatSounds>();
                AssetDatabase.CreateAsset(bank, BankPath);
                report.Add("created " + BankPath);
            }

            Fill(bank.swing, report, "Whoosh 1-1");
            Fill(bank.dodge, report, "Whoosh 4-1");
            Fill(bank.weaponPickup, report, "Draw Weapon Metal 1-1");
            Fill(bank.hitUnarmed, report, "Punch 2-1");
            Fill(bank.hitWeapon, report, "Hit Generic 2-1", "Hit Generic 5-1");
            Fill(bank.hitHeavy, report, "Stab 4-1");
            Fill(bank.blocked, report, "Shield Metal Impact 1-3", "Shield Wood Impact 2-1");
            Fill(bank.attackGrunt, report, "Player_Attack_Grunt_01", "Player_Attack_Grunt_02", "Player_Attack_Grunt_03");
            Fill(bank.hurtGrunt, report, "Player_Hit_Grunt_01", "Player_Hit_Grunt_02", "Player_Hit_Grunt_03");
            Fill(bank.death, report, "Player_Hit_Death_01", "Player_Hit_Death_02", "Player_Hit_Death_03");

            if (bank.spare == null || bank.spare.Length == 0)
                bank.spare = FindAll(report, "Blacksmithing 1-1", "Player_Breathing_Heavy", "Player_Jump_1", "Player_Land_01");

            EditorUtility.SetDirty(bank);
            report.Add("sound bank: " + BankPath + " (edit it to change clips and volumes)");
            return bank;
        }

        static void Fill(CombatSounds.Slot slot, List<string> report, params string[] names)
        {
            if (slot.clips != null && slot.clips.Length > 0) return;   // keep what you set
            slot.clips = FindAll(report, names);
        }

        // ------------------------------------------------------------------ 3. spawners
        static void PlugBank(CombatSounds bank, List<string> report)
        {
            if (bank == null) return;
            int plugged = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var probe = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (probe == null || probe.GetComponentInChildren<ArenaFighterSpawner>(true) == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var sp in root.GetComponentsInChildren<ArenaFighterSpawner>(true))
                    {
                        var so = new SerializedObject(sp);
                        var p = so.FindProperty("combatSounds");
                        if (p == null || p.objectReferenceValue != null) continue;
                        p.objectReferenceValue = bank;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); plugged++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var sp in Object.FindObjectsByType<ArenaFighterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (EditorUtility.IsPersistent(sp)) continue;
                var so = new SerializedObject(sp);
                var p = so.FindProperty("combatSounds");
                if (p == null || p.objectReferenceValue != null) continue;
                p.objectReferenceValue = bank;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sp);
            }
            report.Add("arena prefabs newly given the sound bank: " + plugged);
        }

        // ------------------------------------------------------------------ 4. GameAudio on the controller
        static void SetUpController(AudioClip music, List<string> report)
        {
            var preview = Find("Hit Generic 2-1");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPrefab) == null)
            {
                var sceneGc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
                if (sceneGc == null) { report.Add("GameController prefab not found at " + ControllerPrefab + " and none in the open scene: add a GameAudio component to it yourself"); return; }
                var ga = sceneGc.GetComponent<GameAudio>();
                if (ga == null) ga = sceneGc.gameObject.AddComponent<GameAudio>();
                Assign(ga, music, preview);
                EditorUtility.SetDirty(ga);
                report.Add("GameAudio added to the GameController in the open scene (save the scene)");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(ControllerPrefab);
            try
            {
                var ga = root.GetComponentInChildren<GameAudio>(true);
                bool added = false;
                if (ga == null) { ga = root.AddComponent<GameAudio>(); added = true; }
                bool filled = Assign(ga, music, preview);
                if (added || filled) PrefabUtility.SaveAsPrefabAsset(root, ControllerPrefab);
                report.Add(added ? "GameAudio added to " + ControllerPrefab : "GameAudio already on " + ControllerPrefab + (filled ? " (filled empty slots)" : ""));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool Assign(GameAudio ga, AudioClip music, AudioClip preview)
        {
            bool changed = false;
            var so = new SerializedObject(ga);
            var m = so.FindProperty("musicClip");
            if (m.objectReferenceValue == null && music != null) { m.objectReferenceValue = music; changed = true; }
            var p = so.FindProperty("effectsPreview");
            if (p.objectReferenceValue == null && preview != null) { p.objectReferenceValue = preview; changed = true; }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ------------------------------------------------------------------ 5. Settings screen
        static void SetUpSettingsUi(List<string> report)
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
            if (path == null) { report.Add("GameUI prefab not found: run Tools > Kaiju > Game > Create Game Prefabs first, then this again"); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (ui == null) { report.Add("no GameUI component on " + path); return; }
                if (ui.HasSettingsUi()) { report.Add("Settings screen already in " + path); return; }
                ui.AddSettingsUi();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add("Settings screen + buttons added to " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
