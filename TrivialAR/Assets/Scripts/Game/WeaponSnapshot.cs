using System.Collections.Generic;
using UnityEngine;
using Weaponary;

namespace Game
{
    /// <summary>
    /// Takes a flat, side-view picture of a weapon for the Armoury pages of the Book.
    /// The weapon's model is put far away from everything (so no other object or light affects it), turned so its longest side
    /// points up and you look at its broad side, drawn flat (no lighting) by a temporary camera, and copied into a normal
    /// texture with a see-through background. A weapon with a shield or a second weapon shows both side by side.
    /// Nothing from the model except its meshes is kept: scripts, colliders and animators are stripped from the temporary copy.
    /// </summary>
    public static class WeaponSnapshot
    {
        /// <param name="near">Any object near the table; the temporary scene is built 400 m below it.</param>
        /// <param name="flat">A material using an Unlit shader (URP/Unlit). Null = the model keeps its own lit materials.</param>
        /// <param name="tiltDegrees">How far the weapon leans (it leans the other way for a second weapon, not for a shield).</param>
        /// <param name="upsideDown">Turn the picture 180 degrees (for weapons whose model points the other way).</param>
        /// <param name="shieldOutside">Shields are modelled with the inside toward the camera: turn them so the outside shows.</param>
        public static Texture2D Take(Transform near, WeaponDefinition def, Material flat, int size, float tiltDegrees,
                                     bool upsideDown, bool shieldOutside, out string problem)
        {
            problem = null;
            if (def == null || def.visualPrefab == null) { problem = "the weapon has no model"; return null; }

            Vector3 origin = (near != null ? near.position : Vector3.zero) + Vector3.down * 400f;
            var holder = new GameObject("WeaponSnapshotHolder");
            holder.transform.position = origin;
            holder.SetActive(false);   // nothing in the copies wakes up while they are stripped

            var temp = new List<Object>();
            Camera cam = null;
            RenderTexture rt = null;
            Texture2D result = null;
            try
            {
                var main = Make(def.visualPrefab, holder.transform, flat, temp);
                GameObject off = def.offhandVisualPrefab != null ? Make(def.offhandVisualPrefab, holder.transform, flat, temp) : null;
                holder.SetActive(true);

                bool shield = def.weaponClass == WeaponClass.OneHandAndShield || def.weaponClass == WeaponClass.ShieldOnly;
                Orient(main, upsideDown);
                if (off != null) Orient(off, upsideDown);

                if (shieldOutside && shield)
                {
                    // 180 degrees around the up axis: same shield, seen from the other side
                    var shieldGo = off != null ? off : main;
                    shieldGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * shieldGo.transform.rotation;
                }

                float t = tiltDegrees;
                Lean(main, off != null && !shield ? -t : t);
                if (off != null) Lean(off, shield ? 0f : t);

                // main at the middle, the second piece to its right
                Bounds mb = BoundsOf(main);
                main.transform.position += origin - mb.center;
                if (off != null)
                {
                    mb = BoundsOf(main);
                    Bounds ob = BoundsOf(off);
                    float gap = Mathf.Max(mb.size.x, ob.size.x) * 0.25f;
                    off.transform.position += (origin - ob.center) + Vector3.right * (mb.extents.x + gap + ob.extents.x) + (mb.center - origin);
                }

                Bounds all = BoundsOf(main);
                if (off != null) all.Encapsulate(BoundsOf(off));
                float half = Mathf.Max(all.size.x, all.size.y) * 0.5f * 1.1f;
                if (half < 1e-4f) { problem = "the model has no size"; return null; }

                var camGo = new GameObject("WeaponSnapshotCamera");
                camGo.transform.position = all.center + Vector3.back * 10f;
                camGo.transform.rotation = Quaternion.identity;
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = half;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.allowHDR = false;
                cam.enabled = false;

                rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.antiAliasing = 4;
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();

                result = new Texture2D(size, size, TextureFormat.RGBA32, true);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                result.Apply(true, true);
                RenderTexture.active = prev;
                result.wrapMode = TextureWrapMode.Clamp;
                result.filterMode = FilterMode.Bilinear;
                cam.targetTexture = null;
                return result;
            }
            catch (System.Exception e)
            {
                problem = e.Message;
                if (result != null) Object.Destroy(result);
                return null;
            }
            finally
            {
                if (cam != null) Object.Destroy(cam.gameObject);
                if (rt != null) { rt.Release(); Object.Destroy(rt); }
                Object.Destroy(holder);
                foreach (var o in temp) if (o != null) Object.Destroy(o);
            }
        }

        // A stripped copy of the model: meshes only, drawn flat if a flat material was given.
        static GameObject Make(GameObject prefab, Transform holder, Material flat, List<Object> temp)
        {
            var go = Object.Instantiate(prefab, holder, false);   // the holder is switched off, so nothing runs yet

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (flat == null) continue;

                var src = r.sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = new Material(flat);
                    var s = src[i];
                    if (s != null)
                    {
                        Texture tex = null;
                        if (s.HasProperty("_BaseMap")) tex = s.GetTexture("_BaseMap");
                        if (tex == null && s.HasProperty("_MainTex")) tex = s.GetTexture("_MainTex");
                        if (tex != null)
                        {
                            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                        }
                        Color col = Color.white;
                        if (s.HasProperty("_BaseColor")) col = s.GetColor("_BaseColor");
                        else if (s.HasProperty("_Color")) col = s.GetColor("_Color");
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
                    }
                    temp.Add(m);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            return go;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Longest side up, shortest side toward the camera: you look at the broad side of the weapon, whatever way the model was built.
        static void Orient(GameObject go, bool upsideDown)
        {
            Vector3 s = BoundsOf(go).size;
            float[] v = { s.x, s.y, s.z };

            int longest = 0;
            for (int i = 1; i < 3; i++) if (v[i] > v[longest]) longest = i;
            if (v[1] >= 0.92f * v[longest]) longest = 1;   // nearly round (a shield): keep it upright

            int shortest = -1;
            for (int i = 0; i < 3; i++)
            {
                if (i == longest) continue;
                if (shortest < 0 || v[i] < v[shortest]) shortest = i;
            }

            Vector3 up = Axis(longest);
            Vector3 toCamera = Axis(shortest);
            Quaternion q = Quaternion.Inverse(Quaternion.LookRotation(toCamera, up));   // longest -> up, shortest -> forward
            go.transform.rotation = q * go.transform.rotation;
            if (upsideDown) go.transform.rotation = Quaternion.Euler(0f, 0f, 180f) * go.transform.rotation;
        }

        static void Lean(GameObject go, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.01f) return;
            go.transform.rotation = Quaternion.Euler(0f, 0f, degrees) * go.transform.rotation;
        }

        static Vector3 Axis(int i)
        {
            return i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
        }
    }
}
