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

        /// <summary>
        /// The "Rules of the Arena" pages: how a run works, then one page for every kind of fight, then the kinds of enemy.
        /// The numbers are the defaults of the GameController (3 battles, group sizes, XP shares): if you change those, change the words.
        /// </summary>
        public static BookTextPage[] DefaultHowToPlay()
        {
            return new[]
            {
                new BookTextPage("Rules of the Arena",
                    new BookTextEntry("The Arena",
                        "The game is centered around Arena fighting. Pick your fighter, fight three battles, then choose between three different kinds of epic end-game fights."),
                    new BookTextEntry("The Schedule",
                        "Before every battle you choose one of three fights. Win to move on. A Duel counts too, win or lose."),
                    new BookTextEntry("The Finale",
                        "After three battles comes the Finale. Win it and you are crowned Champion, with a bonus stat point. Then a new schedule begins.")),

                new BookTextPage("Duel",
                    new BookTextEntry("How it works",
                        "One against one, and nobody dies. When a fighter has had enough, they kneel and the duel is over."),
                    new BookTextEntry("Reward",
                        "Half the XP of a normal win, and a little even if you lose. Weapon training grows faster in a duel."),
                    new BookTextEntry("Good for",
                        "Training, and a safe way forward. A finished duel counts as one of your three battles.")),

                new BookTextPage("Death Match",
                    new BookTextEntry("How it works",
                        "One against one, to the end. There is no yielding: the fight is over when someone falls."),
                    new BookTextEntry("Stakes",
                        "The loser dies and is written into the Memento Mori. Your fighter is gone for good."),
                    new BookTextEntry("Reward",
                        "The full XP of a win, and the victory moves you closer to the Finale.")),

                new BookTextPage("Group Fight",
                    new BookTextEntry("How it works",
                        "You and a teammate against two opponents. The fight goes on if you fall, and ends when one whole team is down."),
                    new BookTextEntry("If you fall",
                        "If your team still wins, you live, but it counts as a loss: the schedule starts over and there is no XP. If your team falls too, you die."),
                    new BookTextEntry("Reward",
                        "XP for every opponent beaten, a little less than in a one-on-one.")),

                new BookTextPage("Boss Fight",
                    new BookTextEntry("Finale",
                        "One of the three end-game fights. You face a Boss: one fighter of the best quality, tougher than you, and clever."),
                    new BookTextEntry("How it works",
                        "One against one, to the death. A boss grows more furious as it is hurt, so do not get comfortable."),
                    new BookTextEntry("Reward",
                        "More XP than any single fight, and the crown of Champion. Lose, and you join the Memento Mori.")),

                new BookTextPage("Group Death Match",
                    new BookTextEntry("Finale",
                        "One of the three end-game fights. Your team of three stands against a team of three."),
                    new BookTextEntry("How it works",
                        "Same rules as a Group Fight: the battle goes on after you fall. But the stakes are higher, and it is the Finale."),
                    new BookTextEntry("Reward",
                        "If your team wins, even if you fell, the Finale is won. If your team falls, so do you.")),

                new BookTextPage("Last Hero Standing",
                    new BookTextEntry("Finale",
                        "One of the three end-game fights. Five fighters, no teams: everybody fights everybody."),
                    new BookTextEntry("How it works",
                        "Only one survives. Stay on your feet while the others wear each other down, and pick your moment."),
                    new BookTextEntry("Reward",
                        "The Champion's crown, and XP for every rival beaten. If you fall, the fight is lost.")),

                new BookTextPage("Know Your Enemy",
                    new BookTextEntry("The brawlers",
                        "Most foes just grab a weapon and rush you. They hit hard, but they are easy to read."),
                    new BookTextEntry("The clever ones",
                        "Better fighters circle, wait for you to slip, and strike back the moment you miss. Do not swing wildly."),
                    new BookTextEntry("The bosses",
                        "A boss fights like a clever one, and gets stronger as it loses health. It roars when it changes, and that is your moment.")),
            };
        }

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
                    new BookTextEntry("Death",
                        "In a deadly fight the loser dies, and is written into the Memento Mori. Duels never kill. The Rules of the Arena explain every kind of fight."),
                    new BookTextEntry("Quality",
                        "Pathetic, Normal, High quality or Elite: it is only a starting point. Quality does not decide a fighter's fate.")),

                new BookTextPage("Luck",
                    new BookTextEntry("LUCK",
                        "Some fighters are born lucky and some are not.\n\nNobody knows exactly what luck does. The book will not say.")),
            };
        }
    }
}
