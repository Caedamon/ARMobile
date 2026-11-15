using System.Collections;
using System.Collections.Generic;
using AR;
using Core;
using Fighters;
using UnityEngine;
using Weaponary;

namespace Game
{
    public enum GameState { Title, Placing, Roster, Hub, Fight, Result }

    /// <summary>
    /// The game loop:  Title -> (place on table) -> Roster (pick 1 of 3) -> Hub -> Death Match -> Result -> Hub / Roster.
    /// Put on one object in the scene (Tools > Kaiju > Game > Setup Game Flow does this).
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Assigned by the setup tool")]
        public FighterCatalog catalog;
        public GameObject arenaPrefab;
        public List<WeaponDefinition> showcaseWeapons = new List<WeaponDefinition>();

        [Header("Tuning")]
        public string gameTitle = "ARENA";
        public int rosterSize = 3;
        [Tooltip("Seconds to let the death animation play before showing the result.")]
        public float fightEndDelay = 2.5f;

        public GameState State { get { return _state; } }

        GameState _state;
        GameUI _ui;
        ARStagePlacer _placer;
        RosterStage _roster;
        Canvas _hudCanvas;                 // the scene's existing fight HUD (portraits + health bars)
        SaveData _save;

        List<FighterData> _candidates = new List<FighterData>();
        int _selectedCandidate = -1;

        ArenaFighterSpawner _arena;
        bool _fightEnding;
        bool _lastResultWon;

        // ------------------------------------------------------------------ setup
        void Awake()
        {
            _placer = GetComponent<ARStagePlacer>();
            if (_placer == null) _placer = gameObject.AddComponent<ARStagePlacer>();

            _ui = gameObject.AddComponent<GameUI>();

            // The old one-shot placement script would fight with ours.
            var oldPlacer = FindFirstObjectByType<ARPlaceArena>();
            if (oldPlacer != null) oldPlacer.enabled = false;

            // The fight HUD lives on the scene's root Canvas (BattleUIManager sits on it).
            var battleUi = FindFirstObjectByType<global::UI.BattleUIManager>();
            if (battleUi != null) _hudCanvas = battleUi.GetComponentInParent<Canvas>();
        }

        void Start()
        {
            if (catalog == null || arenaPrefab == null)
                Debug.LogError("[Game] GameController is missing its catalog or arena prefab. Run Tools > Kaiju > Game > Setup Game Flow.", this);

            _ui.ContinueClicked += OnContinue;
            _ui.NewGameClicked += OnNewGame;
            _ui.PlaceInFrontClicked += OnPlaceInFront;
            _ui.PickClicked += OnPick;
            _ui.BackClicked += OnBack;
            _ui.DeathMatchClicked += OnDeathMatch;
            _ui.ResultContinueClicked += OnResultContinue;

            EnterTitle();
        }

        void OnDestroy()
        {
            if (_ui == null) return;
            _ui.ContinueClicked -= OnContinue;
            _ui.NewGameClicked -= OnNewGame;
            _ui.PlaceInFrontClicked -= OnPlaceInFront;
            _ui.PickClicked -= OnPick;
            _ui.BackClicked -= OnBack;
            _ui.DeathMatchClicked -= OnDeathMatch;
            _ui.ResultContinueClicked -= OnResultContinue;
        }

        void SetHud(bool on)
        {
            if (_hudCanvas != null) _hudCanvas.enabled = on;
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            switch (_state)
            {
                case GameState.Placing:
                    if (_placer.PollTapPlacement()) AfterPlaced();
                    break;
                case GameState.Roster:
                    HandleRosterTap();
                    break;
                case GameState.Fight:
                    MonitorFight();
                    break;
            }
        }

        // ------------------------------------------------------------------ title
        void EnterTitle()
        {
            _state = GameState.Title;
            SetHud(false);
            _placer.ClearStage();

            _save = SaveSystem.Load();
            bool canContinue = _save != null && _save.hasFighter && _save.fighter != null;
            string label = canContinue ? "Continue  (" + _save.fighter.displayName + ")" : "Continue";
            _ui.ShowTitle(gameTitle, canContinue, label);
        }

        void OnNewGame()
        {
            if (_state != GameState.Title) return;
            SaveSystem.Delete();
            _save = new SaveData();
            FighterSession.PlayerFighter = null;
            BeginPlacing();
        }

        void OnContinue()
        {
            if (_state != GameState.Title) return;
            if (_save == null || !_save.hasFighter) return;
            FighterSession.PlayerFighter = _save.fighter;
            BeginPlacing();
        }

        // ------------------------------------------------------------------ placing
        void BeginPlacing()
        {
            _state = GameState.Placing;
            if (_placer.HasPlacement) { AfterPlaced(); return; }
            _ui.ShowPlace();
        }

        void OnPlaceInFront()
        {
            if (_state != GameState.Placing) return;
            _placer.PlaceInFrontOfCamera();
            if (_placer.HasPlacement) AfterPlaced();
        }

        void AfterPlaced()
        {
            if (FighterSession.PlayerFighter != null) EnterHub();
            else EnterRoster();
        }

