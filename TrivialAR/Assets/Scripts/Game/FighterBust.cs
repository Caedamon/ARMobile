using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game
{
    /// <summary>
    /// Takes a head-and-shoulders picture of a fighter model for the Codex pages of the Book, the same way WeaponSnapshot does
    /// for the Armoury: the model is copied far below the table (so nothing else is in the picture), stripped of every script,
    /// collider and animator (so nothing runs), drawn flat by a temporary camera and copied into a normal texture with a
    /// see-through background. The copy stands in the pose the prefab was saved in, facing the camera.
    /// The picture is framed from the model's own height, so the small and the large models look equally close.
    /// </summary>
    public static class FighterBust
    {
        /// <param name="near">Any object near the table; the temporary scene is built 400 m below it.</param>
        /// <param name="flat">A material using an Unlit shader (the one the weapons use). Null = the model keeps its own materials.</param>
        /// <param name="width">Picture size in pixels (the page frame decides the shape: taller than wide).</param>
        /// <param name="heightFraction">How much of the model's height the picture shows (0.3 = head and shoulders, 1 = the whole body).</param>
        /// <param name="pose">An animation clip (an idle) to stand the model in, so it does not stand in a T-pose. Null = the prefab's own pose.
        /// Needs a humanoid rig: models with another kind of rig keep their own pose.</param>
        /// <param name="poseTime">Seconds into the clip.</param>
        public static Texture2D Take(Transform near, GameObject prefab, Material flat, int width, int height, float heightFraction,
                                     AnimationClip pose, float poseTime, out string problem)
        {
            problem = null;
            if (prefab == null) { problem = "the model has no prefab"; return null; }

            Vector3 origin = (near != null ? near.position : Vector3.zero) + Vector3.down * 400f;
            var holder = new GameObject("FighterBustHolder");
            holder.transform.position = origin;
            holder.SetActive(false);   // nothing in the copy wakes up while it is stripped

            var temp = new List<UnityEngine.Object>();
            Camera cam = null;
            RenderTexture rt = null;
            Texture2D result = null;
            try
            {
                var go = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                Strip(go);
                RecolourFlat(go, flat, temp);
                holder.SetActive(true);

                // stand the model in the pose (an idle), then take the head position and drop the animator
                // The height is measured BEFORE posing: skinned meshes only update their bounds when drawn, and the model's height
                // hardly changes between the saved pose and an idle.
                Bounds all = BoundsOf(go);
                Transform head = null;
                bool posed = false;
                foreach (var animator in go.GetComponentsInChildren<Animator>(true))
                {
                    if (pose != null && animator.isHuman) { ApplyPose(animator, pose, poseTime); posed = true; }
                    if (head == null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
                    UnityEngine.Object.DestroyImmediate(animator);
                }

                float bodyHeight = all.size.y;
                if (bodyHeight < 1e-4f) { problem = "the model has no size"; return null; }

                Vector3 headPos = head != null ? head.position : new Vector3(all.center.x, all.max.y - 0.10f * bodyHeight, all.center.z);
                bool whole = heightFraction >= 0.95f;
                float half = Mathf.Max(0.05f, bodyHeight * Mathf.Clamp(heightFraction, 0.15f, 1f) * 0.5f * (whole ? 1.08f : 1f));
                // a model that could not be posed still has its arms out: make room for the width (the picture is taller than wide)
                if (whole && !posed) half = Mathf.Max(half, Mathf.Max(all.size.x, all.size.z) * 0.55f * height / Mathf.Max(1f, (float)width));
                // head a little above the middle of the picture
                Vector3 centre = headPos + Vector3.down * (half * 0.22f);
                if (whole) centre = new Vector3(headPos.x, all.center.y, all.center.z);

                var camGo = new GameObject("FighterBustCamera");
                camGo.transform.position = new Vector3(centre.x, centre.y, all.center.z + Mathf.Max(5f, bodyHeight * 4f));
                camGo.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);   // the model faces +Z: look at its front
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = half;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = Mathf.Max(30f, bodyHeight * 12f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.allowHDR = false;
                cam.enabled = false;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.antiAliasing = 4;
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();

                result = new Texture2D(width, height, TextureFormat.RGBA32, true);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(true, true);
                RenderTexture.active = prev;
                result.wrapMode = TextureWrapMode.Clamp;
                result.filterMode = FilterMode.Bilinear;
                cam.targetTexture = null;
                return result;
            }
            catch (Exception e)
            {
                problem = e.Message;
                if (result != null) UnityEngine.Object.Destroy(result);
                return null;
            }
            finally
            {
                if (cam != null) UnityEngine.Object.Destroy(cam.gameObject);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                UnityEngine.Object.Destroy(holder);
                foreach (var o in temp) if (o != null) UnityEngine.Object.Destroy(o);
            }
        }

        // Put a humanoid model into one frame of a clip: a one-off, manually evaluated playable graph on its animator.
        static void ApplyPose(Animator animator, AnimationClip clip, float time)
        {
            PlayableGraph graph = default(PlayableGraph);
            try
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable.SetTime(Mathf.Max(0f, time));
                graph.Evaluate(0f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Codex] Could not pose the model: " + e.Message);
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
            }
        }

        // Remove everything that could run or collide. Scripts depend on each other (RequireComponent), so remove the ones nothing
        // depends on first, and repeat. The animator stays until the head has been found.
        static void Strip(GameObject go)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                var list = new List<MonoBehaviour>(go.GetComponentsInChildren<MonoBehaviour>(true));
                int left = 0;
                foreach (var mb in list)
                {
                    if (mb == null) continue;
                    if (IsRequiredByOther(mb)) { left++; continue; }
                    UnityEngine.Object.DestroyImmediate(mb);
                }
                if (left == 0) break;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
            foreach (var t in go.GetComponentsInChildren<TrailRenderer>(true)) t.gameObject.SetActive(false);
        }

        static bool IsRequiredByOther(Component c)
        {
            var type = c.GetType();
            foreach (var other in c.gameObject.GetComponents<Component>())
            {
                if (other == null || other == c) continue;
                foreach (var attr in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var rc = (RequireComponent)attr;
                    if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(type)) ||
                        (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(type)) ||
                        (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(type))) return true;
                }
            }
            return false;
        }

        static void RecolourFlat(GameObject go, Material flat, List<UnityEngine.Object> temp)
        {
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
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(false);
            Bounds b = default(Bounds);
            bool any = false;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(go.transform.position, Vector3.zero);
        }
    }
}
