#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEditor;
using UnityEngine;
using Weaponary;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Combat > Add Missing Attack Animations
//
// Looks through every Rig_Medium animation file for attack clips that none of your Movesets use yet, and adds them
// to the matching moveset (by name):
//     Melee_Unarmed_Attack...    -> Unarmed
//     Melee_1H_Attack...         -> OneHand and OneHandShield   (the "Jump" ones are skipped on purpose, like the first setup did)
//     Melee_2H_Attack...         -> TwoHand
//     Melee_Dualwield_Attack...  -> DualWield
// Anything else with "Attack" in its name is only LISTED in the Console, so you can tell me where it belongs.
// Nothing is removed or changed, only added. The timings (hitAt etc.) are guesses from the clip name: tune them in the Moveset assets.
// Safe to run again.
namespace KaijuTools
{
    public static class AttackSetup
    {
        const string Folder = "Assets/Data/Movesets";

        static readonly Dictionary<string, float> SetSpeed = new Dictionary<string, float>
        {
            { "Unarmed", 1.15f }, { "OneHand", 1.00f }, { "OneHandShield", 0.95f }, { "TwoHand", 0.85f }, { "DualWield", 1.20f },
        };

        [MenuItem("Tools/Kaiju/Combat/Add Missing Attack Animations")]
        public static void Run()
        {
            // every attack-looking clip in every medium-rig animation file
            var byName = new Dictionary<string, AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("Rig_Medium t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__") || !c.name.Contains("Attack")) continue;
                    if (!byName.ContainsKey(c.name)) byName[c.name] = c;
                }
            }
            if (byName.Count == 0) { Debug.LogWarning("[Attacks] No attack clips found in any Rig_Medium file."); return; }

            // what each moveset already has
            var sets = new Dictionary<string, Moveset>();
            var have = new Dictionary<string, HashSet<string>>();
            foreach (var key in SetSpeed.Keys)
            {
                var m = AssetDatabase.LoadAssetAtPath<Moveset>(Folder + "/" + key + ".asset");
                if (m == null) continue;
                sets[key] = m;
                have[key] = new HashSet<string>(m.attacks.Where(a => a != null && a.clip != null).Select(a => a.clip.name));
            }
            if (sets.Count == 0) { Debug.LogWarning("[Attacks] No movesets found in " + Folder + ". Run Tools > Kaiju > Combat > Create Starter Movesets first."); return; }

            var added = new List<string>();
            var skipped = new List<string>();
            var unmapped = new List<string>();

            foreach (var kv in byName.OrderBy(k => k.Key))
            {
                string n = kv.Key;
                if (n == "Melee_Block_Attack") continue;   // the shield bash, added by the first setup

                List<string> targets;
                if (n.StartsWith("Melee_Unarmed_Attack")) targets = new List<string> { "Unarmed" };
                else if (n.StartsWith("Melee_1H_Attack"))
                {
                    if (n.Contains("Jump")) { skipped.Add(n); continue; }
                    targets = new List<string> { "OneHand", "OneHandShield" };
                }
                else if (n.StartsWith("Melee_2H_Attack")) targets = new List<string> { "TwoHand" };
                else if (n.StartsWith("Melee_Dualwield_Attack")) targets = new List<string> { "DualWield" };
                else { unmapped.Add(n); continue; }

                foreach (var t in targets)
                {
                    if (!sets.ContainsKey(t) || have[t].Contains(n)) continue;
                    sets[t].attacks.Add(DefaultMove(kv.Value, SetSpeed[t]));
                    have[t].Add(n);
                    added.Add(n + "  ->  " + t);
                }
            }

            foreach (var m in sets.Values) EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();

            Debug.Log("[Attacks] Added " + added.Count + " attack(s):\n - " + (added.Count == 0 ? "(nothing new: every attack clip is already in a moveset)" : string.Join("\n - ", added.ToArray())) +
                      (skipped.Count > 0 ? "\nSkipped on purpose (jump attacks): " + string.Join(", ", skipped.ToArray()) : "") +
                      (unmapped.Count > 0 ? "\nNot added, I don't know where these belong (tell me): " + string.Join(", ", unmapped.ToArray()) : "") +
                      "\nTune hitAt / damage on the new entries in " + Folder + ".");
        }

        // Starting guesses from the clip name, the same ones the first setup used.
        static AttackMove DefaultMove(AnimationClip c, float speed)
        {
            string n = c.name;
            var m = new AttackMove { label = Pretty(n), clip = c, speed = speed };

            if (n.Contains("Stab"))        { m.hitAt = 0.40f; m.lungeModelMeters = 0.40f; m.knockbackModelMeters = 0.25f; }
            else if (n.Contains("Chop"))   { m.hitAt = 0.50f; m.damageMul = 1.15f; m.knockbackModelMeters = 0.40f; m.stagger = 1.2f; }
            else if (n.Contains("Spin"))   { m.hitAt = 0.50f; m.damageMul = 1.30f; m.reachMul = 1.15f; m.knockbackModelMeters = 0.60f; m.stagger = 1.3f; m.speed = speed * 0.9f; m.weight = 0.6f; }
            else if (n.Contains("Kick"))   { m.hitAt = 0.50f; m.damageMul = 1.10f; m.knockbackModelMeters = 0.50f; }
            else if (n.Contains("Punch"))  { m.hitAt = 0.40f; m.damageMul = 0.90f; m.lungeModelMeters = 0.20f; m.knockbackModelMeters = 0.25f; }
            else                           { m.hitAt = 0.45f; }
            return m;
        }

        static string Pretty(string clipName)
        {
            return clipName.Replace("Melee_", "").Replace("_Attack", "").Replace("_", " ") + "!";
        }
    }
}
#endif