        // ------------------------------------------------------------------ roster
        void EnterRoster()
        {
            _state = GameState.Roster;
            SetHud(false);

            var stage = _placer.CreateStage("RosterStage");
            _roster = stage.AddComponent<RosterStage>();
            _roster.Init(catalog, showcaseWeapons);

            _candidates = new List<FighterData>();
            for (int i = 0; i < rosterSize; i++)
                _candidates.Add(FighterGenerator.Generate(catalog, FighterSide.Player));
            _selectedCandidate = -1;

            _roster.ShowCandidates(_candidates);
            _ui.ShowRoster();
        }

        void HandleRosterTap()
        {
            if (_roster == null) return;
            if (!ARStagePlacer.TryGetTap(out var pos)) return;
            if (UiGuard.IsOverUI(pos)) return;

            var cam = Camera.main;
            if (cam != null && _roster.TryPick(pos, cam, out int idx))
            {
                _selectedCandidate = idx;
                _roster.Select(idx);
                _ui.ShowRosterCard(_candidates[idx]);
            }
            else
            {
                ClearCandidateSelection();   // tapped empty space
            }
        }

        void ClearCandidateSelection()
        {
            _selectedCandidate = -1;
            if (_roster != null) _roster.ClearSelection();
            _ui.HideRosterCard();
        }

        void OnBack()
        {
            if (_state != GameState.Roster) return;
            ClearCandidateSelection();
        }

        void OnPick()
        {
            if (_state != GameState.Roster) return;
            if (_selectedCandidate < 0 || _selectedCandidate >= _candidates.Count) return;

            var chosen = _candidates[_selectedCandidate];
            FighterSession.PlayerFighter = chosen;

            if (_save == null) _save = new SaveData();
            _save.hasFighter = true;
            _save.fighter = chosen;
            SaveSystem.Save(_save);

            Debug.Log("[Game] Picked: " + chosen.Summary());
            EnterHub();
        }

        // ------------------------------------------------------------------ hub
        void EnterHub()
        {
            _state = GameState.Hub;
            SetHud(false);

            var fighter = FighterSession.PlayerFighter;
            var stage = _placer.CreateStage("HubStage");
            _roster = stage.AddComponent<RosterStage>();
            _roster.Init(catalog, showcaseWeapons);
            _roster.ShowSingle(fighter);

            _ui.ShowHub(fighter, _save != null ? _save.totalWins : 0);
        }

        void OnDeathMatch()
        {
            if (_state != GameState.Hub) return;
            EnterFight();
        }

        // ------------------------------------------------------------------ fight
        void EnterFight()
        {
            _state = GameState.Fight;
            _fightEnding = false;
            _roster = null;
            _ui.HideAll();

            SetHud(true);   // before the arena spawns, so the HUD can find the fighters

            var stage = _placer.InstantiateStage(arenaPrefab);
            _arena = stage != null ? stage.GetComponent<ArenaFighterSpawner>() : null;

            if (_arena == null || _arena.Player == null || _arena.Enemy == null)
            {
                Debug.LogError("[Game] The arena did not spawn both fighters. Check the Console above for [Fighters] errors.");
                EnterHub();
                return;
            }
        }

        void MonitorFight()
        {
            if (_fightEnding || _arena == null) return;
            if (_arena.Player == null || _arena.Enemy == null) return;

            var playerHp = _arena.Player.GetComponent<Health>();
            var enemyHp = _arena.Enemy.GetComponent<Health>();
            if (playerHp == null || enemyHp == null) return;

            bool playerDead = playerHp.IsDead;
            bool enemyDead = enemyHp.IsDead;
            if (playerDead || enemyDead)
            {
                _fightEnding = true;
                StartCoroutine(EndFight(enemyDead && !playerDead));
            }
        }

        IEnumerator EndFight(bool playerWon)
        {
            yield return new WaitForSeconds(fightEndDelay);
            EnterResult(playerWon);
        }

        // ------------------------------------------------------------------ result
        void EnterResult(bool playerWon)
        {
            _state = GameState.Result;
            _lastResultWon = playerWon;
            SetHud(false);

            var fighter = FighterSession.PlayerFighter;
            if (_save == null) _save = new SaveData();

            if (playerWon)
            {
                fighter.wins++;
                _save.totalWins++;
                _save.hasFighter = true;
                _save.fighter = fighter;
                SaveSystem.Save(_save);

                _ui.ShowResult("VICTORY", new Color(1f, 0.82f, 0.25f),
                               fighter.displayName + " wins!\nFights won: " + fighter.wins, "Continue");
            }
            else
            {
                // Permadeath: the fighter is gone.
                string name = fighter != null ? fighter.displayName : "Your fighter";
                FighterSession.PlayerFighter = null;
                _save.hasFighter = false;
                _save.fighter = new FighterData();
                SaveSystem.Save(_save);

                _ui.ShowResult("DEFEATED", new Color(0.92f, 0.30f, 0.30f),
                               name + " has fallen.", "Choose a new fighter");
            }
        }

        void OnResultContinue()
        {
            if (_state != GameState.Result) return;
            if (_lastResultWon) EnterHub();
            else EnterRoster();
        }
    }
}
