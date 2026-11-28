using System.Collections.Generic;
using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// Put on the arena root. When the arena appears it spawns the player's fighter and a random enemy
    /// at the two spawn points, facing each other. Spawns in Awake so the fighters already exist when
    /// ARPlaceArena asks BattleManager and the UI to look for units right after instantiating the arena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaFighterSpawner : MonoBehaviour
    {
        public FighterCatalog catalog;
        public Transform playerSpawn;
        public Transform enemySpawn;

        [Header("Nameplates")]
        public bool showNameplates = true;
        [Tooltip("Empty space between the top of the head and the bottom of the name label (fighter-local units). Bigger = higher.")]
        public float nameplateGap = 0.15f;
        [Tooltip("Only used if the head can't be found.")]
        public float nameplateHeight = 2.3f;
        public float nameplateFontSize = 3f;

        [Header("Placement")]
        [Tooltip("Distance between the two fighters at the start (arena-local meters). 0 = use the PlayerSpawn/EnemySpawn markers.")]
        public float fighterSeparation = 1.2f;
        [Tooltip("ON = the fighters start WEST and EAST of each other (left and right as you look at the table), so you can watch from where you stand. OFF = north/south (near and far).")]
        public bool sideBySide = true;
        [Tooltip("With sideBySide: the player starts on the left (west) side. OFF = the right side.")]
        public bool playerOnWest = true;
        [Tooltip("Height of the fighters' feet above the arena floor (arena-local meters). 0 = standing on the surface.")]
        public float groundOffset = 0f;

        [Header("Group fights")]
        [Tooltip("Distance between neighbours in a team's line (arena-local metres) in group fights.")]
        public float groupSpacing = 0.28f;
        [Tooltip("Group fights get at least this many weapons on the floor per fighter.")]
        public float weaponsPerFighter = 1f;

        [Header("Combat")]
        [Tooltip("ON = real-time FighterBrain drives the fighters. OFF = the old round-based BattleManager.")]
        public bool realtimeCombat = true;
        public Combat.Moveset unarmedMoveset;
        [Tooltip("Shield, dodge and per-style balance numbers. Made by Tools > Kaiju > Combat > Create Combat Tuning. Empty = built-in defaults.")]
        public Combat.CombatTuning combatTuning;
        [Tooltip("Swing, hit, block, dodge, pickup and voice sounds. Made by Tools > Kaiju > Audio > Set Up Audio. Empty = silent fights.")]
        public Combat.CombatSounds combatSounds;
        public Combat.AnimationPack animationPack;
        public bool brainDebugLogs = false;

        [Header("AI (how the fighters think)")]
        [Tooltip("Enemies of this quality or better are Smart (they circle, punish, defend sensibly). Lower ones rush: grab a weapon, charge.")]
        public FighterTier smartFromTier = FighterTier.HighQuality;
        [Tooltip("A Normal-quality enemy is Smart with this chance (so some regular foes surprise you). Pathetic ones are always Basic.")]
        [Range(0f, 1f)] public float normalSmartChance = 0.15f;
        [Tooltip("Boss fights: the boss uses the Boss AI (Smart + phases).")]
        public bool bossUsesBossAi = true;
        [Tooltip("The AI of your own hero and of your allies. Keep Basic unless you want your side smarter too.")]
        public Combat.AiTier heroAi = Combat.AiTier.Basic;
        public Combat.AiTier allyAi = Combat.AiTier.Basic;
        [Tooltip("All the numbers behind Smart and Boss. Tune here.")]
        public Combat.AiTuning aiTuning = new Combat.AiTuning();

        [Header("Arrival")]
        [Tooltip("Fighters fall out of a small portal above them when a fight starts (needs the 'Spawn_Air' clip: Tools > Kaiju > Arrival > Set Up Arrival).")]
        public bool arrivalPortals = true;
        public Color arrivalPortalColor = new Color(0.25f, 0.85f, 1f, 1f);

        [Header("Debug")]
        public bool debugLogs = false;

        /// <summary>The player's hero.</summary>
        public FighterIdentity Player { get; private set; }
        /// <summary>The (first) opponent. In group fights see Foes.</summary>
        public FighterIdentity Enemy { get; private set; }
        /// <summary>The hero's teammates (empty in a 1 against 1 fight).</summary>
        public List<FighterIdentity> Allies { get; private set; }
        /// <summary>Everyone the hero has to beat (one fighter in a 1 against 1 fight).</summary>
        public List<FighterIdentity> Foes { get; private set; }
        public bool IsGroup { get; private set; }

        void Awake() { Spawn(); }

        public void Spawn()
        {
            if (catalog == null)
            {
                Debug.LogError("[Fighters] ArenaFighterSpawner has no catalog assigned. Run Tools > Kaiju > Fighters > Setup Fighter Generation.", this);
                return;
            }

            Allies = new List<FighterIdentity>();
            Foes = new List<FighterIdentity>();

            if (FighterSession.PlayerFighter == null)
                FighterSession.PlayerFighter = FighterGenerator.Generate(catalog, FighterSide.Player);
            var playerData = FighterSession.PlayerFighter;

            IsGroup = FighterSession.Allies.Count > 0 || FighterSession.Foes.Count > 0;
            if (IsGroup)
            {
                SpawnGroup(playerData);
                return;
            }

            var enemyData = FighterSession.EnemyFighter;
            if (enemyData == null)
            {
                // Keep pace with the player: level band from ProgressionRules, and better tiers get more common as the player levels.
                var rng = new System.Random();
                enemyData = FighterGenerator.Generate(catalog, FighterSide.Enemy, rng, playerData.level);
                ProgressionRules.ScaleToLevel(enemyData, ProgressionRules.RollEnemyLevel(playerData.level, rng), rng);
            }

            Player = SpawnOne(playerData, FighterSide.Player, playerSpawn, enemySpawn);
            Enemy  = SpawnOne(enemyData,  FighterSide.Enemy,  enemySpawn,  playerSpawn);
            if (Enemy != null) Foes.Add(Enemy);

            if (debugLogs)
            {
                Debug.Log("[Fighters] PLAYER: " + playerData.Summary());
                Debug.Log("[Fighters] ENEMY:  " + enemyData.Summary());
            }

            TellCombatAndHud();
        }

        // Make sure combat and the HUD see the new fighters.
        void TellCombatAndHud()
        {
            var bm = FindFirstObjectByType<Combat.BattleManager>();
            if (bm != null) bm.DiscoverUnits();
            var ui = FindFirstObjectByType<global::UI.BattleUIManager>();
            if (ui != null) ui.Rebind();
        }

        // ------------------------------------------------------------------ group fights
        // Two teams facing each other in lines (the hero's team on one side), or - Last Hero Standing - everyone on a ring.
        void SpawnGroup(FighterData heroData)
        {
            var allies = FighterSession.Allies;
            var foes = FighterSession.Foes;
            bool ring = FighterSession.FreeForAll;
            int teamA = 1 + allies.Count;
            int teamB = foes.Count;
            int total = teamA + teamB;

            // more fighters need more weapons on the floor (the WeaponSpawner on the arena makes them when the arena starts)
            var ws = GetComponentInChildren<Items.WeaponSpawner>(true);
            if (ws != null) ws.weaponCount = Mathf.Max(ws.weaponCount, Mathf.CeilToInt(total * weaponsPerFighter) + 1);

            float half = Mathf.Max(0.3f, (fighterSeparation > 0f ? fighterSeparation : 1.0f) * 0.5f);
            float heroSign = playerOnWest ? -1f : 1f;

            var pos = new List<Vector3>();      // arena-local, index 0 = hero, then allies, then foes
            var look = new List<Vector3>();     // arena-local direction each one faces

            if (ring)
            {
                // everybody on a circle, the hero at the "player" side, all facing the middle
                float heroAngle = heroSign < 0f ? 180f : 0f;
                for (int i = 0; i < total; i++)
                {
                    float a = (heroAngle + i * 360f / total) * Mathf.Deg2Rad;
                    float c = Mathf.Cos(a) * half, s = Mathf.Sin(a) * half;
                    Vector3 p = sideBySide ? new Vector3(c, groundOffset, s) : new Vector3(s, groundOffset, c);
                    pos.Add(p);
                    look.Add(new Vector3(-p.x, 0f, -p.z));
                }
            }
            else
            {
                AddLine(pos, look, teamA, heroSign * half);   // slot 0 of the line is the middle one: the hero
                AddLine(pos, look, teamB, -heroSign * half);
            }

            // fighters: hero, allies (player side models), foes (enemy side models)
            int k = 0;
            Player = SpawnAt(heroData, FighterSide.Player, pos[k], look[k], ring);
            k++;
            for (int i = 0; i < allies.Count; i++, k++)
            {
                var id = SpawnAt(allies[i], FighterSide.Player, pos[k], look[k], ring);
                if (id != null) Allies.Add(id);
            }
            for (int i = 0; i < foes.Count; i++, k++)
            {
                var id = SpawnAt(foes[i], FighterSide.Enemy, pos[k], look[k], ring);
                if (id != null) Foes.Add(id);
            }
            Enemy = Foes.Count > 0 ? Foes[0] : null;

            if (debugLogs)
            {
                Debug.Log("[Fighters] GROUP: hero + " + Allies.Count + " allies vs " + Foes.Count + " foes" + (ring ? "  (free for all)" : ""));
                Debug.Log("[Fighters] HERO: " + heroData.Summary());
            }

            TellCombatAndHud();
        }

        // One team's line: n fighters side by side on the team's side (x = sideX), middle first so the hero (index 0 of team A) is in the middle.
        void AddLine(List<Vector3> pos, List<Vector3> look, int n, float sideX)
        {
            // slot order by distance from the middle: 0, +1, -1, +2, -2 ...
            for (int i = 0; i < n; i++)
            {
                int step = (i + 1) / 2;
                float offset = (i % 2 == 1 ? 1f : -1f) * step * groupSpacing;
                Vector3 p = sideBySide ? new Vector3(sideX, groundOffset, offset) : new Vector3(offset, groundOffset, sideX);
                pos.Add(p);
                look.Add(sideBySide ? new Vector3(-Mathf.Sign(sideX), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(sideX)));
            }
        }

        // Arena-local spawn position for a side. Feet always at groundOffset (not the markers' height).
        Vector3 SpawnLocal(FighterSide side, Transform marker)
        {
            bool isPlayer = side == FighterSide.Player;
            // -1 = west / near, +1 = east / far
            float sign = sideBySide ? (isPlayer == playerOnWest ? -1f : 1f) : (isPlayer ? -1f : 1f);

            if (fighterSeparation > 0f)
            {
                float half = fighterSeparation * 0.5f;
                return sideBySide ? new Vector3(sign * half, groundOffset, 0f) : new Vector3(0f, groundOffset, sign * half);
            }
            if (marker != null)
            {
                Vector3 l = transform.InverseTransformPoint(marker.position);
                l.y = groundOffset;
                if (sideBySide) l = new Vector3(sign * Mathf.Max(Mathf.Abs(l.x), Mathf.Abs(l.z)), groundOffset, 0f);
                return l;
            }
            return sideBySide ? new Vector3(sign * 0.5f, groundOffset, 0f) : new Vector3(0f, groundOffset, sign * 0.5f);
        }

        FighterIdentity SpawnOne(FighterData d, FighterSide side, Transform at, Transform opponentSpawn)
        {
            var model = catalog.Find(d.modelId);
            var prefab = model == null ? null : (side == FighterSide.Player ? model.playerPrefab : model.enemyPrefab);
            if (prefab == null)
            {
                Debug.LogError("[Fighters] No " + side + " prefab for model '" + d.modelId + "' in the catalog.", this);
                return null;
            }

            Vector3 pos = transform.TransformPoint(SpawnLocal(side, at));
            Vector3 other = transform.TransformPoint(SpawnLocal(side == FighterSide.Player ? FighterSide.Enemy : FighterSide.Player, opponentSpawn));

            Vector3 dir = other - pos;
            dir.y = 0f;
            return Build(d, side, prefab, pos, dir, false);
        }

        // A fighter at an arena-local position, facing an arena-local direction (used by group fights).
        FighterIdentity SpawnAt(FighterData d, FighterSide side, Vector3 localPos, Vector3 localLook, bool freeForAll)
        {
            var model = catalog.Find(d.modelId);
            var prefab = model == null ? null : (side == FighterSide.Player ? model.playerPrefab : model.enemyPrefab);
            if (prefab == null)
            {
                Debug.LogError("[Fighters] No " + side + " prefab for model '" + d.modelId + "' in the catalog.", this);
                return null;
            }
            Vector3 pos = transform.TransformPoint(localPos);
            Vector3 dir = transform.TransformDirection(localLook);
            dir.y = 0f;
            return Build(d, side, prefab, pos, dir, freeForAll);
        }

        // Which way of thinking this fighter gets.
        Combat.AiTier PickAiTier(FighterData d, FighterSide side)
        {
            if (side == FighterSide.Player)
                return d == FighterSession.PlayerFighter ? heroAi : allyAi;

            if (bossUsesBossAi && FighterSession.Mode == FightMode.Boss && d.displayName != null && d.displayName.StartsWith("Boss "))
                return Combat.AiTier.Boss;

            if (d.tier >= smartFromTier && d.tier != FighterTier.Pathetic) return Combat.AiTier.Smart;
            if (d.tier == FighterTier.Normal && Random.value < normalSmartChance) return Combat.AiTier.Smart;
            return Combat.AiTier.Basic;
        }

        FighterIdentity Build(FighterData d, FighterSide side, GameObject prefab, Vector3 pos, Vector3 dir, bool freeForAll)
        {
            Quaternion rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
            var go = Instantiate(prefab, pos, rot, transform);

            if (freeForAll)
            {
                var ai = go.GetComponent<Combat.MeleeAI>();
                if (ai != null) ai.freeForAll = true;   // Last Hero Standing: an enemy of everyone
            }

            var identity = go.GetComponent<FighterIdentity>();
            if (identity == null) identity = go.AddComponent<FighterIdentity>();
            identity.Apply(d, debugLogs, catalog != null ? catalog.ArchetypeOf(d.modelId) : null);

            if (realtimeCombat)
            {
                if (animationPack != null)
                {
                    var anim = go.GetComponent<Combat.FighterAnimation>();
                    if (anim == null) anim = go.AddComponent<Combat.FighterAnimation>();
                    anim.pack = animationPack;
                    anim.debugLogs = brainDebugLogs;

                    if (arrivalPortals)
                    {
                        var arrival = go.GetComponent<Combat.FighterArrival>();
                        if (arrival == null) arrival = go.AddComponent<Combat.FighterArrival>();
                        arrival.portalColor = arrivalPortalColor;
                    }
                }

                var brain = go.GetComponent<Combat.FighterBrain>();
                if (brain == null) brain = go.AddComponent<Combat.FighterBrain>();
                brain.unarmedMoveset = unarmedMoveset;
                brain.tuningAsset = combatTuning;
                brain.sounds = combatSounds;
                brain.debugLogs = brainDebugLogs;
                brain.aiTuning = aiTuning;
                brain.aiTier = PickAiTier(d, side);
            }

            if (showNameplates)
            {
                var plate = go.AddComponent<FighterNameplate>();
                plate.height = nameplateHeight;
                plate.gapAboveHead = nameplateGap;
                plate.fontSize = nameplateFontSize;
                plate.Build(d);
            }
            return identity;
        }
    }
}
