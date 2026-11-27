// File: Assets/Scripts/UI/BattleUIManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Combat;
using Core;

namespace UI
{
    /// <summary>
    /// The fight HUD: one portrait with a health bar per fighter.
    ///   1 against 1:   hero top left, opponent top right (as before).
    ///   Team fights:   your team in a column on the left (you on top, teammates under you, a bit smaller),
    ///                  the other team in a column on the right.
    ///   Last Hero Standing (everyone against everyone): one row of portraits along the top, you first.
    /// The extra portraits are copies of the two portraits already in the scene, made when the fight starts and removed
    /// when the next one begins, so nothing has to be added to the scene.
    /// </summary>
    public class BattleUIManager : MonoBehaviour
    {
        [Header("Main (Left)")]
        public RawImage mainPortrait;     // <-- RawImage for live RenderTexture
        public Image mainHealthFill;      // Filled Image (GREEN)
        public TMP_Text mainActionText;

        [Header("Enemy (Right)")]
        public RawImage enemyPortrait;    // <-- RawImage for live RenderTexture
        public Image enemyHealthFill;     // Filled Image (GREEN)
        public TMP_Text enemyActionText;

        [Header("Group fights (canvas units; the canvas is 1920 wide)")]
        [Tooltip("Teammates and the extra opponents are this size compared to the first portrait.")]
        public float teamScale = 0.7f;
        [Tooltip("Where the first extra portrait starts, down from the top.")]
        public float columnStartY = 350f;
        [Tooltip("Distance between the extra portraits in a column.")]
        public float columnStep = 250f;
        [Tooltip("Last Hero Standing: size of the portraits in the top row...")]
        public float rowScale = 0.8f;
        [Tooltip("...and the distance between them.")]
        public float rowStep = 340f;
        [Tooltip("Resolution of the picture inside the extra portraits.")]
        public int extraTextureSize = 160;

        sealed class Slot
        {
            public RectTransform root;
            public RawImage portrait;
            public Image fill;
            public TMP_Text action;
            public AutoPortraitBinder binder;
            public CanvasGroup group;
            public bool isClone;

            public MeleeAI unit;
            public Health hp;
            public Action<float, float> sub;
            public Coroutine fade;
        }

        // the two portraits that are in the scene
        Slot _main, _enemy;
        Vector2 _mainPos, _enemyPos;
        Vector3 _mainScale, _enemyScale;
        bool _baseReady;

        readonly List<Slot> _slots = new List<Slot>();

        void OnEnable()  => BattleEvents.OnActionText += HandleActionText;
        void OnDisable() => BattleEvents.OnActionText -= HandleActionText;
        void Start()     => Rebind();
        void OnDestroy() { ClearSlots(); }

        // ------------------------------------------------------------------ building
        void EnsureBase()
        {
            if (_baseReady) return;
            _main = MakeBase(mainPortrait, mainHealthFill, mainActionText);
            _enemy = MakeBase(enemyPortrait, enemyHealthFill, enemyActionText);
            if (_main != null) { _mainPos = _main.root.anchoredPosition; _mainScale = _main.root.localScale; }
            if (_enemy != null) { _enemyPos = _enemy.root.anchoredPosition; _enemyScale = _enemy.root.localScale; }
            _baseReady = true;
        }

        static Slot MakeBase(RawImage portrait, Image fill, TMP_Text action)
        {
            if (portrait == null) return null;
            var root = portrait.transform.parent as RectTransform;
            if (root == null) return null;
            var s = new Slot
            {
                root = root,
                portrait = portrait,
                fill = fill,
                action = action,
                binder = portrait.GetComponent<AutoPortraitBinder>(),
                isClone = false
            };
            s.group = root.GetComponent<CanvasGroup>();
            if (s.group == null) s.group = root.gameObject.AddComponent<CanvasGroup>();
            return s;
        }

        Slot CloneOf(Slot template, float scale, float x, float y)
        {
            var go = Instantiate(template.root.gameObject, template.root.parent);
            go.name = template.root.name + " (extra)";
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = new Vector2(x, y);

            var s = new Slot
            {
                root = rt,
                portrait = go.GetComponentInChildren<RawImage>(true),
                action = go.GetComponentInChildren<TMP_Text>(true),
                binder = go.GetComponentInChildren<AutoPortraitBinder>(true),
                isClone = true
            };
            foreach (var img in go.GetComponentsInChildren<Image>(true))
                if (img.name == "Green") { s.fill = img; break; }
            s.group = go.GetComponent<CanvasGroup>();
            if (s.group == null) s.group = go.AddComponent<CanvasGroup>();
            if (s.binder != null) s.binder.textureSize = extraTextureSize;
            if (s.action != null) s.action.alpha = 0f;
            return s;
        }

        void ClearSlots()
        {
            foreach (var s in _slots)
            {
                if (s == null) continue;
                if (s.hp != null && s.sub != null) s.hp.OnHealthChanged -= s.sub;
                if (s.isClone && s.root != null) Destroy(s.root.gameObject);
            }
            _slots.Clear();
        }

