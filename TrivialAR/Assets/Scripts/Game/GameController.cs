using System.Collections;
using System.Collections.Generic;
using AR;
using Core;
using Fighters;
using UnityEngine;
using Weaponary;

namespace Game
{
    public enum GameState { Title, Placing, Roster, Hub, Fight, Result, Book }

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
        [Tooltip("Material for the roster floor and tombstone. Assigned by Tools > Kaiju > Game > Create Stage Material.")]
        public Material stageMaterial;

        [Header("UI and stage prefabs (made by Tools > Kaiju > Game > Create Game Prefabs - edit them freely)")]
        public GameUI gameUiPrefab;
        public RosterStage rosterStagePrefab;
        public TombstoneStage tombstoneStagePrefab;
        [Tooltip("The AR Book (made by Tools > Kaiju > Game > Create Book Prefabs). Leave empty to hide the Book.")]
        public BookStage bookStagePrefab;
        [Tooltip("While the book is open, hide the table-top fighter and floor so the (big) book has room. OFF = they stay.")]
        public bool hideHubWhileBookOpen = true;
        [Tooltip("Leave empty to keep the title text from the GameUI prefab.")]
        public string titleOverride = "";

        public enum DebugOutcome { None, ForceVictory, ForceDefeat }

        [Header("Debug")]
        public bool debugLogs = false;
        [Tooltip("Shows a small readout in the top-left corner (touch devices, last tap, what's under it, recent errors). Turn off when done testing on the phone.")]
        public bool deviceDebugOverlay = true;
        [Tooltip("Testing: end every fight after ~4 seconds with this result (e.g. to see the tombstone).")]
        public DebugOutcome debugOutcome = DebugOutcome.None;

        [Header("Duel")]
        [Tooltip("Hero XP from a won duel, as a fraction of a normal win (duels are for training, and nobody dies).")]
        public float duelWinXpFactor = 0.5f;
        [Tooltip("Hero XP from a lost duel. You still learn something.")]
        public float duelLossXpFactor = 0.2f;

        [Header("Tuning")]
        public int rosterSize = 3;
        [Tooltip("Seconds to let the death animation play before showing the result.")]
        public float fightEndDelay = 2.5f;

        public GameState State { get { return _state; } }

