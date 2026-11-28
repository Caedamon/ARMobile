// File: Assets/Scripts/Editor/CodexSetup.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Fighters;
using Game;
using UnityEditor;
using UnityEngine;

// Tools > Kaiju > Book > Set Up Codex
// 1. Makes one ArchetypeDefinition asset per class in Assets/Data/Archetypes (only the ones that do not exist yet:
//    anything you already edited there is NOT touched).
// 2. Makes Assets/Data/ArchetypeLibrary.asset (the list, in Codex order) and plugs it into the FighterCatalog.
// 3. Plugs the library and the catalog into the BookStage prefab(s) and gives the book its six bookmark colours and the name "Codex".
// Safe to run again. The texts and the modifiers below are only the STARTING values: after this they live in the assets, edit them there.
namespace KaijuTools
{
    public static class CodexSetup
    {
        const string CatalogPath = "Assets/Data/FighterCatalog.asset";
        const string Folder = "Assets/Data/Archetypes";
        const string LibraryPath = "Assets/Data/ArchetypeLibrary.asset";

        sealed class Def
        {
            public string name, modelId, text;
            public ArchetypeClass cls;
            public ArchetypeModifier[] mods;
            public Def(string name, string modelId, ArchetypeClass cls, string text, ArchetypeModifier[] mods)
            { this.name = name; this.modelId = modelId; this.cls = cls; this.text = text; this.mods = mods; }
        }