        // ------------------------------------------------------------------ binding
        /// <summary>Called when the fighters of a fight have appeared (and again if they change).</summary>
        public void Rebind()
        {
            EnsureBase();
            ClearSlots();

            // put the two scene portraits back the way they were
            if (_main != null) { _main.root.anchoredPosition = _mainPos; _main.root.localScale = _mainScale; _main.root.gameObject.SetActive(true); _main.group.alpha = 1f; }
            if (_enemy != null) { _enemy.root.anchoredPosition = _enemyPos; _enemy.root.localScale = _enemyScale; _enemy.root.gameObject.SetActive(true); _enemy.group.alpha = 1f; }
            if (_main == null || _enemy == null) return;

            var hero = FindHero();
            var allies = new List<MeleeAI>();
            var foes = new List<MeleeAI>();
            Gather(hero, allies, foes);

            bool ring = hero != null && hero.freeForAll;
            var left = new List<MeleeAI>();
            if (hero != null) left.Add(hero);
            left.AddRange(allies);

            if (ring)
            {
                // everyone against everyone: one row along the top, the hero first
                _enemy.root.gameObject.SetActive(false);
                var all = new List<MeleeAI>(left);
                all.AddRange(foes);
                float canvasW = ((RectTransform)transform).rect.width;
                float rowW = (all.Count - 1) * rowStep + 200f * rowScale;
                float x0 = (canvasW - rowW) * 0.5f - 50f * rowScale;
                for (int i = 0; i < all.Count; i++)
                {
                    float x = x0 + i * rowStep;
                    Slot s;
                    if (i == 0) { s = _main; s.root.localScale = Vector3.one * rowScale; s.root.anchoredPosition = new Vector2(x, 0f); }
                    else s = CloneOf(_main, rowScale, x, 0f);
                    Assign(s, all[i]);
                }
                return;
            }

            // two teams: left column (hero, then the teammates) and right column
            for (int i = 0; i < left.Count; i++)
            {
                Slot s = i == 0 ? _main : CloneOf(_main, teamScale, 0f, -(columnStartY + (i - 1) * columnStep));
                Assign(s, left[i]);
            }
            for (int i = 0; i < foes.Count; i++)
            {
                Slot s = i == 0 ? _enemy : CloneOf(_enemy, teamScale, 0f, -(columnStartY + (i - 1) * columnStep));
                Assign(s, foes[i]);
            }
            if (foes.Count == 0) _enemy.root.gameObject.SetActive(false);
        }

        void Assign(Slot s, MeleeAI u)
        {
            s.unit = u;
            s.hp = u != null ? u.GetComponent<Health>() : null;
            if (s.binder != null) s.binder.Bind(u);
            UpdateFill(s);
            if (s.hp != null)
            {
                s.sub = (cur, max) => UpdateFill(s);
                s.hp.OnHealthChanged += s.sub;
            }
            _slots.Add(s);
        }

        static void UpdateFill(Slot s)
        {
            if (s == null) return;
            if (s.fill != null && s.hp != null)
            {
                var max = Mathf.Max(0.0001f, s.hp.MaxHP);
                s.fill.fillAmount = Mathf.Clamp(s.hp.CurrentHP, 0f, max) / max;
            }
            // a fallen fighter's portrait is greyed out (but stays, so you can see who is out)
            if (s.group != null) s.group.alpha = s.hp != null && s.hp.IsDead ? 0.45f : 1f;
        }

        static MeleeAI FindHero()
        {
            MeleeAI first = null;
            foreach (var u in FindObjectsByType<MeleeAI>(FindObjectsSortMode.None))
            {
                if (u == null || u.team != Team.Main) continue;
                if (first == null) first = u;
                var id = u.GetComponent<Fighters.FighterIdentity>();
                if (id != null && id.Data != null && id.Data == Fighters.FighterSession.PlayerFighter) return u;
            }
            return first;
        }

        // Teammates and opponents, in the order they were spawned (the arena keeps the lists); without the arena, whoever is there.
        static void Gather(MeleeAI hero, List<MeleeAI> allies, List<MeleeAI> foes)
        {
            var spawner = FindFirstObjectByType<Fighters.ArenaFighterSpawner>();
            if (spawner != null && spawner.Foes != null && spawner.Foes.Count > 0)
            {
                foreach (var id in spawner.Allies) { var a = id != null ? id.GetComponent<MeleeAI>() : null; if (a != null && a != hero) allies.Add(a); }
                foreach (var id in spawner.Foes)   { var f = id != null ? id.GetComponent<MeleeAI>() : null; if (f != null && f != hero) foes.Add(f); }
                return;
            }

            foreach (var u in FindObjectsByType<MeleeAI>(FindObjectsSortMode.None))
            {
                if (u == null || u == hero) continue;
                bool hostile = hero != null ? MeleeAI.IsHostile(hero, u) : u.team == Team.Enemy;
                (hostile ? foes : allies).Add(u);
            }
        }

        // ------------------------------------------------------------------ action text
        void HandleActionText(MeleeAI who, string text)
        {
            foreach (var s in _slots)
            {
                if (s.unit != who || s.action == null) continue;
                if (s.fade != null) StopCoroutine(s.fade);
                s.fade = StartCoroutine(FadeMessage(s.action, text, 0.15f, 0.9f, 0.6f));
                return;
            }
        }

        IEnumerator FadeMessage(TMP_Text label, string text, float fadeIn, float hold, float fadeOut)
        {
            label.text = text;

            for (float t = 0f; t < fadeIn; t += Time.deltaTime) { label.alpha = Mathf.Lerp(0f, 1f, t / fadeIn); yield return null; }
            label.alpha = 1f;

            yield return new WaitForSeconds(hold);

            for (float t = 0f; t < fadeOut; t += Time.deltaTime) { label.alpha = Mathf.Lerp(1f, 0f, t / fadeOut); yield return null; }
            label.alpha = 0f;
        }
    }
}
