// File: Assets/Scripts/Combat/FighterArrival.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game;

namespace Combat
{
    /// <summary>
    /// How a fighter arrives at the start of a fight: a small flat portal opens in the air above it and the fighter drops out of it
    /// to the ground, playing the "Spawn_Air" clip (an animation of falling and landing). Added by the ArenaFighterSpawner next to the
    /// FighterBrain; FighterAnimation.PlaySpawn hands over to this.
    ///
    /// The Spawn_Air clip itself carries the fall in a bone that the humanoid rig throws away (the "root"), so without this the
    /// fighter would play the falling pose standing on the ground. This component supplies the missing fall, using the same timing
    /// the clip was made with (the fall is between 0.17 s and 0.73 s of the clip).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FighterArrival : MonoBehaviour
    {
        public Color portalColor = new Color(0.25f, 0.85f, 1f, 1f);
        [Tooltip("How high above the ground the fall starts, in body heights.")]
        public float fallBodies = 1.6f;
        [Tooltip("How far above the start of the fall the portal hangs, in body heights.")]
        public float portalAboveBodies = 1.15f;
        [Tooltip("In a group fight the fighters do not all drop at the same moment: each waits a random time up to this.")]
        public float maxStagger = 0.35f;
        public float portalOpenSeconds = 0.18f;
        [Tooltip("When the fall starts and ends inside the Spawn_Air clip (seconds). Measured from the clip: 0.17 and 0.73.")]
        public float fallStart = 0.17f;
        public float fallEnd = 0.73f;
        [Tooltip("A soft thud when the fighter lands (uses the fighter's own sound set).")]
        public bool landThud = true;

        Transform _portal, _ring;
        readonly List<Material> _mats = new List<Material>();
        readonly List<Renderer> _hidden = new List<Renderer>();
        Transform _parent;
        Vector3 _groundLocal;
        bool _restored;

        /// <summary>Called by FighterAnimation.PlaySpawn. Returns how long the arrival takes (the brain waits that long before it fights).</summary>
        public float Begin(FighterAnimation fa, float clipLength)
        {
            float stagger = Random.Range(0f, Mathf.Max(0f, maxStagger));
            _parent = transform.parent;
            _groundLocal = transform.localPosition;
            _restored = false;
            StartCoroutine(Run(fa, stagger, clipLength));
            return stagger + clipLength + 0.05f;
        }

        Vector3 GroundWorld()
        {
            return _parent != null ? _parent.TransformPoint(_groundLocal) : _groundLocal;
        }

        IEnumerator Run(FighterAnimation fa, float stagger, float clipLength)
        {
            float H = BodyHeight();
            float fallH = H * Mathf.Max(0f, fallBodies);
            float portalH = fallH + H * portalAboveBodies;

            try
            {
                // from this very frame the fighter is up in the air and unseen, so there is no frame of it standing on the ground
                SetVisible(false);
                transform.position = GroundWorld() + Vector3.up * fallH;
                BuildPortal(H);
                PlacePortal(portalH, 0f);

                for (float t = 0f; t < stagger; t += Time.deltaTime) yield return null;

                // the portal opens
                for (float t = 0f; t < portalOpenSeconds; t += Time.deltaTime)
                {
                    PlacePortal(portalH, Ease(t / Mathf.Max(0.01f, portalOpenSeconds)));
                    yield return null;
                }
                PlacePortal(portalH, 1f);

                // the fighter drops out
                SetVisible(true);
                if (fa != null) fa.TriggerSpawn();

                bool landed = false;
                float closeFrom = fallStart + 0.15f, closeTo = fallStart + 0.45f;
                for (float t = 0f; t < clipLength; t += Time.deltaTime)
                {
                    float u = Mathf.Clamp01((t - fallStart) / Mathf.Max(0.01f, fallEnd - fallStart));
                    float s = u * u * (3f - 2f * u);   // the clip's own fall curve is a smoothstep
                    transform.position = GroundWorld() + Vector3.up * (fallH * (1f - s));

                    float open = 1f - Mathf.Clamp01((t - closeFrom) / Mathf.Max(0.01f, closeTo - closeFrom));
                    PlacePortal(portalH, Ease(open));

                    if (!landed && t >= fallEnd)
                    {
                        landed = true;
                        Land();
                    }
                    yield return null;
                }
            }
            finally
            {
                Restore();
            }
        }

        void Land()
        {
            if (!landThud) return;
            var brain = GetComponent<FighterBrain>();
            var sounds = brain != null ? brain.sounds : null;
            if (sounds != null) sounds.hitUnarmed.Play(0f, 0.55f);
        }

        // Everything back to normal: on the ground, visible, no portal.
        void Restore()
        {
            if (_restored) return;
            _restored = true;
            if (this != null) transform.position = GroundWorld();
            SetVisible(true);
            if (_portal != null) Destroy(_portal.gameObject);
            foreach (var m in _mats) if (m != null) Destroy(m);
            _mats.Clear();
        }

        void OnDestroy()
        {
            if (_portal != null) Destroy(_portal.gameObject);
            foreach (var m in _mats) if (m != null) Destroy(m);
        }

        // ------------------------------------------------------------------ helpers
        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }

