using System;
using System.Collections.Generic;
using Fighters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The fight schedule screen of the menus ("pick one of 3"). This is the second half of GameUI (a partial class), kept in
    /// its own file so GameUI.cs does not grow even more. Everything is built by AddScheduleUi (Tools > Kaiju > Schedule >
    /// Set Up Fight Schedule puts it into the GameUI prefab, where you can restyle it) and as a run-time fallback.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>The hub's "Fight Schedule" button.</summary>
        public event Action ScheduleClicked;
        /// <summary>"Back" on the schedule screen.</summary>
        public event Action ScheduleBackClicked;
        /// <summary>One of the three cards was tapped (0, 1 or 2).</summary>
        public event Action<int> ScheduleCardPicked;

        [Header("Fight schedule (made by Set Up Fight Schedule)")]
        [SerializeField] Button _scheduleButton, _scheduleBackButton;
        [SerializeField] GameObject _schedulePanel;
        [SerializeField] TMP_Text _scheduleTitle, _scheduleSub;
        [SerializeField] Button[] _scheduleCard = new Button[3];
        [SerializeField] TMP_Text[] _scHead = new TMP_Text[3];
        [SerializeField] TMP_Text[] _scName = new TMP_Text[3];
        [SerializeField] TMP_Text[] _scInfo = new TMP_Text[3];
        [SerializeField] TMP_Text[] _scTag = new TMP_Text[3];
        [SerializeField] TMP_Text[] _scNote = new TMP_Text[3];
        [SerializeField] TMP_Text[] _scXp = new TMP_Text[3];

        static readonly Color CardDeath = new Color(0.34f, 0.13f, 0.15f, 1f);
        static readonly Color CardDuel  = new Color(0.14f, 0.24f, 0.40f, 1f);
        static readonly Color CardBoss  = new Color(0.40f, 0.29f, 0.07f, 1f);
        static readonly Color CardLock  = new Color(0.20f, 0.21f, 0.25f, 1f);
        static readonly Color CardGroup = new Color(0.12f, 0.32f, 0.26f, 1f);
        static readonly Color CardBrawl = new Color(0.30f, 0.14f, 0.38f, 1f);
        static readonly Color Gold      = new Color(1f, 0.82f, 0.25f, 1f);

        public bool HasScheduleUi()
        {
            if (_schedulePanel == null || _scheduleBackButton == null || _scheduleTitle == null || _scheduleSub == null) return false;
            if (!Full(_scheduleCard) || !Full(_scHead) || !Full(_scName) || !Full(_scInfo) || !Full(_scTag) || !Full(_scNote) || !Full(_scXp)) return false;
            return _scheduleButton != null || _hubPanel == null;
        }

        static bool Full(UnityEngine.Object[] items)
        {
            if (items == null || items.Length != 3) return false;
            for (int i = 0; i < 3; i++) if (items[i] == null) return false;
            return true;
        }

        /// <summary>
        /// Adds the "Fight Schedule" button to the hub (the old Death Match / Duel / Group buttons are switched off, not deleted:
        /// switch them back on in the prefab if you want them) and the schedule screen with its three cards.
        /// Only creates what is missing.
        /// </summary>
        public void AddScheduleUi()
        {
            if (_safe == null) return;
            TMP_Text l;

            if (_hubPanel != null)
            {
                if (_scheduleButton == null)
                {
                    var found = _hubPanel.transform.Find("ScheduleButton");
                    if (found != null) _scheduleButton = found.GetComponent<Button>();
                }
                if (_scheduleButton == null)
                    _scheduleButton = NewButton(_hubPanel.transform, "ScheduleButton", "Fight Schedule", 0.5f, 0.24f, new Vector2(760f, 140f), Green, out l);

                string[] old = { "DeathMatchButton", "DuelButton", "GroupButton" };
                foreach (var n in old)
                {
                    var t = _hubPanel.transform.Find(n);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }

            if (_schedulePanel == null)
            {
                _schedulePanel = NewPanel("SchedulePanel", new Color(0.03f, 0.04f, 0.07f, 0.72f), true);
                var rt = _schedulePanel.transform;

                _scheduleTitle = NewText(rt, "ScheduleTitle", "FIGHT 1 OF 3", 80f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
                Anchor(_scheduleTitle.rectTransform, 0.5f, 0.93f, new Vector2(1000f, 120f));
                _scheduleSub = NewText(rt, "ScheduleSub", "Pick your next opponent", 40f, SoftWhite, TextAlignmentOptions.Center);
                Anchor(_scheduleSub.rectTransform, 0.5f, 0.875f, new Vector2(1000f, 70f));

                _scheduleCard = new Button[3];
                _scHead = new TMP_Text[3]; _scName = new TMP_Text[3]; _scInfo = new TMP_Text[3];
                _scTag = new TMP_Text[3]; _scNote = new TMP_Text[3]; _scXp = new TMP_Text[3];

                for (int i = 0; i < 3; i++)
                {
                    float y = 0.72f - i * 0.20f;
                    var card = NewButton(rt, "ScheduleCard" + i, "DEATH MATCH", 0.5f, y, new Vector2(960f, 340f), CardDeath, out l);
                    _scheduleCard[i] = card;

                    _scHead[i] = l;
                    l.fontSize = 54f;
                    l.alignment = TextAlignmentOptions.Left;
                    Frac(l.rectTransform, 0.04f, 0.66f, 0.62f, 0.96f);

                    var ct = card.transform;
                    _scName[i] = NewText(ct, "CardName", "", 46f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
                    Frac(_scName[i].rectTransform, 0.04f, 0.40f, 0.96f, 0.66f);
                    _scInfo[i] = NewText(ct, "CardInfo", "", 34f, SoftWhite, TextAlignmentOptions.Left);
                    Frac(_scInfo[i].rectTransform, 0.04f, 0.20f, 0.96f, 0.42f);
                    _scTag[i] = NewText(ct, "CardTag", "", 46f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                    Frac(_scTag[i].rectTransform, 0.62f, 0.66f, 0.96f, 0.96f);
                    _scNote[i] = NewText(ct, "CardNote", "", 32f, SoftWhite, TextAlignmentOptions.Left);
                    Frac(_scNote[i].rectTransform, 0.04f, 0.03f, 0.58f, 0.22f);
                    _scXp[i] = NewText(ct, "CardXp", "", 38f, BarFill, TextAlignmentOptions.Right, FontStyles.Bold);
                    Frac(_scXp[i].rectTransform, 0.58f, 0.03f, 0.96f, 0.22f);
                }

                _scheduleBackButton = NewButton(rt, "ScheduleBackButton", "Back", 0.5f, 0.10f, new Vector2(720f, 130f), Blue, out l);
                _schedulePanel.SetActive(false);
            }
        }

        void WireScheduleButtons()
        {
            Wire(_scheduleButton, () => Raise(ScheduleClicked));
            Wire(_scheduleBackButton, () => Raise(ScheduleBackClicked));
            if (_scheduleCard != null)
                for (int i = 0; i < _scheduleCard.Length; i++)
                {
                    int idx = i;
                    Wire(_scheduleCard[i], () => Raise(() => { if (ScheduleCardPicked != null) ScheduleCardPicked(idx); }));
                }
        }

        /// <summary>The hub button says where you are in the schedule ("Fight 2 of 3", "Finale").</summary>
        public void SetScheduleLabel(string label, bool finale)
        {
            if (_scheduleButton == null) return;
            var lab = _scheduleButton.GetComponentInChildren<TMP_Text>();
            if (lab != null) lab.text = label;
            var img = _scheduleButton.GetComponent<Image>();
            if (img != null) img.color = finale ? new Color(0.62f, 0.45f, 0.08f, 1f) : Green;
        }

        public bool ScheduleOpen { get { return _schedulePanel != null && _schedulePanel.activeSelf; } }

        /// <summary>Shows the schedule screen. xpPreview[i] is the XP a win on card i would give.</summary>
        public void ShowSchedule(string title, string subtitle, List<FightCard> cards, FighterData hero, int[] xpPreview)
        {
            if (_schedulePanel == null) return;
            HideAll();
            _scheduleTitle.text = title;
            _scheduleSub.text = subtitle;

            for (int i = 0; i < 3; i++)
            {
                bool has = cards != null && i < cards.Count && cards[i] != null;
                _scheduleCard[i].gameObject.SetActive(has);
                if (!has) continue;

                var c = cards[i];
                var img = _scheduleCard[i].GetComponent<Image>();

                if (c.locked || c.enemy == null)
                {
                    _scheduleCard[i].interactable = false;
                    img.color = CardLock;
                    _scHead[i].text = string.IsNullOrEmpty(c.lockedTitle) ? "LOCKED" : c.lockedTitle;
                    _scHead[i].color = new Color(0.6f, 0.62f, 0.68f, 1f);
                    _scName[i].text = "Coming soon";
                    _scName[i].color = new Color(0.6f, 0.62f, 0.68f, 1f);
                    _scInfo[i].text = "";
                    _scTag[i].text = "";
                    _scNote[i].text = "";
                    _scXp[i].text = "";
                    continue;
                }

                var e = c.enemy;
                int danger = RunPlanner.Danger(hero, c);
                _scheduleCard[i].interactable = true;
                img.color = CardColor(c.mode);

                _scHead[i].text = RunPlanner.ModeLabel(c.mode);
                _scHead[i].color = HeadColor(c.mode);

                // long names and team lines shrink to fit instead of running off the card
                _scHead[i].enableAutoSizing = true; _scHead[i].fontSizeMin = 30f; _scHead[i].fontSizeMax = 54f;
                _scName[i].enableAutoSizing = true; _scName[i].fontSizeMin = 26f; _scName[i].fontSizeMax = 46f;
                _scInfo[i].enableAutoSizing = true; _scInfo[i].fontSizeMin = 22f; _scInfo[i].fontSizeMax = 34f;

                _scName[i].text = RunPlanner.CardName(c);
                _scName[i].color = FighterTiers.Tint(e.tier);
                _scInfo[i].text = RunPlanner.CardInfo(c);
                _scTag[i].text = RunPlanner.DangerLabel(danger);
                _scTag[i].color = DangerColor(danger);
                _scNote[i].text = RunPlanner.RulesLine(c);
                int xp = xpPreview != null && i < xpPreview.Length ? xpPreview[i] : 0;
                _scXp[i].text = xp > 0 ? "XP  " + xp : "";
            }

            _schedulePanel.transform.SetAsLastSibling();
            _schedulePanel.SetActive(true);
        }

        static Color CardColor(FightMode m)
        {
            switch (m)
            {
                case FightMode.Duel:            return CardDuel;
                case FightMode.Boss:            return CardBoss;
                case FightMode.Group:           return CardGroup;
                case FightMode.GroupDeathMatch: return CardGroup;
                case FightMode.LastHero:        return CardBrawl;
                default:                        return CardDeath;
            }
        }

        static Color HeadColor(FightMode m)
        {
            switch (m)
            {
                case FightMode.Duel:            return new Color(0.55f, 0.78f, 1f);
                case FightMode.Boss:            return Gold;
                case FightMode.Group:           return new Color(0.55f, 0.95f, 0.78f);
                case FightMode.GroupDeathMatch: return new Color(0.55f, 0.95f, 0.78f);
                case FightMode.LastHero:        return new Color(0.88f, 0.65f, 1f);
                default:                        return new Color(1f, 0.55f, 0.50f);
            }
        }

        static Color DangerColor(int danger)
        {
            switch (danger)
            {
                case 0:  return new Color(0.50f, 0.90f, 0.55f);
                case 1:  return new Color(1.00f, 1.00f, 1.00f);
                case 2:  return new Color(1.00f, 0.72f, 0.30f);
                default: return new Color(1.00f, 0.35f, 0.32f);
            }
        }
    }
}