        // Codex order: the playable classes, then the enemy-only classes, then the large ones.
        // Modifiers are a first draft (points, see ArchetypeRules for what a point is worth): change them in the assets.
        static readonly Def[] Defaults =
        {
            new Def("Paladin", "Paladin_with_Helmet", ArchetypeClass.Playable,
                @"Warriors blessed with a great sense of duty and honour, Paladins dedicate their lives to protecting the innocent and preserving the natural order. Through disciplined cultivation and unwavering conviction, they channel their inner Aura into martial strength. They harbour a natural abhorrence towards the undead and those who defile the cycle of life and death.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Poise, 1), new ArchetypeModifier(ArchetypeModifierKind.Movement, -1) }),
            new Def("Barbarian", "Barbarian", ArchetypeClass.Playable,
                @"Born among peoples who value strength above scholarly pursuits, Barbarians cultivate their bodies through hardship, battle and relentless physical trials. Though often dismissed as uncivilised by Imperial scholars, their instinctive command of Aura makes them formidable warriors. Where others rely upon refined techniques, the Barbarian relies upon overwhelming might.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, -1) }),
            new Def("Ninja", "Ninja", ArchetypeClass.Playable,
                @"Practitioners of ancient martial traditions, Ninjas cultivate precision, agility and control over their own presence. Their techniques favour subtlety over brute strength, allowing them to exploit weaknesses that lesser warriors fail to perceive. A true master is said to end a battle before their opponent realises it has begun.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Dodge, 1), new ArchetypeModifier(ArchetypeModifierKind.AttackSpeed, 1), new ArchetypeModifier(ArchetypeModifierKind.Poise, -1) }),
            new Def("Orc Raider", "OrcRaider", ArchetypeClass.Playable,
                @"Raised within warrior clans where strength earns respect and battle determines standing, Orc Raiders possess a natural affinity for physical cultivation. Their remarkable endurance allows them to withstand injuries that would cripple lesser warriors. Despite their fearsome reputation, many clans uphold strict traditions of loyalty, courage and martial honour.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Movement, -1) }),
            new Def("Rogue", "RogueHooded", ArchetypeClass.Playable,
                @"Unbound by the doctrines of established martial schools, Rogues develop their abilities through experience, necessity and survival. They favour adaptability, cunning and opportunistic strikes over honourable confrontation. While Imperial academies rarely acknowledge their methods, few experienced warriors willingly underestimate a Rogue.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Dodge, 1), new ArchetypeModifier(ArchetypeModifierKind.Movement, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, -1) }),
            new Def("Automaton", "Robot_One", ArchetypeClass.Playable,
                @"Among the many wonders of Imperial spirit engineering are the autonomous constructs commonly known as Automatons. Sustained by artificial cores rather than flesh and blood, these creations possess neither a conventional Mana Core nor Dantian. Some exhibit remarkable individuality, raising uncomfortable questions regarding the true nature of consciousness and the soul.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.AttackSpeed, -1) }),
            new Def("Skeleton Warrior", "Skeleton_Warrior", ArchetypeClass.Playable,
                @"Animated through forbidden manipulation of the forces governing life and death, Skeleton Warriors are remnants of once-living combatants. Some retain fragments of their former memories and martial knowledge, while others exist solely to obey their creator. Particularly remarkable specimens have been observed developing wills of their own.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 2), new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, -1) }),
            new Def("Tiefling", "Tiefling", ArchetypeClass.Playable,
                @"Bearing physical characteristics associated with demonic bloodlines, Tieflings have long been viewed with suspicion by the peoples of the Empire. Their unusual constitution grants them a natural affinity for certain manifestations of Mana. Contrary to popular superstition, their ancestry dictates neither their morality nor the path they choose to cultivate.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Poise, 1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, -1) }),
            new Def("Vampire", "Vampire", ArchetypeClass.Playable,
                @"Through the forbidden cultivation of Blood and Death, certain individuals have transcended the limitations of mortal flesh. Vampires sustain their existence by drawing vitality from living beings. Though blessed with longevity and remarkable regenerative abilities, their continued existence comes at a price that few righteous cultivators are willing to pay.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.AttackSpeed, 1), new ArchetypeModifier(ArchetypeModifierKind.Poise, -1) }),
            new Def("Fallen Paladin", "Fallen_Paladin", ArchetypeClass.EnemyOnly,
                @"Once bound by sacred oaths of duty and honour, Fallen Paladins are warriors who have abandoned or betrayed the principles upon which their strength was founded. Some fell through despair, others through ambition or forbidden cultivation. Though their conviction has been corrupted, the martial discipline of their former lives remains.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Poise, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, -1) }),
            new Def("Caveman", "Caveman", ArchetypeClass.EnemyOnly,
                @"Found among isolated peoples untouched by Imperial civilisation, these primitive warriors practise neither formal martial arts nor structured cultivation. Nevertheless, generations of survival in unforgiving environments have granted them remarkable physical strength and instinct. Imperial scholars continue to debate whether their abilities represent an unconscious form of Aura cultivation.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 2), new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.AttackSpeed, -1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, -1) }),
            new Def("Necromancer", "Necromancer", ArchetypeClass.EnemyOnly,
                @"Scholars and cultivators who pursue the forbidden mysteries of Death, Necromancers seek dominion over the boundary separating the living from the departed. Their experiments have produced both extraordinary discoveries and unspeakable horrors. The Empire regards their practices with considerable suspicion, for mastery over death rarely comes without sacrifice.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Poise, 1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, -1), new ArchetypeModifier(ArchetypeModifierKind.Damage, -1) }),
            new Def("Skeleton Minion", "Skeleton_Minion", ArchetypeClass.EnemyOnly,
                @"The simplest result of necromantic reanimation, Skeleton Minions possess little independent thought and retain few memories of their former existence. Individually weak, they are often raised in considerable numbers. Their creation represents one of the earliest and most infamous applications of Death-aligned Mana.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, -1), new ArchetypeModifier(ArchetypeModifierKind.Damage, -1) }),
            new Def("Skeleton Rogue", "Skeleton_Rogue", ArchetypeClass.EnemyOnly,
                @"Unlike ordinary reanimated remains, Skeleton Rogues retain traces of the agility and cunning possessed in life. Unburdened by exhaustion, fear or the limitations of living flesh, they are particularly dangerous adversaries. Their continued use of complex martial techniques remains a subject of fascination among scholars of the soul.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Dodge, 2), new ArchetypeModifier(ArchetypeModifierKind.Movement, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, -1) }),
            new Def("Werewolf", "Werewolf_Wolf", ArchetypeClass.EnemyOnly,
                @"Afflicted or blessed by an unusual transformation of the body, Werewolves combine mortal intelligence with the instincts and physical might of beasts. The origins of their condition remain disputed, with theories ranging from ancient curses to forbidden bodily cultivation. Those capable of mastering their instincts become exceptionally dangerous warriors.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Movement, 2), new ArchetypeModifier(ArchetypeModifierKind.Poise, -1) }),
            new Def("Black Knight", "BlackKnight", ArchetypeClass.Large,
                @"Encased within formidable armour, Black Knights are warriors whose martial cultivation has elevated them beyond ordinary mortal limitations. Their identities are often concealed, their allegiances uncertain. Some accounts describe them as disgraced champions, while others suggest they are guardians bound by ancient and unbreakable oaths.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Movement, -1) }),
            new Def("Clanker", "Clanker", ArchetypeClass.Large,
                @"Massive constructs born from the ingenuity of Imperial spirit engineers, Clankers were originally designed to perform tasks beyond the physical capabilities of mortal labourers. Their immense strength soon attracted military interest. Though lacking the sophistication of more advanced constructs, their durability makes them formidable engines of destruction.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 2), new ArchetypeModifier(ArchetypeModifierKind.Dodge, -1), new ArchetypeModifier(ArchetypeModifierKind.Movement, -1) }),
            new Def("Frost Golem", "FrostGolem", ArchetypeClass.Large,
                @"Formed through the binding of Ice-aligned Mana into a physical vessel, Frost Golems are manifestations of elemental power given purpose and motion. Their artificial cores sustain bodies of enchanted ice. The knowledge required to create such beings is possessed by relatively few practitioners of elemental magic.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Health, 2), new ArchetypeModifier(ArchetypeModifierKind.Poise, 1), new ArchetypeModifier(ArchetypeModifierKind.AttackSpeed, -1) }),
            new Def("Skeleton Golem", "Skeleton_Golem", ArchetypeClass.Large,
                @"Constructed from the remains of numerous creatures and bound together through advanced necromancy, Skeleton Golems represent a grotesque union of Death magic and construct engineering. Unlike lesser undead, their creation demands considerable mastery and vast quantities of Mana. Their existence is widely regarded as a desecration of the natural order.",
                new[] { new ArchetypeModifier(ArchetypeModifierKind.Damage, 1), new ArchetypeModifier(ArchetypeModifierKind.Health, 1), new ArchetypeModifier(ArchetypeModifierKind.Dodge, -1) }),
        };

        [MenuItem("Tools/Kaiju/Book/Set Up Codex")]
        public static void Run()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FighterCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Set up Codex", "No FighterCatalog found at " + CatalogPath + ".\nRun Tools > Kaiju > Fighters > Setup Fighter Generation first.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Archetypes");

            // 1. the archetype assets
            int made = 0, kept = 0;
            var assets = new List<ArchetypeDefinition>();
            var missingModels = new List<string>();
            foreach (var d in Defaults)
            {
                string path = Folder + "/" + d.modelId + ".asset";
                var a = AssetDatabase.LoadAssetAtPath<ArchetypeDefinition>(path);
                if (a == null)
                {
                    a = ScriptableObject.CreateInstance<ArchetypeDefinition>();
                    a.displayName = d.name;
                    a.classification = d.cls;
                    a.modelIds = new List<string> { d.modelId };
                    a.description = d.text;
                    a.modifiers = new List<ArchetypeModifier>(d.mods);
                    AssetDatabase.CreateAsset(a, path);
                    made++;
                }
                else kept++;
                assets.Add(a);
                if (catalog.Find(d.modelId) == null) missingModels.Add(d.modelId);
            }

            // 2. the library: keeps an existing order, adds what is missing at the end
            var lib = AssetDatabase.LoadAssetAtPath<ArchetypeLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArchetypeLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            if (lib.entries == null) lib.entries = new List<ArchetypeDefinition>();
            lib.entries.RemoveAll(e => e == null);
            foreach (var a in assets) if (!lib.entries.Contains(a)) lib.entries.Add(a);
            EditorUtility.SetDirty(lib);

            // the catalog points at it
            var cso = new SerializedObject(catalog);
            var cp = cso.FindProperty("archetypes");
            if (cp != null) { cp.objectReferenceValue = lib; cso.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(catalog);

            // the pose the portraits stand in: the idle of the Animation Pack (so the models do not stand in a T-pose)
            AnimationClip poseClip = null;
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationPack"))
            {
                var pack = AssetDatabase.LoadAssetAtPath<Combat.AnimationPack>(AssetDatabase.GUIDToAssetPath(guid));
                if (pack == null) continue;
                poseClip = pack.idleOriginal != null ? pack.idleOriginal : pack.idleUnarmed;
                if (poseClip != null) break;
            }

            // 3. the book(s)
            int books = 0;
            var paths = AssetDatabase.FindAssets("BookStage t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var stage = root.GetComponent<BookStage>();
                    if (stage == null) continue;
                    var so = new SerializedObject(stage);
                    SetRef(so, "codexLibrary", lib);
                    SetRef(so, "codexCatalog", catalog);
                    var pose = so.FindProperty("codexPose");
                    if (pose != null && pose.objectReferenceValue == null && poseClip != null) pose.objectReferenceValue = poseClip;
                    var name = so.FindProperty("codexName");
                    if (name != null) name.stringValue = "Codex";
                    var colours = so.FindProperty("tabColors");
                    if (colours != null)
                    {
                        Color[] c =
                        {
                            new Color(0.55f, 0.16f, 0.16f, 1f), new Color(0.78f, 0.52f, 0.12f, 1f), new Color(0.16f, 0.36f, 0.62f, 1f),
                            new Color(0.20f, 0.50f, 0.26f, 1f), new Color(0.10f, 0.45f, 0.48f, 1f), new Color(0.36f, 0.14f, 0.46f, 1f)
                        };
                        colours.arraySize = c.Length;
                        for (int i = 0; i < c.Length; i++) colours.GetArrayElementAtIndex(i).colorValue = c[i];
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    books++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = "Archetypes: " + made + " made, " + kept + " already there (not changed). Library: " + lib.entries.Count + " entries, plugged into the catalog.\n" +
                         (poseClip != null ? "Portrait pose: " + poseClip.name + ".\n" : "No Animation Pack idle found: the portraits keep the prefab's pose (T-pose).\n") +
                         "Book prefabs set up: " + books + (books == 0 ? "  (none found: run Tools > Kaiju > Game > Create Book Prefabs first, then this again)" : "") + ".";
            if (missingModels.Count > 0)
                msg += "\n\nThese model ids are NOT in the FighterCatalog (their pages will have no portrait): " + string.Join(", ", missingModels) +
                       ".\nRun Tools > Kaiju > Fighters > Setup Fighter Generation, or fix the Model Ids in Assets/Data/Archetypes.";
            Debug.Log("[Codex] " + msg.Replace("\n", " "));
            EditorUtility.DisplayDialog("Set up Codex", msg, "OK");
        }

        static void SetRef(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning("[Codex] Field '" + field + "' not found on BookStage.");
        }
    }
}
#endif
