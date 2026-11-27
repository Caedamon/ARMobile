using System.Collections;
using System.Collections.Generic;
using AR;
using Core;
using Fighters;
using UnityEngine;
using Weaponary;

namespace Game
{
    public enum GameState { Title, Placing, Roster, Hub, Fight, Result, Book, Schedule }

    /// <summary>
    /// The game loop:  Title -> (place on table) -> Roster (pick 1 of 3) -> Hub -> Fight Schedule (pick 1 of 3, three times, then a finale) -> Fight -> Result -> Hub / Roster.
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

        [Header("Fight schedule")]
        [Tooltip("Regular fights (pick 1 of 3 each) before the finale.")]
        public int fightsPerRun = 3;
        [Tooltip("How many levels above the hero the finale boss is. The boss is also the best tier out of many rolls (Elite if possible).")]
        public int bossLevelBonus = 2;
        [Tooltip("Hero XP from winning the boss fight, as a multiple of a normal win against that opponent.")]
        public float bossXpFactor = 1.5f;
        [Tooltip("Free stat points for winning the finale (finishing the whole schedule).")]
        public int championBonusPoints = 1;

        public enum ArenaFloor { Never, WhenNoSurface, Always }

        [Header("Arena floor")]
        [Tooltip("A dark disc under the fighters. WhenNoSurface = only after 'Place in front of me' (nothing real to stand on, so they would hover in mid-air). Always = also on a real table, like the hub.")]
        public ArenaFloor arenaFloor = ArenaFloor.WhenNoSurface;
        public Color arenaFloorColor = new Color(0.10f, 0.11f, 0.15f, 1f);

        [Header("Boss intro (the little film before a Boss fight)")]
        [Tooltip("A portal opens, the boss walks out, the picture zooms in, the name and BOSS! smash onto the screen, the boss roars. Tap skips it.")]
        public bool bossIntro = true;
        public string bossLabel = "BOSS!";
        public Color bossNameColor = new Color(0.90f, 0.05f, 0.05f, 1f);
        public Color bossLabelColor = new Color(0.30f, 0.04f, 0.45f, 1f);
        [Tooltip("Optional. Your own roar recording. Empty = low layered voice sounds from the fighters' sound set.")]
        public AudioClip bossRoarClip;
        [Tooltip("Optional. Your own 'smash' sound for the two text hits. Empty = the heavy hit sound, lowered.")]
        public AudioClip bossSlamClip;
        [Tooltip("Optional. Sound of the portal opening.")]
        public AudioClip bossPortalClip;

        [Header("Group fights")]
        [Tooltip("Group Fight (regular fights): teammates you get besides yourself...")]
        public int groupFightAllies = 1;
        [Tooltip("...and the opponents you face.")]
        public int groupFightFoes = 2;
        [Tooltip("Group Death Match (finale): teammates besides yourself...")]
        public int finaleAllies = 2;
        [Tooltip("...and opponents.")]
        public int finaleFoes = 3;
        [Tooltip("Last Hero Standing (finale): how many rivals you fight, all against everyone. 4 = five fighters in all.")]
        public int lastHeroRivals = 4;
        [Tooltip("Hero XP for a group win = this x the XP of each defeated opponent added up.")]
        public float groupXpFactor = 0.7f;
        [Tooltip("Same for Last Hero Standing.")]
        public float lastHeroXpFactor = 0.6f;

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
        bool _fromSchedule;                // this fight was picked from the schedule (so winning it moves the run on)
        FightCard _card;                   // the schedule card being fought (null for the old direct buttons)
        bool _introPlaying;                // the boss film is on: nobody fights yet
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
            _ui.ScheduleClicked += OnSchedule;
            _ui.ScheduleBackClicked += OnScheduleBack;
            _ui.ScheduleCardPicked += OnScheduleCard;
            _ui.StatPointSpent += OnSpendPoint;
            _ui.ResultContinueClicked += OnResultContinue;
            _ui.BookTapped += OnBookTapped;
            _ui.BookDropped += OnBookDropped;
            _ui.BookPrevClicked += OnBookPrev;
            _ui.BookNextClicked += OnBookNext;
            _ui.BookCloseClicked += OnBookClose;
            _ui.BookTabClicked += OnBookTab;

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
            _ui.ScheduleClicked -= OnSchedule;
            _ui.ScheduleBackClicked -= OnScheduleBack;
            _ui.ScheduleCardPicked -= OnScheduleCard;
            _ui.StatPointSpent -= OnSpendPoint;
            _ui.ResultContinueClicked -= OnResultContinue;
            _ui.BookTapped -= OnBookTapped;
            _ui.BookDropped -= OnBookDropped;
            _ui.BookPrevClicked -= OnBookPrev;
            _ui.BookNextClicked -= OnBookNext;
            _ui.BookCloseClicked -= OnBookClose;
            _ui.BookTabClicked -= OnBookTab;
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
                    else _ui.SetPlaceHint(_placer.HintText);
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

