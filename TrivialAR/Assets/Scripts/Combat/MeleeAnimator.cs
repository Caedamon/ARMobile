using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeAnimator : MonoBehaviour
    {
        [SerializeField] bool useAnimator;

        public string speedParam = "Speed";
        public string attackTrigger = "Attack";
        public string heavyBool = "Heavy";
        public string blockBool = "Block";
        public string hitTrigger = "Hit";
        public string dieTrigger = "Die";
        public string dodgeTrigger = "Dodge";

        public string clipIdle = "Idle";
        public string clipWalk = "Walking_A";
        public string clipAttackLight = "1H_Melee_Attack_Slice_Horizontal";
        public string clipAttackHeavy = "2H_Melee_Attack_Spinning";
        public string clipBlockLoop = "Blocking";
        public string clipHit = "Hit_A";
        public string clipDie = "Death_A";
        public string clipDodge = "Roll";

        public float idleDuration = 0.25f;
        public float walkTickDuration = 0.50f;
        public float attackLightDuration = 0.70f;
        public float attackHeavyDuration = 0.95f;
        public float blockEnterExitDuration = 0.20f;
        public float hitDuration = 0.45f;
        public float dieDuration = 1.20f;
        public float dodgeDuration = 0.60f;

        static readonly string PH_AtkL = "Base/Attack_Light";
        static readonly string PH_AtkH = "Base/Attack_Heavy";
        static readonly string PH_Hit  = "Base/Hit";
        static readonly string PH_Die  = "Base/Die";

        Animator _anim;
        UnityEngine.Animation _legacy; // fully qualified to avoid namespace clash
        bool _animReady;
        float _lastSpeed;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _legacy = GetComponent<UnityEngine.Animation>();
            _animReady = (_anim != null && _anim.runtimeAnimatorController != null);
            useAnimator = _animReady;
            RecalculateDurationsFromController();
        }

        public void RecalculateDurationsFromController()
        {
            if (!_animReady) return;
            var roc = _anim.runtimeAnimatorController; if (!roc) return;

            var map = new Dictionary<string, AnimationClip>();
            if (roc is AnimatorOverrideController aoc)
            {
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(pairs);
                foreach (var kv in pairs) if (kv.Key) map[kv.Key.name] = kv.Value ? kv.Value : kv.Key;
            }
            else
            {
                foreach (var c in roc.animationClips) if (c) map[c.name] = c;
            }

            attackLightDuration = GetLen(map, PH_AtkL, attackLightDuration);
            attackHeavyDuration = GetLen(map, PH_AtkH, attackHeavyDuration, PH_AtkL);
            hitDuration         = GetLen(map, PH_Hit,  hitDuration);
            dieDuration         = GetLen(map, PH_Die,  dieDuration, minLen: 0.6f);
        }

        float GetLen(Dictionary<string, AnimationClip> map, string key, float current, string fb = null, float minLen = 0.35f)
        {
            if (map != null && map.TryGetValue(key, out var c) && c) return Mathf.Max(minLen, c.length);
            if (!string.IsNullOrEmpty(fb) && map != null && map.TryGetValue(fb, out var d) && d) return Mathf.Max(minLen, d.length);
            return current;
        }

        public float PlayIdle()
        {
            if (useAnimator) SetSpeed(0f);
            else if (_legacy != null) CrossFade(clipIdle, 0.1f, true);
            return idleDuration;
        }

        public float PlayWalk(float speed01 = 1f)
        {
            if (useAnimator) SetSpeed(Mathf.Clamp01(speed01));
            else if (_legacy != null) CrossFade(clipWalk, 0.1f, true);
            return walkTickDuration;
        }

        public void EnsureIdlePlaying()
        {
            if (useAnimator) { if (_lastSpeed > 0f) SetSpeed(0f); }
            else if (_legacy != null && !_legacy.IsPlaying(clipIdle)) CrossFade(clipIdle, 0.1f, true);
        }

        public void SetSpeed(float v)
        {
            _lastSpeed = v;
            if (!useAnimator) return;
            _anim.SetFloat(speedParam, v);
        }

        public string PickAttack() { return "Attack_Light"; }

        public float PlayAttack(string key)
        {
            bool heavy = !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains("heavy");

            var picker = GetComponent<Weaponary.AttackVariantPicker>();
            picker?.PrepareAttack(heavy);

            if (useAnimator)
            {
                _anim.SetBool(heavyBool, heavy);
                _anim.ResetTrigger(attackTrigger);
                _anim.SetTrigger(attackTrigger);
                return heavy ? attackHeavyDuration : attackLightDuration;
            }

            if (_legacy != null)
            {
                var clip = heavy ? clipAttackHeavy : clipAttackLight;
                if (!CrossFade(clip, 0.05f, false)) CrossFade(clipIdle, 0.05f, true);
            }
            return heavy ? attackHeavyDuration : attackLightDuration;
        }

        public string PickHit() { return "Hit"; }

        public float PlayHit(string _)
        {
            if (useAnimator)
            {
                _anim.ResetTrigger(hitTrigger);
                _anim.SetTrigger(hitTrigger);
            }
            else if (_legacy != null)
            {
                if (!CrossFade(clipHit, 0.02f, false)) CrossFade(clipIdle, 0.02f, true);
            }
            return hitDuration;
        }

        public string PickDodge() { return "Dodge"; }

        public float PlayDodge(string _)
        {
            if (useAnimator)
            {
                var trig = string.IsNullOrEmpty(dodgeTrigger) ? hitTrigger : dodgeTrigger;
                _anim.ResetTrigger(trig);
                _anim.SetTrigger(trig);
            }
            else if (_legacy != null)
            {
                if (!CrossFade(clipDodge, 0.02f, false))
                    if (!CrossFade(clipHit, 0.02f, false))
                        CrossFade(clipIdle, 0.02f, true);
            }
            return dodgeDuration;
        }

        public float SetBlock(bool value)
        {
            if (useAnimator) _anim.SetBool(blockBool, value);
            else if (_legacy != null)
            {
                if (value) CrossFade(clipBlockLoop, 0.05f, true);
                else CrossFade(clipIdle, 0.1f, true);
            }
            return blockEnterExitDuration;
        }

        public float PlayDeath() => PlayDie();

        public float PlayDie()
        {
            if (useAnimator)
            {
                _anim.ResetTrigger(dieTrigger);
                _anim.SetTrigger(dieTrigger);
            }
            else if (_legacy != null)
            {
                if (!CrossFade(clipDie, 0.02f, false))
                    if (!CrossFade(clipHit, 0.02f, false))
                        CrossFade(clipIdle, 0.02f, true);
            }
            return dieDuration;
        }

        bool CrossFade(string clip, float fade, bool loop = false)
        {
            if (_legacy == null || string.IsNullOrEmpty(clip)) return false;
            var c = _legacy.GetClip(clip); if (!c) return false;
            c.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;
            _legacy.CrossFade(c.name, Mathf.Clamp(fade, 0f, 0.25f));
            return true;
        }
    }
}