        static GameController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; }

        GameState _state;
        GameUI _ui;
        ARStagePlacer _placer;
        RosterStage _roster;
        BookStage _book;
        GameObject _bookObject;
        GameObject _hiddenStage;
        bool _bookClosing;
        Canvas _hudCanvas;                 // the scene's existing fight HUD (portraits + health bars)
        SaveData _save;

        List<FighterData> _candidates = new List<FighterData>();
        int _selectedCandidate = -1;

        ArenaFighterSpawner _arena;
        bool _fightEnding;
        float _fightStarted;
        bool _resultToHub;
        FighterData _lastEnemy;
        FightMode _mode = FightMode.DeathMatch;
        int[] _profBefore;
        readonly System.Random _rng = new System.Random();

        // ------------------------------------------------------------------ setup
        void Awake()
        {
            // Only one GameController may exist (a second one would build a second, duplicate menu).
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[Game] A second GameController was found and removed. Keep only one in the scene.", this);
                enabled = false;
                Destroy(gameObject);
                return;
            }
            _instance = this;

            StageMaterials.Base = stageMaterial;
            _placer = GetComponent<ARStagePlacer>();
            if (_placer == null) _placer = gameObject.AddComponent<ARStagePlacer>();

            if (gameUiPrefab != null)
            {
                _ui = Instantiate(gameUiPrefab, transform);
                _ui.name = "GameUI";
            }
            else
            {
                _ui = GetComponent<GameUI>();
                if (_ui == null) _ui = gameObject.AddComponent<GameUI>();   // fallback: build the default menus in code
            }

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
            _ui.DuelClicked += OnDuel;
            _ui.StatPointSpent += OnSpendPoint;
            _ui.ResultContinueClicked += OnResultContinue;
            _ui.BookTapped += OnBookTapped;
            _ui.BookDropped += OnBookDropped;
            _ui.BookPrevClicked += OnBookPrev;
            _ui.BookNextClicked += OnBookNext;
            _ui.BookCloseClicked += OnBookClose;

            // Remove any overlay that was saved into a prefab by accident, then add a fresh one.
            var oldOverlay = GetComponent<DeviceDebugOverlay>();
            if (oldOverlay != null) DestroyImmediate(oldOverlay);
            var oldOverlayCanvas = transform.Find("DeviceDebugCanvas");
            if (oldOverlayCanvas != null) DestroyImmediate(oldOverlayCanvas.gameObject);
            if (deviceDebugOverlay) gameObject.AddComponent<DeviceDebugOverlay>().Init(() => _state.ToString());

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
            _ui.DuelClicked -= OnDuel;
            _ui.StatPointSpent -= OnSpendPoint;
            _ui.ResultContinueClicked -= OnResultContinue;
            _ui.BookTapped -= OnBookTapped;
            _ui.BookDropped -= OnBookDropped;
            _ui.BookPrevClicked -= OnBookPrev;
            _ui.BookNextClicked -= OnBookNext;
            _ui.BookCloseClicked -= OnBookClose;
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
            _ui.ShowTitle(titleOverride, canContinue, label);
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

            _roster = NewRosterStage("RosterStage");
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

            if (debugLogs) Debug.Log("[Game] Picked: " + chosen.Summary());
            EnterHub();
        }

        // ------------------------------------------------------------------ hub
        void EnterHub()
        {
            _state = GameState.Hub;
            SetHud(false);

            var fighter = FighterSession.PlayerFighter;
            _roster = NewRosterStage("HubStage");
            _roster.Init(catalog, showcaseWeapons);
            _roster.ShowSingle(fighter);

            _ui.ShowHub(fighter, _save != null ? _save.totalWins : 0);
        }

        // ------------------------------------------------------------------ book
        // The Book icon was dragged onto the table: the book appears where it was dropped. A plain tap puts it in the middle.
        void OnBookTapped() { PlaceBook(false, default(Vector2)); }
        void OnBookDropped(Vector2 screenPos) { PlaceBook(true, screenPos); }

        void PlaceBook(bool useScreenPoint, Vector2 screenPos)
        {
            if (_state != GameState.Hub) return;
            var fighter = FighterSession.PlayerFighter;
            if (fighter == null) return;
            if (bookStagePrefab == null)
            {
                Debug.LogError("[Game] No Book prefab assigned. Run Tools > Kaiju > Game > Create Book Prefabs.", this);
                return;
            }

            Vector3 point;
            if (!useScreenPoint || !_placer.TryGroundPoint(screenPos, out point)) point = _placer.StageOrigin;

            // Facing you: its +Z points away from the camera, like every stage.
            Vector3 dir = Vector3.forward;
            var cam = Camera.main;
            if (cam != null)
            {
                dir = point - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) { dir = cam.transform.forward; dir.y = 0f; }
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            }

            var go = _placer.InstantiateAt(bookStagePrefab.gameObject, point, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var book = go != null ? go.GetComponent<BookStage>() : null;
            if (book == null)
            {
                if (go != null) Destroy(go);
                Debug.LogError("[Game] The Book prefab has no BookStage component on its root (or the table is not placed yet).", this);
                return;
            }

            _bookObject = go;
            _book = book;
            _hiddenStage = null;
            if (hideHubWhileBookOpen && _placer.CurrentStage != null)
            {
                _hiddenStage = _placer.CurrentStage;
                _hiddenStage.SetActive(false);
            }

            _state = GameState.Book;
            _bookClosing = false;
            _book.Opened += RefreshBookUi;
            _book.PageChanged += RefreshBookUi;
            _book.Failed += OnBookFailed;
            _ui.ShowBook("Opening the book...", false, false);

            if (debugLogs) Debug.Log("[Game] Book placed at " + point + ". Fallen: " + (_save != null && _save.fallen != null ? _save.fallen.Count : 0));
            _book.Open(fighter, _save != null ? _save.fallen : null);
        }

        void RefreshBookUi()
        {
            if (_state != GameState.Book || _book == null) return;
            _ui.SetBookControls(_book.PageLabel, _book.CanTurnBack, _book.CanTurnForward);
        }

        void OnBookPrev()
        {
            if (_state != GameState.Book || _book == null || _bookClosing) return;
            _book.TurnBackward();
        }

        void OnBookNext()
        {
            if (_state != GameState.Book || _book == null || _bookClosing) return;
            _book.TurnForward();
        }

        void OnBookClose()
        {
            if (_state != GameState.Book || _book == null || _bookClosing) return;
            _bookClosing = true;
            _ui.SetBookControls("", false, false);
            _book.Close(() =>
            {
                if (_state != GameState.Book) return;
                LeaveBook();
                BackToHub();
            });
        }

        void OnBookFailed()
        {
            if (_state != GameState.Book) return;
            Debug.LogError("[Game] The Book could not be opened. Check the Console for [Book] messages.", this);
            LeaveBook();
            BackToHub();
        }

        // The hub stage was only hidden while the book was open, so just show it and the hub menu again.
        void BackToHub()
        {
            _state = GameState.Hub;
            _ui.ShowHub(FighterSession.PlayerFighter, _save != null ? _save.totalWins : 0);
        }

        void LeaveBook()
        {
            if (_book != null)
            {
                _book.Opened -= RefreshBookUi;
                _book.PageChanged -= RefreshBookUi;
                _book.Failed -= OnBookFailed;
            }
            _book = null;
            _bookClosing = false;
            // Dismiss lets the closing sound finish before the book is removed.
            if (_bookObject != null)
            {
                var closing = _bookObject.GetComponent<BookStage>();
                if (closing != null) closing.Dismiss();
                else Destroy(_bookObject);
            }
            _bookObject = null;
            if (_hiddenStage != null) _hiddenStage.SetActive(true);
            _hiddenStage = null;
        }

        void OnDeathMatch()
        {
            if (_state != GameState.Hub) return;
            EnterFight(FightMode.DeathMatch);
        }

        // A duel is a death match where nobody dies: the loser kneels, and you keep your fighter.
        void OnDuel()
        {
            if (_state != GameState.Hub) return;
            EnterFight(FightMode.Duel);
        }

        // A "+" on the Stats screen: one free level-up point goes into the chosen stat. Saved at once.
        void OnSpendPoint(int stat)
        {
            if (_state != GameState.Hub) return;
            var f = FighterSession.PlayerFighter;
            if (f == null || f.statPoints <= 0) return;

            switch (stat)
            {
                case 0: if (f.might     >= ProgressionRules.StatCap) return; f.might++;     break;
                case 1: if (f.ego       >= ProgressionRules.StatCap) return; f.ego++;       break;
                case 2: if (f.education >= ProgressionRules.StatCap) return; f.education++; break;
                case 3: if (f.insight   >= ProgressionRules.StatCap) return; f.insight++;   break;
                default: return;
            }
            f.statPoints--;

            if (_save == null) _save = new SaveData();
            _save.hasFighter = true;
            _save.fighter = f;
            SaveSystem.Save(_save);
            _ui.RefreshStatsUi(f);
            if (debugLogs) Debug.Log("[Game] Spent a point on stat " + stat + ". Left: " + f.statPoints);
        }

        // ------------------------------------------------------------------ fight
        void EnterFight(FightMode mode)
        {
            _mode = mode;
            FighterSession.Mode = mode;   // read by the arena's fighters when they spawn
            _state = GameState.Fight;
            _fightEnding = false;
            _fightStarted = Time.time;
            _profBefore = ProgressionRules.ProfSnapshot(FighterSession.PlayerFighter);
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

            _lastEnemy = _arena.Enemy.Data;
        }

        void MonitorFight()
        {
            if (_fightEnding || _arena == null) return;

            if (debugOutcome != DebugOutcome.None && Time.time - _fightStarted > 4f)
            {
                _fightEnding = true;
                StartCoroutine(EndFight(debugOutcome == DebugOutcome.ForceVictory));
                return;
            }
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
            SetHud(false);

            var fighter = FighterSession.PlayerFighter;
            if (_save == null) _save = new SaveData();
            fighter.fights++;

            if (_mode == FightMode.Duel)
            {
                EnterDuelResult(fighter, playerWon);
                return;
            }

            if (playerWon)
            {
                fighter.wins++;
                _save.totalWins++;

                int xp = ProgressionRules.XpReward(fighter, _lastEnemy != null ? _lastEnemy : fighter);
                var up = ProgressionRules.AddXp(fighter, xp, _rng);

                _save.hasFighter = true;
                _save.fighter = fighter;
                SaveSystem.Save(_save);

                ShowFighterStage(fighter);
                _resultToHub = true;
                _ui.ShowResult("VICTORY", new Color(1f, 0.82f, 0.25f), BuildVictoryText(fighter, up, _profBefore), "Continue");
            }
            else if (Random.value < ProgressionRules.DefeatSurvivalChance)
            {
                ShowFighterStage(fighter);
                _resultToHub = true;
                _ui.ShowResult("DEFEATED", new Color(1f, 0.6f, 0.25f),
                               fighter.displayName + " was left for dead...\nbut survived!", "Continue");
            }
            else
            {
                // Permadeath: the fighter is gone, and gets a tombstone.
                var fallen = fighter;
                FighterSession.PlayerFighter = null;
                if (_save.fallen == null) _save.fallen = new List<FallenFighter>();
                _save.fallen.Add(new FallenFighter(fallen, _lastEnemy));   // the graveyard pages of the Book
                _save.hasFighter = false;
                _save.fighter = new FighterData();
                SaveSystem.Save(_save);

                var stage = tombstoneStagePrefab != null
                    ? _placer.InstantiateStage(tombstoneStagePrefab.gameObject)
                    : _placer.CreateStage("TombstoneStage");
                if (stage != null)
                {
                    var tomb = stage.GetComponent<TombstoneStage>();
                    if (tomb == null) tomb = stage.AddComponent<TombstoneStage>();
                    tomb.Build(fallen, _lastEnemy);
                }

                _resultToHub = false;
                _ui.ShowResult("FALLEN", new Color(0.92f, 0.30f, 0.30f),
                               fallen.displayName + " has fallen.\nLevel " + fallen.level + "  -  " + fallen.wins + " win" + (fallen.wins == 1 ? "" : "s"),
                               "Choose a new fighter");
            }
        }

        // Duel: nobody dies. Win or lose you keep your fighter; you get a share of the XP, and the weapon proficiency
        // earned during the fight (1.5x in duels) is already in the fighter's data.
        void EnterDuelResult(FighterData fighter, bool playerWon)
        {
            FighterSession.Mode = FightMode.DeathMatch;
            _mode = FightMode.DeathMatch;

            float factor = playerWon ? duelWinXpFactor : duelLossXpFactor;
            int xp = Mathf.Max(1, Mathf.RoundToInt(ProgressionRules.XpReward(fighter, _lastEnemy != null ? _lastEnemy : fighter) * factor));
            if (playerWon) { fighter.wins++; _save.totalWins++; }
            var up = ProgressionRules.AddXp(fighter, xp, _rng);

            _save.hasFighter = true;
            _save.fighter = fighter;
            SaveSystem.Save(_save);

            ShowFighterStage(fighter);
            _resultToHub = true;
            if (playerWon)
                _ui.ShowResult("DUEL WON", new Color(1f, 0.82f, 0.25f), BuildVictoryText(fighter, up, _profBefore, fighter.displayName + " wins the duel!"), "Continue");
            else
                _ui.ShowResult("DUEL LOST", new Color(1f, 0.6f, 0.25f),
                               BuildVictoryText(fighter, up, _profBefore, fighter.displayName + " yields, and nobody is hurt.\nYou still learn from it."), "Continue");
        }

        // A roster/hub/result stage: from the prefab if one is assigned, otherwise built in code.
        RosterStage NewRosterStage(string stageName)
        {
            GameObject stage = rosterStagePrefab != null
                ? _placer.InstantiateStage(rosterStagePrefab.gameObject)
                : _placer.CreateStage(stageName);
            if (stage == null) return null;
            var rs = stage.GetComponent<RosterStage>();
            if (rs == null) rs = stage.AddComponent<RosterStage>();
            return rs;
        }

        // The winner standing on the table (re-uses the hub display).
        void ShowFighterStage(FighterData fighter)
        {
            _roster = NewRosterStage("ResultStage");
            if (_roster == null) return;
            _roster.Init(catalog, showcaseWeapons);
            _roster.ShowSingle(fighter);
        }

        static string BuildVictoryText(FighterData f, LevelUpResult up, int[] profBefore, string headline = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(headline != null ? headline : f.displayName + " wins!");
            sb.Append("\n+").Append(up.xpGained).Append(" XP   (").Append(f.xp).Append(" / ").Append(ProgressionRules.XpToNext(f.level)).Append(")");
            if (up.levelsGained > 0)
            {
                sb.Append("\nLEVEL UP!  Lv ").Append(f.level);
                foreach (var s in up.statGains) sb.Append("   ").Append(s);
                if (up.pointsGained > 0)
                    sb.Append("\n+").Append(up.pointsGained).Append(up.pointsGained == 1 ? " stat point" : " stat points").Append("  (spend it under Stats)");
            }
            string prof = ProgressionRules.ProfDiffText(profBefore, f);
            if (!string.IsNullOrEmpty(prof)) sb.Append("\n").Append(prof);
            return sb.ToString();
        }

        void OnResultContinue()
        {
            if (_state != GameState.Result) return;
            if (_resultToHub) EnterHub();
            else EnterRoster();
        }
    }
}
