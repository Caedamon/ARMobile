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
            var enemyData = FighterSession.EnemyFighter ?? FighterGenerator.Generate(catalog, FighterSide.Enemy);
            var playerData = FighterSession.PlayerFighter;

            Player = SpawnOne(playerData, FighterSide.Player, playerSpawn, enemySpawn);
            Enemy  = SpawnOne(enemyData,  FighterSide.Enemy,  enemySpawn,  playerSpawn);

            Debug.Log("[Fighters] PLAYER: " + playerData.Summary());
            Debug.Log("[Fighters] ENEMY:  " + enemyData.Summary());

            // Make sure combat and the HUD see the new fighters.
            var bm = FindFirstObjectByType<Combat.BattleManager>();
            if (bm != null) bm.DiscoverUnits();
            var ui = FindFirstObjectByType<global::UI.BattleUIManager>();
            if (ui != null) ui.Rebind();
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

            float z = side == FighterSide.Player ? -0.5f : 0.5f;
            Vector3 pos = at != null ? at.position : transform.TransformPoint(new Vector3(0f, 0.05f, z));
            Vector3 other = opponentSpawn != null ? opponentSpawn.position : transform.TransformPoint(new Vector3(0f, 0.05f, -z));

            Vector3 dir = other - pos;
            dir.y = 0f;
            Quaternion rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;

            var go = Instantiate(prefab, pos, rot, transform);

            var identity = go.GetComponent<FighterIdentity>();
            if (identity == null) identity = go.AddComponent<FighterIdentity>();
            identity.Apply(d);

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
