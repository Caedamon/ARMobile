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
        public float nameplateHeight = 2.3f;
        public float nameplateFontSize = 3f;

        [Header("Placement")]
        [Tooltip("Distance between the two fighters at the start (arena-local meters). 0 = use the PlayerSpawn/EnemySpawn markers.")]
        public float fighterSeparation = 1.2f;
        [Tooltip("Height of the fighters' feet above the arena floor (arena-local meters). 0 = standing on the surface.")]
        public float groundOffset = 0f;

        [Header("Combat")]
        [Tooltip("ON = real-time FighterBrain drives the fighters. OFF = the old round-based BattleManager.")]
        public bool realtimeCombat = true;
        public Combat.Moveset unarmedMoveset;
        public Combat.AnimationPack animationPack;
        public bool brainDebugLogs = false;

        [Header("Debug")]
        public bool debugLogs = false;

        public FighterIdentity Player { get; private set; }
        public FighterIdentity Enemy { get; private set; }

        void Awake() { Spawn(); }

        public void Spawn()
        {
            if (catalog == null)
            {
                Debug.LogError("[Fighters] ArenaFighterSpawner has no catalog assigned. Run Tools > Kaiju > Fighters > Setup Fighter Generation.", this);
                return;
            }

            if (FighterSession.PlayerFighter == null)
                FighterSession.PlayerFighter = FighterGenerator.Generate(catalog, FighterSide.Player);
            var playerData = FighterSession.PlayerFighter;
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

            if (debugLogs)
            {
                Debug.Log("[Fighters] PLAYER: " + playerData.Summary());
                Debug.Log("[Fighters] ENEMY:  " + enemyData.Summary());
            }

            // Make sure combat and the HUD see the new fighters.
            var bm = FindFirstObjectByType<Combat.BattleManager>();
            if (bm != null) bm.DiscoverUnits();
            var ui = FindFirstObjectByType<global::UI.BattleUIManager>();
            if (ui != null) ui.Rebind();
        }

        // Arena-local spawn position for a side. Feet always at groundOffset (not the markers' height).
        Vector3 SpawnLocal(FighterSide side, Transform marker)
        {
            if (fighterSeparation > 0f)
            {
                float half = fighterSeparation * 0.5f;
                return new Vector3(0f, groundOffset, side == FighterSide.Player ? -half : half);
            }
            if (marker != null)
            {
                Vector3 l = transform.InverseTransformPoint(marker.position);
                l.y = groundOffset;
                return l;
            }
            return new Vector3(0f, groundOffset, side == FighterSide.Player ? -0.5f : 0.5f);
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
            Quaternion rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;

            var go = Instantiate(prefab, pos, rot, transform);

            var identity = go.GetComponent<FighterIdentity>();
            if (identity == null) identity = go.AddComponent<FighterIdentity>();
            identity.Apply(d, debugLogs);

            if (realtimeCombat)
            {
                if (animationPack != null)
                {
                    var anim = go.GetComponent<Combat.FighterAnimation>();
                    if (anim == null) anim = go.AddComponent<Combat.FighterAnimation>();
                    anim.pack = animationPack;
                    anim.debugLogs = brainDebugLogs;
                }

                var brain = go.GetComponent<Combat.FighterBrain>();
                if (brain == null) brain = go.AddComponent<Combat.FighterBrain>();
                brain.unarmedMoveset = unarmedMoveset;
                brain.debugLogs = brainDebugLogs;
            }

            if (showNameplates)
            {
                var plate = go.AddComponent<FighterNameplate>();
                plate.height = nameplateHeight;
                plate.fontSize = nameplateFontSize;
                plate.Build(d);
            }
            return identity;
        }
    }
}
