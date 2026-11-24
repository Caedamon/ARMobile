#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Combat;
using Fighters;
using UnityEditor;
using UnityEngine;
using Weaponary;

// Put in Assets/Scripts/Editor/
// Menu: Tools > Kaiju > Combat > Balance Simulator
//
// Plays thousands of simplified duels between every weapon loadout (and unarmed) in a second or two and shows win rates.
// It uses your real weapon assets, movesets, animation clip lengths, stats formulas and the Combat Tuning asset.
// It is a MODEL, not the real fight: both fighters stand in range the whole time, so it ignores walking, spacing, missed
// swings and weapon pickup. Trust it for comparing styles with each other, not for exact win rates.
namespace KaijuTools
{
    public sealed class BalanceSimulator : EditorWindow
    {
        CombatTuning _tuning;
        int _fights = 100;
        int _statA = 5, _statB = 5;
        float _unarmedDamage = 25f;
        Vector2 _scroll;
        string _report = "Press Run.";

        [MenuItem("Tools/Kaiju/Combat/Balance Simulator")]
        public static void Open() { GetWindow<BalanceSimulator>("Balance Simulator"); }

        void OnEnable()
        {
            if (_tuning == null) _tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>("Assets/Data/CombatTuning.asset");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Simplified duels with your real numbers. Good for comparing styles, not exact win rates (no walking, spacing or missed swings).", MessageType.Info);
            _tuning = (CombatTuning)EditorGUILayout.ObjectField("Combat Tuning (empty = defaults)", _tuning, typeof(CombatTuning), false);
            _fights = EditorGUILayout.IntSlider("Fights per pairing", _fights, 20, 600);
            _statA = EditorGUILayout.IntSlider("Stats of the row fighter (all four)", _statA, 1, 15);
            _statB = EditorGUILayout.IntSlider("Stats of the opponent (all four)", _statB, 1, 15);
            _unarmedDamage = EditorGUILayout.FloatField("Unarmed base damage", _unarmedDamage);

            if (GUILayout.Button("Run", GUILayout.Height(28))) Run();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.SelectableLabel(_report, EditorStyles.textArea, GUILayout.ExpandHeight(true), GUILayout.MinHeight(400));
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ setup
        sealed class Loadout
        {
            public string name;
            public string style;
            public float baseDamage;
            public bool shield;
            public Moveset set;
        }

        sealed class F
        {
            public Loadout l;
            public float might, ego, insight, luck;
            public StyleTuning sty;
            public float hp, cd, nextBlock, nextDodge, until, hitT, endT, dstart;
            public char state = 'E';   // E engage, A attacking, B blocking, D dodging, H hit
            public AttackMove last, move;
            public bool hit;
        }

        void Run()
        {
            var tuning = _tuning != null ? _tuning : CombatTuning.Default();

            var loadouts = new List<Loadout>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.moveset == null) continue;
                loadouts.Add(new Loadout
                {
                    name = def.displayName,
                    style = ProgressionRules.StyleKey(def.weaponClass),
                    baseDamage = Mathf.Max(1f, def.baseDamage) * Mathf.Max(0.1f, def.damageScalar),
                    shield = def.weaponClass == WeaponClass.OneHandAndShield || def.weaponClass == WeaponClass.ShieldOnly,
                    set = def.moveset
                });
            }
            Moveset unarmed = null;
            foreach (var guid in AssetDatabase.FindAssets("Unarmed t:Moveset"))
            {
                unarmed = AssetDatabase.LoadAssetAtPath<Moveset>(AssetDatabase.GUIDToAssetPath(guid));
                if (unarmed != null) break;
            }
            if (unarmed != null)
                loadouts.Add(new Loadout { name = "Unarmed", style = ProgressionRules.Unarmed, baseDamage = _unarmedDamage, shield = false, set = unarmed });

            if (loadouts.Count < 2) { _report = "Found fewer than 2 weapons (WeaponDefinition assets with a moveset)."; return; }

            int n = loadouts.Count;
            var rng = new System.Random(12345);
            var score = new double[n];
            var games = new int[n];
            var wins = new double[n, n];

