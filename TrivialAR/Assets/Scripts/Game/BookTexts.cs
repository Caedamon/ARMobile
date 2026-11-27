using System;
using UnityEngine;

namespace Game
{
    /// <summary>One heading with a paragraph under it (a stat, a rule...).</summary>
    [Serializable]
    public sealed class BookTextEntry
    {
        public string heading = "";
        [TextArea(2, 8)] public string body = "";

        public BookTextEntry() { }
        public BookTextEntry(string heading, string body) { this.heading = heading; this.body = body; }
    }

    /// <summary>One page of explanation text in the Book: a title and up to three entries.</summary>
    [Serializable]
    public sealed class BookTextPage
    {
        public string title = "";
        public BookTextEntry[] entries = new BookTextEntry[0];

        public BookTextPage() { }
        public BookTextPage(string title, params BookTextEntry[] entries) { this.title = title; this.entries = entries; }
    }

    /// <summary>
    /// The words on the Stats pages of the Book. These are only the starting texts: after the first time the BookStage prefab is
    /// saved, edit them on the BookStage in the Inspector ("Stats Pages"). The numbers are copied from the combat balance
    /// (CombatTuning), so if you change the balance, change the words too.
    /// </summary>
    public static class BookTexts
    {
        public static BookTextPage[] DefaultStats()
        {
            return new[]
            {
                new BookTextPage("Stats",
                    new BookTextEntry("MIGHT",
                        "Raw strength and toughness.\n\nEvery point makes each hit land about 5% harder AND adds 16 hit points to the health bar."),
                    new BookTextEntry("EGO",
                        "Stubborn pride and stage presence.\n\nEvery point gives a 4% chance to shrug off most of the knockback in the middle of an attack. Proud fighters also lean toward picking up a shield.")),

                new BookTextPage("Stats",
                    new BookTextEntry("EDUCATION",
                        "Book smarts and training.\n\nEvery point adds about 5% more XP from a win and speeds up weapon training by roughly 6%. It does nothing in the swing itself; it pays off over a whole career."),
                    new BookTextEntry("INSIGHT",
                        "Reading the fight.\n\nEvery point sharpens crit chance (+0.5%) and crit damage (+1%), attack tempo (+2%), dodge (+2.5%) and shield-block (+3%) chances, and lets a fighter spot loose weapons from further away. Clever fighters like dual wielding.")),

                new BookTextPage("Weapon training",
                    new BookTextEntry("Every style",
                        "You train a fighting style by landing hits and kills with it (duels train you faster). Every level of training adds +5% damage with that style. The top level is 10."),
                    new BookTextEntry("One-handed",
                        "Each level adds +1.5% chance to land a critical hit."),
                    new BookTextEntry("Sword & shield",
                        "Each level adds +2% chance to block with the shield.")),

                new BookTextPage("Weapon training",
                    new BookTextEntry("Two-handed",
                        "Each level adds +3% poise: a big weapon is harder to knock off balance."),
                    new BookTextEntry("Dual wield",
                        "Each level adds +2% attack speed."),
                    new BookTextEntry("Unarmed",
                        "Each level adds +3% chance to dodge a blow.")),

                new BookTextPage("Levels and fate",
                    new BookTextEntry("Level up",
                        "Every level gives one free stat point. Spend it under Stats on the hub."),
                    new BookTextEntry("Death Match",
                        "The loser dies, and is written into the Memento Mori."),
                    new BookTextEntry("Duel",
                        "Nobody dies. The loser kneels, and you keep your fighter. A good way to train.")),

                new BookTextPage("Luck",
                    new BookTextEntry("LUCK",
                        "Some fighters are born lucky and some are not.\n\nNobody knows exactly what luck does. The book will not say.")),
            };
        }
    }
}
