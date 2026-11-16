using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Weaponary
{
    /// <summary>
    /// Lives on a fighter. Holds the equipped weapon: attaches the model to the hand,
    /// swaps the attack animation per attack, sets reach, and exposes damage via WeaponDamage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHolder : MonoBehaviour
    {
        [Header("Hand sockets (bone names)")]
        public string rightSocketName = "handslot.r";
        public string leftSocketName  = "handslot.l";

        [Header("Animation override")]
        [Tooltip("Name of the controller's original Attack_Light clip. Filled in automatically by the setup tool.")]
        public string lightAttackOriginalClip = "Melee_Unarmed_Punch";

        public WeaponDefinition Current { get; private set; }
        public bool IsArmed => Current != null;
        public bool HasShield
        {
            get
            {
                return Current != null &&
                       (Current.weaponClass == WeaponClass.OneHandAndShield || Current.weaponClass == WeaponClass.ShieldOnly);
            }
        }
        public float LastAttackDuration { get; private set; }

        Animator _animator;
        MeleeMotor _motor;
        AnimatorOverrideController _aoc;
        AnimationClip _originalAttack;
        float _baseAttackRange;
        bool _rangeCaptured;
        GameObject _rightVisual, _leftVisual;
        int _lastClipIndex = -1;
        bool _warned;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponent<MeleeMotor>();
            if (_motor != null) { _baseAttackRange = _motor.attackRange; _rangeCaptured = true; }
        }

        public void Equip(WeaponDefinition def)
        {
            Unequip();
            if (def == null) return;
            Current = def;

            var right = FindSocket(HumanBodyBones.RightHand, rightSocketName);
            if (def.visualPrefab != null && right != null)
            {
                _rightVisual = Attach(def.visualPrefab, right);
                _rightVisual.transform.localPosition = CreatorOffset(def.visualPrefab);

                var wd = _rightVisual.GetComponent<WeaponDamage>();
                if (wd == null) wd = _rightVisual.AddComponent<WeaponDamage>();
                wd.baseDamage = def.baseDamage;
                wd.weaponScalar = def.damageScalar;
            }

            bool usesOffhand = def.weaponClass == WeaponClass.DualOneHand ||
                               def.weaponClass == WeaponClass.OneHandAndShield ||
                               def.weaponClass == WeaponClass.ShieldOnly;
            if (usesOffhand && def.offhandVisualPrefab != null)
            {
                var left = FindSocket(HumanBodyBones.LeftHand, leftSocketName);
                if (left != null)
                {
                    _leftVisual = Attach(def.offhandVisualPrefab, left);
                    _leftVisual.transform.localRotation = Quaternion.Euler(def.offhandEuler);
                    _leftVisual.transform.localPosition = CreatorOffset(def.offhandVisualPrefab) + def.offhandOffset;
                }
            }

            if (_motor != null) _motor.attackRange = def.reach;
        }

        public void Unequip()
        {
            if (_rightVisual != null) Destroy(_rightVisual);
            if (_leftVisual != null) Destroy(_leftVisual);
            _rightVisual = null;
            _leftVisual = null;

            if (_aoc != null && _originalAttack != null) _aoc[_originalAttack] = null; // null = back to original clip
            if (_motor != null && _rangeCaptured) _motor.attackRange = _baseAttackRange;
            Current = null;
        }

        /// <summary>
        /// Called by MeleeAnimator right before it triggers an attack.
        /// Returns true if a weapon clip was prepared (LastAttackDuration is then valid).
        /// </summary>
        public bool PrepareAttack()
        {
            LastAttackDuration = 0f;
            if (Current == null || Current.attackClips == null || Current.attackClips.Count == 0) return false;
            if (!EnsureOverride()) return false;

            var clip = PickClip(Current.attackClips);
            if (clip == null) return false;

            _aoc[_originalAttack] = clip;
            LastAttackDuration = Mathf.Max(0.35f, clip.length);
            return true;
        }
        
        /// <summary>Real-time combat: force a specific clip for the next attack. True if it was applied.</summary>
        public bool PrepareAttackClip(AnimationClip clip)
        {
            if (clip == null || !EnsureOverride()) return false;
            _aoc[_originalAttack] = clip;
            LastAttackDuration = Mathf.Max(0.35f, clip.length);
            return true;
        }

        // ---------- internals ----------

        // Where the KayKit creator placed each item in the hand slot (read from the characters' own FBX files).
        // None of them are rotated; shields sit 0.156 "outwards" from the hand. Unknown items stay at the slot origin.
        // Your WeaponDefinition's offhandOffset is added on top for fine tuning.
        static readonly Dictionary<string, Vector3> CreatorOffsets = new Dictionary<string, Vector3>
        {
            { "1H_Sword",         new Vector3(0f, 0.033f, 0f) },
            { "1H_Axe",           new Vector3(0f, 0.033f, 0f) },
            { "2H_Sword",         new Vector3(0f, 0.033f, 0f) },
            { "2H_Axe",           new Vector3(0f, 0.033f, 0f) },
            { "Orc_Axe",          new Vector3(0f, 0.033f, 0f) },
            { "1H_Sword_Offhand", new Vector3(0f, 0.017f, 0f) },
            { "1H_Axe_Offhand",   new Vector3(0f, 0.033f, 0f) },
            { "Orc_Club",         new Vector3(0f, 0.017f, 0f) },
        };
        static readonly Vector3 ShieldOffset = new Vector3(0f, 0.017f, 0.156f);

        static Vector3 CreatorOffset(GameObject prefab)
        {
            if (prefab == null) return Vector3.zero;
            Vector3 v;
            if (CreatorOffsets.TryGetValue(prefab.name, out v)) return v;
            if (prefab.name.ToLowerInvariant().Contains("shield")) return ShieldOffset;   // all five KayKit shields share this
            return Vector3.zero;
        }

        GameObject Attach(GameObject prefab, Transform socket)
        {
            var go = Instantiate(prefab, socket);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            Fighters.FighterIdentity.SetLayerRecursive(go.transform, socket.gameObject.layer);   // visible to this team's portrait camera
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            return go;
        }

        bool EnsureOverride()
        {
            if (_aoc != null && _originalAttack != null) return true;
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null) return false;

            var rac = _animator.runtimeAnimatorController;
            if (rac == null) { WarnOnce("Animator has no controller."); return false; }

            _aoc = rac as AnimatorOverrideController;
            if (_aoc == null)
            {
                _aoc = new AnimatorOverrideController(rac);
                _animator.runtimeAnimatorController = _aoc;
            }

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _aoc.GetOverrides(pairs);
            foreach (var kv in pairs)
            {
                if (kv.Key != null && kv.Key.name == lightAttackOriginalClip) { _originalAttack = kv.Key; break; }
            }

            if (_originalAttack == null)
            {
                var names = new System.Text.StringBuilder();
                foreach (var kv in pairs) if (kv.Key != null) names.Append(kv.Key.name).Append(", ");
                WarnOnce($"Could not find original attack clip '{lightAttackOriginalClip}' in the controller. Clips available: {names}");
                return false;
            }
            return true;
        }

        AnimationClip PickClip(List<AnimationClip> list)
        {
            int n = list.Count;
            if (n == 0) return null;
            int i = (n == 1 || _lastClipIndex < 0) ? Random.Range(0, n) : (_lastClipIndex + Random.Range(1, n)) % n;
            _lastClipIndex = i;
            if (list[i] != null) return list[i];
            foreach (var c in list) if (c != null) return c;
            return null;
        }

        Transform FindSocket(HumanBodyBones bone, string socketName)
        {
            var deep = FindDeep(transform, socketName);
            if (deep != null) return deep;
            if (_animator != null && _animator.isHuman)
            {
                WarnOnce($"Socket '{socketName}' not found; using the humanoid hand bone instead (weapon may sit slightly off).");
                return _animator.GetBoneTransform(bone);
            }
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        void WarnOnce(string msg)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[WeaponHolder] {name}: {msg}", this);
        }
    }
}