            try
            {
                for (int i = 0; i < n; i++)
                {
                    EditorUtility.DisplayProgressBar("Balance Simulator", loadouts[i].name, i / (float)n);
                    for (int j = 0; j < n; j++)
                    {
                        double w = 0;
                        for (int k = 0; k < _fights; k++) w += Duel(loadouts[i], loadouts[j], _statA, _statB, tuning, rng);
                        wins[i, j] = w / _fights;
                        if (i != j) { score[i] += w / _fights; games[i]++; }
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            var sb = new StringBuilder();
            sb.AppendLine("Tuning: " + (_tuning != null ? _tuning.name : "built-in defaults") + "   |   stats " + _statA + " vs " + _statB + "   |   " + _fights + " fights per pairing");
            sb.AppendLine();
            sb.AppendLine("Average win rate against every other loadout (50% = even):");

            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => (score[b] / games[b]).CompareTo(score[a] / games[a]));
            foreach (int i in order) sb.AppendLine("  " + (score[i] / games[i] * 100.0).ToString("0.0").PadLeft(5) + "%   " + loadouts[i].name);

            sb.AppendLine();
            sb.AppendLine("By style (average of its loadouts):");
            var styleSum = new Dictionary<string, double>();
            var styleCount = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string s = loadouts[i].style;
                if (!styleSum.ContainsKey(s)) { styleSum[s] = 0; styleCount[s] = 0; }
                styleSum[s] += score[i] / games[i];
                styleCount[s]++;
            }
            foreach (var kv in styleSum) sb.AppendLine("  " + (kv.Value / styleCount[kv.Key] * 100.0).ToString("0.0").PadLeft(5) + "%   " + ProgressionRules.StyleLabel(kv.Key));

            sb.AppendLine();
            sb.AppendLine("Head to head (row style beats column style):");
            string[] styles = { ProgressionRules.Unarmed, ProgressionRules.OneHand, ProgressionRules.Shield, ProgressionRules.TwoHand, ProgressionRules.Dual };
            sb.Append("".PadRight(16));
            foreach (var s in styles) sb.Append(ProgressionRules.StyleLabel(s).PadRight(16));
            sb.AppendLine();
            foreach (var a in styles)
            {
                sb.Append(ProgressionRules.StyleLabel(a).PadRight(16));
                foreach (var b in styles)
                {
                    double sum = 0; int c = 0;
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                            if (i != j && loadouts[i].style == a && loadouts[j].style == b) { sum += wins[i, j]; c++; }
                    sb.Append((c > 0 ? (sum / c * 100.0).ToString("0") + "%" : "-").PadRight(16));
                }
                sb.AppendLine();
            }

            _report = sb.ToString();
            Debug.Log("[Balance]\n" + _report);
            Repaint();
        }

        // ------------------------------------------------------------------ one simplified duel (1 = A wins, 0 = B wins)
        static float Duel(Loadout la, Loadout lb, int statA, int statB, CombatTuning t, System.Random rng)
        {
            var fa = MakeFighter(la, statA, t, rng);
            var fb = MakeFighter(lb, statB, t, rng);
            var fs = new[] { fa, fb };
            const float dt = 0.01f;

            for (float now = 0f; now < 120f; now += dt)
            {
                for (int i = 0; i < 2; i++)
                {
                    var a = fs[i];
                    var d = fs[1 - i];
                    if (a.hp <= 0f) continue;

                    switch (a.state)
                    {
                        case 'E':
                            a.cd -= dt;
                            if (a.cd <= 0f && d.hp > 0f) StartAttack(a, d, now, t, rng);
                            break;

                        case 'A':
                            if (!a.hit && now >= a.hitT)
                            {
                                a.hit = true;
                                ResolveHit(a, d, now, t, rng);
                            }
                            if (a.state == 'A' && now >= a.endT)
                            {
                                float insightFactor = Mathf.Clamp(1f + 0.04f * (a.insight - 5f), 0.6f, 1.6f);
                                var range = a.l.set.cooldownRange;
                                a.cd = Lerp(rng, range.x, range.y) * a.sty.cooldownMul / insightFactor;
                                a.state = 'E';
                            }
                            break;

                        default:   // B, D, H
                            if (now >= a.until)
                            {
                                if (a.state == 'H') a.cd = Mathf.Max(a.cd, 0.15f);
                                a.state = 'E';
                            }
                            break;
                    }
                }
                if (fa.hp <= 0f || fb.hp <= 0f) break;
            }

            if (fa.hp <= 0f && fb.hp <= 0f) return 0.5f;
            if (fb.hp <= 0f) return 1f;
            if (fa.hp <= 0f) return 0f;
            return fa.hp == fb.hp ? 0.5f : (fa.hp > fb.hp ? 1f : 0f);
        }

