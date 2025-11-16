using Animation;
using UnityEngine;
using Weaponary;
using Combat;

namespace Items
{
    [DisallowMultipleComponent]
    public sealed class WeaponPickup : MonoBehaviour
    {
        public WeaponClass weaponClass = WeaponClass.OneHand;
        public bool destroyOnPickup = true;
        public float reserveTimeout = 2f;

        bool _reserved;
        float _reserveUntil;

        Collider _col;
        Rigidbody _rb;

        void Awake()
        {
            _col = GetComponent<Collider>();
            _rb  = GetComponent<Rigidbody>();
        }

        public bool TryReserve(GameObject who)
        {
            if (_reserved && Time.time < _reserveUntil) return false;
            _reserved = true;
            _reserveUntil = Time.time + reserveTimeout;
            return true;
        }

        public void Pick(GameObject who)
        {
            var binder = who.GetComponent<WeaponAnimatorBinder>();
            var anim   = who.GetComponent<MeleeAnimator>();
            var picker = who.GetComponent<AttackVariantPicker>();

            binder?.ApplyFor(weaponClass);
            anim?.RecalculateDurationsFromController();
            picker?.MarkEquipped(); // end Unarmed-only period

            var a = who.GetComponent<Animator>();
            if (a) { a.ResetTrigger("Pickup"); a.SetTrigger("Pickup"); }

            if (_col) _col.enabled = false;
            if (_rb) { _rb.isKinematic = true; _rb.detectCollisions = false; }

            var rends = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;

            if (destroyOnPickup) Destroy(gameObject, 0.1f);
        }
    }
}