            ShowHubUi(fighter);
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
            _ui.SetBookTabButtonsVisible(!_book.WantsWorldTabs);   // the book has its own side tabs (they come back if those could not be built)

            if (debugLogs) Debug.Log("[Game] Book placed at " + point + ". Fallen: " + (_save != null && _save.fallen != null ? _save.fallen.Count : 0));
            _book.Open(fighter, _save != null ? _save.fallen : null);
        }

        void RefreshBookUi()
        {
            if (_state != GameState.Book || _book == null) return;
            _ui.SetBookControls(_book.PageLabel, _book.CanTurnBack, _book.CanTurnForward);
            if (_book.IsOpen) _ui.SetBookTabButtonsVisible(!_book.HasWorldTabs);
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

        // A tab under the open book: 0 Index, 1 Memoria Dies, 2 Stats, 3 Armoury (the same order as BookStage.Section).
        void OnBookTab(int tab)
        {
            if (_state != GameState.Book || _book == null || _bookClosing) return;
            if (tab < 0 || tab > 3) return;
            _book.JumpTo((BookStage.Section)tab);
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
            ShowHubUi(FighterSession.PlayerFighter);
        }

        void ShowHubUi(FighterData fighter)
        {
            _ui.ShowHub(fighter, _save != null ? _save.totalWins : 0);
            int done = _save != null && _save.run != null ? _save.run.fightsDone : 0;
            bool finale = done >= fightsPerRun;
            _ui.SetScheduleLabel(finale ? "Fight Schedule:  FINALE" : "Fight Schedule:  Fight " + (done + 1) + " of " + fightsPerRun, finale);
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
            _fromSchedule = false;
            _card = null;
            EnterFight(FightMode.DeathMatch);
        }

        // A duel is a death match where nobody dies: the loser kneels, and you keep your fighter.
        void OnDuel()
        {
            if (_state != GameState.Hub) return;
            _fromSchedule = false;
            _card = null;
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

        // ------------------------------------------------------------------ fight schedule
        // Pick 1 of 3 cards, three times, then a finale (pick 1 of 3). The cards (and their opponents) are saved, so closing the game
        // does not re-roll them. Winning a Death Match or finishing a Duel moves the schedule on; dying ends the hero and the schedule.
        RunState Run()
        {
            if (_save == null) _save = new SaveData();
            if (_save.run == null) _save.run = new RunState();
            if (_save.run.cards == null) _save.run.cards = new List<FightCard>();
            return _save.run;
        }

        void OnSchedule()
        {
            if (_state != GameState.Hub) return;
            var hero = FighterSession.PlayerFighter;
            if (hero == null) return;

            var run = Run();
            if (!run.active || run.cards.Count == 0) RollCards(run, hero);   // first time (or a fresh schedule)
            EnterSchedule();
        }

        void RollCards(RunState run, FighterData hero)
        {
            run.active = true;
            run.cards = run.fightsDone >= fightsPerRun
                ? RunPlanner.RollFinale(catalog, hero, bossLevelBonus, _rng, finaleAllies, finaleFoes, lastHeroRivals)
                : RunPlanner.RollRegular(catalog, hero, _rng, groupFightAllies, groupFightFoes);
            if (_save != null) { _save.hasFighter = true; _save.fighter = hero; SaveSystem.Save(_save); }
            if (debugLogs) Debug.Log("[Game] Rolled " + run.cards.Count + " schedule cards (fights done: " + run.fightsDone + ")");
        }

        void EnterSchedule()
        {
            _state = GameState.Schedule;
            var hero = FighterSession.PlayerFighter;
            var run = Run();
            bool finale = run.fightsDone >= fightsPerRun;

            var preview = new int[3];
            for (int i = 0; i < 3 && i < run.cards.Count; i++)
                preview[i] = PreviewXp(run.cards[i], hero);

            _ui.ShowSchedule(finale ? "FINALE" : "FIGHT " + (run.fightsDone + 1) + " OF " + fightsPerRun,
                             finale ? "Choose your final battle" : "Pick your next opponent",
                             run.cards, hero, preview);
        }

        void OnScheduleBack()
        {
            if (_state != GameState.Schedule) return;
            BackToHub();
        }

        void OnScheduleCard(int index)
        {
            if (_state != GameState.Schedule) return;
            var run = Run();
            if (index < 0 || index >= run.cards.Count) return;
            var card = run.cards[index];
            if (card == null || card.locked || card.enemy == null) return;

            _fromSchedule = true;
            _card = card;
            FighterSession.EnemyFighter = card.IsGroup ? null : card.enemy;   // the arena uses this opponent instead of rolling a random one
            if (debugLogs) Debug.Log("[Game] Schedule pick: " + card.mode + " vs " + card.enemy.Summary() + (card.IsGroup ? "  (+" + (card.foes.Count - 1) + " more, " + card.allies.Count + " allies)" : ""));
            EnterFight(card.mode, card);
        }

        // XP a win on this card would give (the same numbers the result screen uses).
        int PreviewXp(FightCard card, FighterData hero)
        {
            if (card == null || card.locked || card.enemy == null || hero == null) return 0;
            return RewardForCard(card, hero, true);
        }

        // Group fights pay for every opponent beaten; the others pay for the one opponent.
        int RewardForCard(FightCard card, FighterData hero, bool won)
        {
            if (!card.IsGroup) return RewardFor(card.mode, hero, card.enemy, won);

            float xp = 0f;
            foreach (var f in card.foes) xp += ProgressionRules.XpReward(hero, f);
            xp *= card.mode == FightMode.LastHero ? lastHeroXpFactor : groupXpFactor;
            return Mathf.Max(1, Mathf.RoundToInt(xp));
        }

        int RewardFor(FightMode mode, FighterData hero, FighterData enemy, bool won)
        {
            float xp = ProgressionRules.XpReward(hero, enemy);
            if (mode == FightMode.Duel) xp *= won ? duelWinXpFactor : duelLossXpFactor;
            else if (mode == FightMode.Boss) xp *= bossXpFactor;
            return Mathf.Max(1, Mathf.RoundToInt(xp));
        }

        // A fight from the schedule was won (or a duel was finished): the next three cards.
        void AdvanceRun(FighterData hero)
        {
            if (!_fromSchedule) return;
            var run = Run();
            run.fightsDone++;
            RollCards(run, hero);
        }

        // ------------------------------------------------------------------ fight
        void EnterFight(FightMode mode, FightCard card = null)
        {
            _mode = mode;

            // group fights: who fights on which side (read by the arena spawner when the arena appears)
            FighterSession.ClearGroup();
            if (card != null && card.IsGroup)
            {
                FighterSession.Allies.AddRange(card.allies);
                FighterSession.Foes.AddRange(card.foes);
                FighterSession.FreeForAll = card.mode == FightMode.LastHero;
            }
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
            AddArenaFloor(stage);
            FighterSession.EnemyFighter = null;   // the arena has picked it up; later fights roll their own
            FighterSession.ClearGroup();

            if (_arena == null || _arena.Player == null || _arena.Foes == null || _arena.Foes.Count == 0)
            {
                Debug.LogError("[Game] The arena did not spawn the fighters. Check the Console above for [Fighters] errors.");
                EnterHub();
                return;
            }

            // the toughest opponent is the one who "killed" the hero if the hero falls (tombstone and Book)
            FighterIdentity toughest = _arena.Foes[0];
            foreach (var f in _arena.Foes)
                if (f != null && f.Data != null && (toughest == null || toughest.Data == null || f.Data.level * 2 + f.Data.VisibleTotal > toughest.Data.level * 2 + toughest.Data.VisibleTotal)) toughest = f;
            _lastEnemy = toughest != null ? toughest.Data : null;

            if (mode == FightMode.Boss && bossIntro && _arena.Foes[0] != null)
                StartBossIntro(_arena.Foes[0]);
        }

        // The film before a boss fight. The fighters are frozen (and hidden) from this moment until it ends, so nothing happens behind it.
        void StartBossIntro(FighterIdentity boss)
        {
            try
            {
                string bossTitle = boss.Data != null ? boss.Data.displayName : "BOSS";
                if (bossTitle.StartsWith("Boss ")) bossTitle = bossTitle.Substring(5);   // the card name is "Boss <name>"; the film says BOSS! by itself

                var go = new GameObject("BossIntro");
                var intro = go.AddComponent<BossIntro>();
                intro.bossName = bossTitle;
                intro.label = bossLabel;
                intro.nameColor = bossNameColor;
                intro.labelColor = bossLabelColor;
                intro.roarClip = bossRoarClip;
                intro.slamClip = bossSlamClip;
                intro.portalClip = bossPortalClip;

                _introPlaying = true;
                SetHud(false);
                intro.Begin(_arena, boss, () =>
                {
                    _introPlaying = false;
                    _fightStarted = Time.time;
                    SetHud(true);
                });
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Game] Boss intro failed, starting the fight: " + e.Message, this);
                _introPlaying = false;
                SetHud(true);
            }
        }

        // The arena prefab has no ground of its own (on a real table the table is the ground). After "Place in front of me" there is
        // nothing real to stand on, so draw the same dark disc the hub has under the fighters.
        void AddArenaFloor(GameObject stage)
        {
            if (stage == null || arenaFloor == ArenaFloor.Never) return;
            if (arenaFloor == ArenaFloor.WhenNoSurface && _placer != null && _placer.PlacedOnSurface) return;

            float sep = _arena != null ? _arena.fighterSeparation : 1.2f;
            float diameter = Mathf.Max(sep + 0.7f, 0.9f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "ArenaFloor";
            var col = floor.GetComponent<Collider>();
            if (col != null) Destroy(col);
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.004f, 0f);   // just under the fighters' feet
            floor.transform.localScale = new Vector3(diameter, 0.003f, diameter);

            var rend = floor.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mat = StageMaterials.Make(arenaFloorColor);
                if (mat != null) rend.sharedMaterial = mat;
            }
        }

        void MonitorFight()
        {
            if (_fightEnding || _arena == null || _introPlaying) return;

            if (debugOutcome != DebugOutcome.None && Time.time - _fightStarted > 4f)
            {
                _fightEnding = true;
                StartCoroutine(EndFight(debugOutcome == DebugOutcome.ForceVictory, false));
                return;
            }
            if (_arena.Player == null || _arena.Foes == null || _arena.Foes.Count == 0) return;

            var playerHp = _arena.Player.GetComponent<Health>();
            if (playerHp == null) return;

            // the fight is won when every opponent is down (one in a 1 against 1 fight), lost when the hero falls
            bool allFoesDead = true;
            foreach (var f in _arena.Foes)
            {
                if (f == null) continue;
                var hp = f.GetComponent<Health>();
                if (hp != null && !hp.IsDead) { allFoesDead = false; break; }
            }

            bool playerDead = playerHp.IsDead;

            // Team fights (Group Fight, Group Death Match): the fight plays on after the hero falls, and ends when one TEAM is down.
            // If the hero's team still wins, the hero lives but the schedule starts over (see EnterResult).
            bool teamFight = _card != null && _card.IsGroup && _mode != FightMode.LastHero;
            if (teamFight)
            {
                bool anyAllyUp = false;
                foreach (var a in _arena.Allies)
                {
                    if (a == null) continue;
                    var hp = a.GetComponent<Health>();
                    if (hp != null && !hp.IsDead) { anyAllyUp = true; break; }
                }

                if (allFoesDead)
                {
                    _fightEnding = true;
                    StartCoroutine(EndFight(true, playerDead));
                }
                else if (playerDead && !anyAllyUp)
                {
                    _fightEnding = true;
                    StartCoroutine(EndFight(false, false));
                }
                return;
            }

            if (playerDead || allFoesDead)
            {
                _fightEnding = true;
                StartCoroutine(EndFight(allFoesDead && !playerDead, false));
            }
        }

        IEnumerator EndFight(bool playerWon, bool heroFell = false)
        {
            yield return new WaitForSeconds(fightEndDelay);
            EnterResult(playerWon, heroFell);
        }

        // ------------------------------------------------------------------ result
        // heroFell: only in team fights. The hero went down but their team won anyway.
        void EnterResult(bool playerWon, bool heroFell = false)
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

            if (playerWon && heroFell)
            {
                // The team won, but the hero went down: it counts as a LOSS for the schedule (it starts over from fight 1 with new
                // opponents) but the hero is alive and keeps everything earned so far (levels, stat points, training). No XP for this fight.
                _save.run = new RunState();
                _save.hasFighter = true;
                _save.fighter = fighter;
                SaveSystem.Save(_save);

                ShowFighterStage(fighter);
                _resultToHub = true;
                _ui.ShowResult("DEFEATED", new Color(1f, 0.6f, 0.25f),
                               fighter.displayName + " fell in battle,\nbut the team won the day!\n\nThe schedule starts over from fight 1.\n" + fighter.displayName + " keeps everything earned so far.",
                               "Continue");
            }
            else if (playerWon)
            {
                fighter.wins++;
                _save.totalWins++;

                int xp = _card != null
                    ? RewardForCard(_card, fighter, true)
                    : RewardFor(_mode, fighter, _lastEnemy != null ? _lastEnemy : fighter, true);
                var up = ProgressionRules.AddXp(fighter, xp, _rng);

                string title = "VICTORY";
                string headline = _mode == FightMode.Group ? fighter.displayName + "'s team wins!" : null;
                string extra;
                if ((_mode == FightMode.Boss || _mode == FightMode.GroupDeathMatch || _mode == FightMode.LastHero) && _fromSchedule)
                {
                    // The finale is won: the schedule is complete, with a bonus. The next press on Fight Schedule starts a new one.
                    fighter.statPoints += championBonusPoints;
                    _save.runsWon++;
                    _save.run = new RunState();
                    title = "CHAMPION!";
                    if (_mode == FightMode.Boss) headline = fighter.displayName + " beat the boss and finished the schedule!";
                    else if (_mode == FightMode.GroupDeathMatch) headline = fighter.displayName + "'s team won the group death match and finished the schedule!";
                    else headline = fighter.displayName + " is the last hero standing and finished the schedule!";
                    extra = championBonusPoints > 0
                        ? "+" + championBonusPoints + (championBonusPoints == 1 ? " bonus stat point" : " bonus stat points") + "  (spend under Stats)"
                        : null;
                }
                else
                {
                    AdvanceRun(fighter);
                    extra = RunProgressLine();
                }

                _save.hasFighter = true;
                _save.fighter = fighter;
                SaveSystem.Save(_save);

                ShowFighterStage(fighter);
                _resultToHub = true;
                _ui.ShowResult(title, new Color(1f, 0.82f, 0.25f), BuildVictoryText(fighter, up, _profBefore, headline, extra), "Continue");
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
                _save.run = new RunState();   // the schedule ends with the hero
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

            int xp = RewardFor(FightMode.Duel, fighter, _lastEnemy != null ? _lastEnemy : fighter, playerWon);
            if (playerWon) { fighter.wins++; _save.totalWins++; }
            var up = ProgressionRules.AddXp(fighter, xp, _rng);
            AdvanceRun(fighter);   // a finished duel counts as a schedule fight, won or lost
            string runLine = RunProgressLine();

            _save.hasFighter = true;
            _save.fighter = fighter;
            SaveSystem.Save(_save);

            ShowFighterStage(fighter);
            _resultToHub = true;
            if (playerWon)
                _ui.ShowResult("DUEL WON", new Color(1f, 0.82f, 0.25f), BuildVictoryText(fighter, up, _profBefore, fighter.displayName + " wins the duel!", runLine), "Continue");
            else
                _ui.ShowResult("DUEL LOST", new Color(1f, 0.6f, 0.25f),
                               BuildVictoryText(fighter, up, _profBefore, fighter.displayName + " yields, and nobody is hurt.\nYou still learn from it.", runLine), "Continue");
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

        // "2 of 3 fights done" / "FINALE unlocked!" for fights picked from the schedule, nothing for the old direct fights.
        string RunProgressLine()
        {
            if (!_fromSchedule || _save == null || _save.run == null) return null;
            if (_save.run.fightsDone >= fightsPerRun) return "FINALE unlocked!  Open the Fight Schedule";
            return "Schedule:  " + _save.run.fightsDone + " of " + fightsPerRun + " fights done";
        }

        static string BuildVictoryText(FighterData f, LevelUpResult up, int[] profBefore, string headline = null, string extra = null)
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
            if (!string.IsNullOrEmpty(extra)) sb.Append("\n").Append(extra);
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