        static F MakeFighter(Loadout l, int stat, CombatTuning t, System.Random rng)
        {
            return new F
            {
                l = l, might = stat, ego = stat, insight = stat, luck = 5f,
                sty = t.For(l.style),
                hp = StatRules.HpBase + StatRules.HpPerMight * stat,
                cd = Lerp(rng, 0f, 0.6f)
            };
        }

        static void StartAttack(F a, F d, float now, CombatTuning t, System.Random rng)
        {
            var atk = a.l.set.attacks;
            float total = 0f;
            var cand = new List<AttackMove>();
            foreach (var m in atk)
                if (m != null && m.weight > 0f && !(m == a.last && atk.Count > 1)) { cand.Add(m); total += m.weight; }
            if (cand.Count == 0) { a.cd = 0.5f; return; }

            float r = Lerp(rng, 0f, total);
            AttackMove move = cand[cand.Count - 1];
            foreach (var m in cand) { r -= m.weight; if (r <= 0f) { move = m; break; } }

            a.last = a.move = move;
            float clip = move.clip != null ? move.clip.length : 0.7f;
            float speed = Mathf.Max(0.3f, move.speed * a.sty.speedMul * (1f + t.attackSpeedPerInsight * (a.insight - 5f)));
            float dur = clip / speed;
            a.hitT = now + dur * move.hitAt;
            a.endT = now + dur * move.endAt;
            a.hit = false;
            a.state = 'A';

            // the defender gets a chance to block or dodge
            if (d.state != 'E') return;
            float hitDelay = dur * move.hitAt;
            if (d.l.shield && now >= d.nextBlock && Lerp(rng, 0f, 1f) <= t.BlockChance(d.insight))
            {
                float bs = Mathf.Max(0.5f, hitDelay + 0.4f);
                d.nextBlock = now + bs + t.blockCooldown;
                d.state = 'B';
                d.until = now + bs;
                return;
            }
            if (now >= d.nextDodge && Lerp(rng, 0f, 1f) <= t.DodgeChance(d.insight))
            {
                d.nextDodge = now + t.dodgeCooldown;
                d.state = 'D';
                d.dstart = now;
                d.until = now + 0.55f;
            }
        }

        static void ResolveHit(F a, F d, float now, CombatTuning t, System.Random rng)
        {
            if (d.state == 'D' && now - d.dstart < 0.45f) return;   // dodged

            float dmg = a.l.baseDamage * (1f + 0.05f * a.might) * Lerp(rng, 0.9f, 1.1f);
            float crit = Mathf.Clamp01(0.05f + 0.005f * (a.insight + a.luck));
            if (Lerp(rng, 0f, 1f) < crit) dmg *= 1.5f + 0.01f * a.insight;
            dmg *= a.move.damageMul * a.sty.damageMul;
            dmg *= t.IncomingFactor(d.state == 'B', d.l.shield);
            d.hp -= dmg;

            if (d.hp > 0f && d.state != 'B')
            {
                bool resist = d.state == 'A' && Lerp(rng, 0f, 1f) < d.ego * t.egoPoisePerPoint;
                if (!resist)
                {
                    d.state = 'H';
                    d.until = now + 0.35f * Mathf.Max(0.2f, a.move.stagger);
                }
            }
        }

        static float Lerp(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    }
}
#endif