        float BodyHeight()
        {
            var rends = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (rends.Length == 0) return Mathf.Max(0.05f, 1.8f * transform.lossyScale.y);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;

            // the renderers' bounds can be generous (padded for the animations): also work it out from the head bone and use the smaller
            var anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    float headUp = head.position.y - transform.position.y;
                    if (headUp > 0.01f) h = Mathf.Min(h, headUp / 0.80f);
                }
            }
            return Mathf.Max(0.03f, h);
        }

        void SetVisible(bool visible)
        {
            if (!visible)
            {
                _hidden.Clear();
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r != null && r.enabled) { _hidden.Add(r); r.enabled = false; }
            }
            else
            {
                foreach (var r in _hidden) if (r != null) r.enabled = true;
                _hidden.Clear();
            }
        }

        void PlacePortal(float heightAboveGround, float openness)
        {
            if (_portal == null) return;
            _portal.position = GroundWorld() + Vector3.up * heightAboveGround;
            _portal.localScale = Vector3.one * Mathf.Max(0.0001f, openness);
            _portal.gameObject.SetActive(openness > 0.001f);
            if (_ring != null) _ring.localRotation = Quaternion.Euler(0f, Time.time * 160f, 0f);
        }

        Material Mat(Color c, Color glow)
        {
            var m = StageMaterials.Make(c);
            if (m == null) return null;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow);
            _mats.Add(m);
            return m;
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = m;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
            return go;
        }

        // A flat ring of glowing beads around a dark disc, lying in the air. World size: just wider than the fighter.
        void BuildPortal(float H)
        {
            var ringMat = Mat(portalColor, portalColor * 2.2f);
            var voidMat = Mat(new Color(0.02f, 0.05f, 0.08f, 1f), portalColor * 0.25f);
            if (ringMat == null || voidMat == null) return;   // no usable material: no portal, the fighter just drops

            float R = 0.40f * H;
            var root = new GameObject("ArrivalPortal");
            _portal = root.transform;
            _portal.localScale = Vector3.one * 0.0001f;

            var disc = Prim(PrimitiveType.Cylinder, "Void", _portal, voidMat);
            disc.transform.localScale = new Vector3(2f * R, 0.004f * H, 2f * R);   // a flat cylinder, axis up

            _ring = new GameObject("Ring").transform;
            _ring.SetParent(_portal, false);
            const int beads = 14;
            for (int i = 0; i < beads; i++)
            {
                float a = i * Mathf.PI * 2f / beads;
                var b = Prim(PrimitiveType.Sphere, "Bead", _ring, ringMat);
                b.transform.localPosition = new Vector3(Mathf.Cos(a) * R, 0f, Mathf.Sin(a) * R);
                b.transform.localScale = Vector3.one * (R * (i % 2 == 0 ? 0.24f : 0.15f));
            }
        }
    }
}
