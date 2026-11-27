// File: Assets/Scripts/Game/BossIntro.cs
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Combat;
using Fighters;

namespace Game
{
    /// <summary>
    /// The little film before a boss fight: a portal opens, the boss walks out, the picture zooms in on it, the boss name smashes onto
    /// the screen (red, black outline), then "BOSS!" smashes in (dark purple) while the boss roars. Then the fight starts.
    ///
    /// The phone's camera cannot be moved (it IS the phone), so the film is drawn by a second camera that covers the whole screen with a dark
    /// backdrop for the few seconds it lasts. Everything is built in code; nothing to add to the scene. Tap anywhere to skip.
    /// Started by GameController (Boss fights only).
    /// </summary>
    public sealed class BossIntro : MonoBehaviour
    {
        // set by GameController before Begin()
        public string bossName = "BOSS";
        public string label = "BOSS!";
        public Color nameColor = new Color(0.90f, 0.05f, 0.05f, 1f);
        public Color labelColor = new Color(0.30f, 0.04f, 0.45f, 1f);
        public Color labelHalo = new Color(0.80f, 0.60f, 1f, 1f);
        public Color backdrop = new Color(0.08f, 0.06f, 0.13f, 1f);
        public Color portalColor = new Color(0.62f, 0.25f, 1f, 1f);
        public AudioClip portalClip, slamClip, roarClip;

        [Header("Timing (seconds)")]
        public float portalOpenSeconds = 1.0f;
        public float zoomSeconds = 1.3f;
        public float freezeSeconds = 0.35f;
        public float nameHoldSeconds = 0.7f;
        public float roarHoldSeconds = 1.5f;
        public float fadeOutSeconds = 0.35f;

        public bool Running { get; private set; }

        // fight objects
        ArenaFighterSpawner _arena;
        GameObject _boss, _hero;
        readonly List<FighterBrain> _brains = new List<FighterBrain>();
        readonly List<MeleeAI> _ais = new List<MeleeAI>();
        readonly List<TextMeshPro> _plates = new List<TextMeshPro>();
        Vector3 _P;            // where the boss ends up (its fight position)
        Quaternion _rot;       // facing the hero
        Transform _film;       // film space: origin = where the boss ends up, +z = towards the hero, 1 unit = the boss's height. Follows the stage.
        float _H = 0.09f;      // body height of the boss, in world meters
        bool _rootMotion;
        Animator _bossAnimator;
        Transform _headBone;   // the boss's head bone (the close-up is aimed at it)

        // film objects
        GameObject _canvasGo, _camGo;
        Camera _cam;
        RectTransform _content, _barTop, _barBottom;
        Image _flash;
        TextMeshProUGUI _skipHint;
        Transform _portal, _ringPivot, _spokePivot;
        readonly List<Material> _mats = new List<Material>();

        // camera state, in film space (the camera is placed from these every frame, plus the shake)
        Vector3 _camPos, _camLook;
        float _camFov = 38f;

        // state
        Action _onDone;
        float _age;
        bool _skip, _cleaned;
        float _shake;                 // 0..1, decays
        float _portalScale;
        float _barSlide;              // 0 = hidden, 1 = in
        Vector2 _contentBase;

        const float BarHeight = 150f;

        // ------------------------------------------------------------------ start
        /// <summary>Freezes the fight (nobody moves until the film is over) and starts the film. onDone is called when it ends or is skipped.</summary>
        public void Begin(ArenaFighterSpawner arena, FighterIdentity boss, Action onDone)
        {
            _arena = arena;
            _onDone = onDone;
            Running = true;
            _age = 0f;
            try
            {
                if (arena == null || boss == null) throw new InvalidOperationException("no arena or boss");
                _boss = boss.gameObject;
                _hero = arena.Player != null ? arena.Player.gameObject : null;
                Freeze();
                StartCoroutine(Run());
            }
            catch (Exception e)
            {
                Debug.LogError("[BossIntro] Could not start the intro, starting the fight right away: " + e.Message, this);
                Cleanup();
            }
        }

        // Nobody fights, nobody is seen: the hero and the boss are hidden until the film needs them; their brains start when it is over,
        // so they "arrive" as in any other fight.
        void Freeze()
        {
            foreach (var b in _arena.GetComponentsInChildren<FighterBrain>(true)) { _brains.Add(b); b.enabled = false; }
            // With its brain off, a fighter looks "not real-time" to the old turn-based BattleManager, which would pick it up and walk it to a
            // weapon. A fighter whose MeleeAI is off is skipped by that, so the boss stays where the film puts it.
            foreach (var a in _arena.GetComponentsInChildren<MeleeAI>(true)) { _ais.Add(a); a.enabled = false; }
            foreach (var t in _arena.GetComponentsInChildren<TextMeshPro>(true))
                if (t.enabled) { _plates.Add(t); t.enabled = false; }   // the floating name labels

            _P = _boss.transform.position;

            // the boss faces the hero (worked out from where they stand, not from how the model happens to be turned)
            Vector3 face = _boss.transform.forward;
            if (_hero != null)
            {
                Vector3 d = _hero.transform.position - _P;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-8f) face = d;
            }
            face.y = 0f;
            face = face.sqrMagnitude > 1e-8f ? face.normalized : Vector3.forward;
            _rot = Quaternion.LookRotation(face, Vector3.up);

            var rends = _boss.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                _H = Mathf.Max(0.02f, b.size.y);
            }
            // The renderers' bounds can be generous (padded for the animations), so the body height is also worked out from the head bone and the smaller is used.
            var humanAnim = _boss.GetComponent<Animator>();
            if (humanAnim == null) humanAnim = _boss.GetComponentInChildren<Animator>();
            if (humanAnim != null && humanAnim.isHuman)
            {
                _headBone = humanAnim.GetBoneTransform(HumanBodyBones.Head);
                if (_headBone != null)
                {
                    float headUp = _headBone.position.y - _P.y;
                    if (headUp > 0.01f) _H = Mathf.Max(0.02f, Mathf.Min(_H, headUp / 0.80f));
                }
            }
            // the boss has just made its entrance through the portal: no "arrival" animation when the fight starts
            var bossAnim = _boss.GetComponent<FighterAnimation>();
            if (bossAnim != null) bossAnim.skipSpawn = true;

            _bossAnimator = _boss.GetComponent<Animator>();
            if (_bossAnimator == null) _bossAnimator = _boss.GetComponentInChildren<Animator>();
            if (_bossAnimator != null) _rootMotion = _bossAnimator.applyRootMotion;

            // film space: a child of the stage, so if the stage moves or settles during the film, the film moves with it
            var fgo = new GameObject("BossIntroFilm");
            _film = fgo.transform;
            _film.SetParent(_arena.transform, false);
            _film.position = _P;
            _film.rotation = _rot;
            float parentScale = Mathf.Max(0.0001f, _arena.transform.lossyScale.y);
            _film.localScale = Vector3.one * (_H / parentScale);

            _boss.SetActive(false);
            // The hero stands in his place the whole time, frozen like the boss, and has no arrival animation either, so that when the film ends
            // both start at the same moment.
            if (_hero != null)
            {
                var heroAnim = _hero.GetComponent<FighterAnimation>();
                if (heroAnim != null) heroAnim.skipSpawn = true;
            }
        }

        // ------------------------------------------------------------------ the film
        IEnumerator Run()
        {
            try { yield return Sequence(); }
            finally { Cleanup(); }
        }

        IEnumerator Sequence()
        {
            // Film space: the boss ends at (0,0,0) facing +z (towards the hero), 1 unit = the boss's height.
            const float walk = 1.8f;                         // the portal stands this far behind where the boss ends up
            Vector3 start = new Vector3(0f, 0f, -walk);

            BuildOverlay();
            BuildCamera();
            BuildPortal(walk);

            // 1. the camera stands straight in front of the portal, on its axis, looking at it
            float aspect = Mathf.Max(0.3f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float fix = Mathf.Max(1f, 1.78f / aspect);       // a narrow (portrait) screen needs more distance
            Vector3 camStart = new Vector3(0f, 0.70f, -walk + 4.4f * fix);
            Vector3 portalLook = new Vector3(0f, 0.72f, -walk);
            _camPos = camStart; _camLook = portalLook; _camFov = 34f;
            ApplyCamera();

            // bars slide in, the portal opens
            PlayEffect(portalClip, 0.5f, 0.45f);
            yield return Tween(portalOpenSeconds, t =>
            {
                _barSlide = Mathf.Clamp01(t * 3f);
                _portalScale = EaseOut(t);
                _camPos = camStart + new Vector3(0f, 0f, -0.25f * t);
            });
            if (_skip) yield break;

            // 2. the boss walks out of the portal, straight towards the camera
            _boss.SetActive(true);
            _boss.transform.SetPositionAndRotation(_film.TransformPoint(start), _film.rotation);
            if (_bossAnimator != null) { _bossAnimator.applyRootMotion = false; _bossAnimator.speed = 1f; }
            var ma = _boss.GetComponent<MeleeAnimator>();
            if (ma != null) ma.SetSpeed(1f);

            float walkSpeed = 1.6f * Mathf.Max(0.0001f, _boss.transform.lossyScale.y);
            float walkTime = Mathf.Clamp(walk * _H / walkSpeed, 1.0f, 4.0f);
            Vector3 camAfterOpen = _camPos;
            yield return Tween(walkTime, t =>
            {
                _boss.transform.SetPositionAndRotation(_film.TransformPoint(Vector3.Lerp(start, Vector3.zero, t)), _film.rotation);
                _portalScale = t < 0.55f ? 1f : 1f - EaseOut((t - 0.55f) / 0.45f);
                _camLook = new Vector3(0f, 0.68f, Mathf.Lerp(-walk, -0.15f, t * 0.75f));
                _camPos = camAfterOpen + new Vector3(0f, 0f, -0.35f * t);
            });
            _boss.transform.SetPositionAndRotation(_film.position, _film.rotation);
            if (ma != null) ma.SetSpeed(0f);
            _portalScale = 0f;
            if (_skip) yield break;

            // 3. zoom in on the face, from the front
            // aim at the middle of the face: the head bone sits at the base of the head, so a little above it
            float faceY = 0.80f;
            if (_headBone != null) faceY = Mathf.Clamp(_film.InverseTransformPoint(_headBone.position).y + 0.07f, 0.5f, 1.1f);
            Vector3 closePos = new Vector3(0.06f, faceY - 0.12f, 1.25f);
            Vector3 closeLook = new Vector3(0f, faceY, 0f);
            Vector3 zoomFromPos = _camPos, zoomFromLook = _camLook;
            float zoomFromFov = _camFov;
            yield return Tween(zoomSeconds, t =>
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                _camPos = Vector3.Lerp(zoomFromPos, closePos, s);
                _camLook = Vector3.Lerp(zoomFromLook, closeLook, s);
                _camFov = Mathf.Lerp(zoomFromFov, 28f, s);
            });
            _camPos = closePos; _camLook = closeLook; _camFov = 28f;
            if (_skip) yield break;

            // 4. freeze frame: a short breath before the hit (the picture creeps in a hair)
            Vector3 holdPos = _camPos;
            yield return Tween(freezeSeconds, t => { _camPos = holdPos + new Vector3(0f, 0f, -0.04f * t); });
            if (_skip) yield break;

            // 5. the name smashes in: red, black outline
            string niceName = (bossName ?? "BOSS").Trim().ToUpperInvariant();
            if (niceName.Length == 0) niceName = "BOSS";
            float nameSize = Mathf.Clamp(1700f / (niceName.Length * 0.62f), 70f, 190f);
            var nameLabel = MakeLabel("Name", niceName, nameSize, nameColor, Color.black, 0.30f, false, new Vector2(0f, 150f));
            yield return Slam(nameLabel, 3.0f, 0.55f);
            if (_skip) yield break;
            yield return Wait(nameHoldSeconds);
            if (_skip) yield break;

            // 6. "BOSS!" smashes in (dark purple) and the boss roars
            var bossLabel = MakeLabel("Boss", (label ?? "BOSS!").ToUpperInvariant(), 300f, labelColor, new Color(0.02f, 0f, 0.04f, 1f), 0.32f, true, new Vector2(0f, -130f));
            StartCoroutine(Roar());
            yield return Slam(bossLabel, 3.4f, 0.95f);
            if (_skip) yield break;

            Vector3 roarPos = _camPos;
            yield return Tween(roarHoldSeconds, t =>
            {
                _shake = Mathf.Max(_shake, 0.35f * (1f - t));   // the roar keeps the picture trembling
                _camFov = Mathf.Lerp(28f, 25f, EaseOut(t));
                _camPos = roarPos + new Vector3(0f, 0f, -0.08f * t);
            });
            if (_skip) yield break;

            // 7. everything fades, the bars slide out
            yield return Tween(fadeOutSeconds, t =>
            {
                nameLabel.group.alpha = 1f - t;
                bossLabel.group.alpha = 1f - t;
                _barSlide = 1f - t;
            });
        }

        IEnumerator Roar()
        {
            // the arms-up pose the winners use (only if the Animation Pack has it), plus sound
            var fa = _boss != null ? _boss.GetComponent<FighterAnimation>() : null;
            if (fa != null) fa.PlayCheer();

            if (roarClip != null) { PlayEffect(roarClip, 1f, 1f); yield break; }

            // no recording given: low, layered voice sounds from the fighters' own sound set (a stand-in until there is a real roar clip)
            var s = _arena != null ? _arena.combatSounds : null;
            if (s == null) yield break;
            s.death.Play(0.00f, 0.50f);
            s.death.Play(0.10f, 0.42f);
            s.hurtGrunt.Play(0.25f, 0.45f);
            s.death.Play(0.40f, 0.55f);
            s.hitHeavy.Play(0.05f, 0.45f);
        }

        // ------------------------------------------------------------------ text slam
        sealed class Label
        {
            public RectTransform root;
            public CanvasGroup group;
        }

        Label MakeLabel(string name, string text, float size, Color face, Color outline, float outlineWidth, bool halo, Vector2 pos)
        {
            var go = new GameObject(name + "Label", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2600f, 420f);
            rt.anchoredPosition = pos;
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            if (halo)
            {
                // a wider light outline behind, so dark purple stands out from the dark backdrop
                MakeText(go.transform, "Halo", text, size, face, labelHalo, 0.75f);
            }
            MakeText(go.transform, "Text", text, size, face, outline, outlineWidth);

            return new Label { root = rt, group = cg };
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color face, Color outline, float outlineWidth)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.color = face;
            t.outlineColor = (Color32)outline;
            t.outlineWidth = outlineWidth;
            return t;
        }

        // From big and see-through to its place, faster and faster, then it hits: shake, flash, boom, and a small bounce.
        IEnumerator Slam(Label l, float startScale, float hitStrength)
        {
            const float fall = 0.16f;
            for (float t = 0f; t < fall && !_skip; t += Time.deltaTime)
            {
                float k = t / fall;
                l.root.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, k * k);
                l.group.alpha = Mathf.Clamp01(k * 2.5f);
                yield return null;
            }
            l.group.alpha = 1f;
            l.root.localScale = Vector3.one;

            // impact
            _shake = Mathf.Max(_shake, hitStrength);
            if (_flash != null) { var c = _flash.color; c.a = 0.55f * hitStrength + 0.2f; _flash.color = c; }
            PlayImpact(hitStrength);

            const float bounce = 0.14f;
            for (float t = 0f; t < bounce && !_skip; t += Time.deltaTime)
            {
                float k = t / bounce;
                l.root.localScale = Vector3.one * (1f + 0.09f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            l.root.localScale = Vector3.one;
        }

        void PlayImpact(float strength)
        {
            if (slamClip != null) { PlayEffect(slamClip, 1f, 1f); return; }
            var s = _arena != null ? _arena.combatSounds : null;
            if (s == null) return;
            s.hitHeavy.Play(0f, 0.55f - 0.1f * strength);
            s.hitHeavy.Play(0.04f, 0.42f);
        }

        static void PlayEffect(AudioClip clip, float volume, float pitch)
        {
            if (clip != null) GameAudio.PlayEffect(clip, volume, pitch, 0f);
        }

        // ------------------------------------------------------------------ small helpers
        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        // Runs step(0..1) every frame for 'seconds'; stops early if the player skips.
        IEnumerator Tween(float seconds, Action<float> step)
        {
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime)
            {
                step(t / seconds);
                yield return null;
            }
            if (!_skip) step(1f);
        }

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) yield return null;
        }

        static bool Pressed()
        {
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            var t = Touchscreen.current;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (!Running) return;
            _age += Time.deltaTime;
            if (_age > 0.5f && Pressed()) _skip = true;
            if (_age > 20f) _skip = true;   // safety: never leave the fight frozen

            _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 2.2f);

            // bars
            float bs = Mathf.SmoothStep(0f, 1f, _barSlide);
            if (_barTop != null) _barTop.anchoredPosition = new Vector2(0f, BarHeight * (1f - bs));
            if (_barBottom != null) _barBottom.anchoredPosition = new Vector2(0f, -BarHeight * (1f - bs));

            // text shake and flash fade
            if (_content != null)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * (_shake * 45f);
                _content.anchoredPosition = _contentBase + jitter;
            }
            if (_flash != null && _flash.color.a > 0f)
            {
                var c = _flash.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 3.5f);
                _flash.color = c;
            }
            if (_skipHint != null && _age > 1f)
            {
                var c = _skipHint.color;
                c.a = Mathf.MoveTowards(c.a, 0.55f, Time.deltaTime);
                _skipHint.color = c;
            }

            // portal
            if (_portal != null)
            {
                _portal.localScale = Vector3.one * Mathf.Max(0.0001f, _portalScale);
                _portal.gameObject.SetActive(_portalScale > 0.001f);
                if (_ringPivot != null) _ringPivot.localRotation = Quaternion.Euler(0f, 0f, _age * 70f);
                if (_spokePivot != null) _spokePivot.localRotation = Quaternion.Euler(0f, 0f, -_age * 150f);
            }
        }

        void LateUpdate()
        {
            if (Running) ApplyCamera();
        }

        void ApplyCamera()
        {
            if (_cam == null || _film == null) return;
            Vector3 p = _film.TransformPoint(_camPos) + UnityEngine.Random.insideUnitSphere * (_shake * 0.05f * _H);
            Vector3 look = _film.TransformPoint(_camLook);
            _camGo.transform.position = p;
            Vector3 dir = look - p;
            if (dir.sqrMagnitude > 1e-10f) _camGo.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            _cam.fieldOfView = _camFov;
        }

        // ------------------------------------------------------------------ building the pieces
        void BuildCamera()
        {
            _camGo = new GameObject("BossIntroCamera");
            _cam = _camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = backdrop;
            _cam.cullingMask = ~0;
            _cam.depth = 100f;                       // above the AR camera
            _cam.nearClipPlane = Mathf.Max(0.0005f, _H * 0.04f);
            _cam.farClipPlane = Mathf.Max(5f, _H * 80f);
            _cam.fieldOfView = _camFov;

            // the scene's post-processing (depth of field in particular) is made for the AR camera and would blur this picture
            var data = _cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
            }
        }

        void BuildOverlay()
        {
            _canvasGo = new GameObject("BossIntroCanvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // black bars
            _barTop = MakeBar("BarTop", true);
            _barBottom = MakeBar("BarBottom", false);

            // the text lives under 'Content' (it shakes)
            var cgo = new GameObject("Content", typeof(RectTransform));
            cgo.transform.SetParent(_canvasGo.transform, false);
            _content = (RectTransform)cgo.transform;
            _content.anchorMin = Vector2.zero; _content.anchorMax = Vector2.one;
            _content.offsetMin = _content.offsetMax = Vector2.zero;
            _contentBase = Vector2.zero;

            // white flash on every hit
            var fgo = new GameObject("Flash", typeof(RectTransform));
            fgo.transform.SetParent(_canvasGo.transform, false);
            var frt = (RectTransform)fgo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _flash = fgo.AddComponent<Image>();
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;

            // small hint, bottom right
            var hgo = new GameObject("SkipHint", typeof(RectTransform));
            hgo.transform.SetParent(_canvasGo.transform, false);
            var hrt = (RectTransform)hgo.transform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.sizeDelta = new Vector2(500f, 70f);
            hrt.anchoredPosition = new Vector2(-30f, 20f);
            _skipHint = hgo.AddComponent<TextMeshProUGUI>();
            _skipHint.text = "Tap to skip";
            _skipHint.fontSize = 34f;
            _skipHint.alignment = TextAlignmentOptions.BottomRight;
            _skipHint.color = new Color(1f, 1f, 1f, 0f);
            _skipHint.raycastTarget = false;
        }

        RectTransform MakeBar(string name, bool top)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, BarHeight);
            rt.anchoredPosition = new Vector2(0f, top ? BarHeight : -BarHeight);   // hidden above / below the screen
            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rt;
        }

        Material Mat(Color c, Color? glow)
        {
            var m = StageMaterials.Make(c);
            if (m == null) return null;
            if (glow.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow.Value);
            }
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

        // A standing ring of glowing beads around a dark disc with turning spokes, facing +z (the way the boss walks). Film units: boss height = 1.
        void BuildPortal(float distanceBehind)
        {
            var ringMat = Mat(portalColor, portalColor * 2.2f);
            var voidMat = Mat(new Color(0.03f, 0f, 0.06f, 1f), new Color(0.10f, 0f, 0.20f, 1f));
            var spokeMat = Mat(new Color(0.45f, 0.15f, 0.85f, 1f), new Color(0.9f, 0.5f, 1f, 1f) * 1.2f);
            if (ringMat == null || voidMat == null || spokeMat == null) return;   // no usable material: skip the portal, the boss just appears

            const float R = 0.8f;
            var root = new GameObject("BossPortal");
            _portal = root.transform;
            _portal.SetParent(_film, false);
            _portal.localPosition = new Vector3(0f, R * 0.95f, -distanceBehind);
            _portal.localRotation = Quaternion.identity;
            _portal.localScale = Vector3.one * 0.0001f;

            // the dark disc (a flat cylinder turned to face forward)
            var disc = Prim(PrimitiveType.Cylinder, "Void", _portal, voidMat);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(2f * R, 0.01f, 2f * R);

            // the ring
            _ringPivot = new GameObject("Ring").transform;
            _ringPivot.SetParent(_portal, false);
            const int beads = 28;
            for (int i = 0; i < beads; i++)
            {
                float a = i * Mathf.PI * 2f / beads;
                var b = Prim(PrimitiveType.Sphere, "Bead", _ringPivot, ringMat);
                b.transform.localPosition = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f);
                b.transform.localScale = Vector3.one * (R * (i % 2 == 0 ? 0.20f : 0.13f));
            }

            // turning spokes, a hair in front of the disc
            _spokePivot = new GameObject("Spokes").transform;
            _spokePivot.SetParent(_portal, false);
            _spokePivot.localPosition = new Vector3(0f, 0f, 0.012f);
            for (int i = 0; i < 3; i++)
            {
                var s = Prim(PrimitiveType.Cube, "Spoke", _spokePivot, spokeMat);
                s.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                s.transform.localScale = new Vector3(R * 1.85f, R * 0.06f, R * 0.02f);
            }
        }

        // ------------------------------------------------------------------ end
        void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;
            Running = false;

            // where the boss stands now (the stage may have moved a little during the film)
            Vector3 endPos = _film != null ? _film.position : _P;
            Quaternion endRot = _film != null ? _film.rotation : _rot;

            if (_canvasGo != null) Destroy(_canvasGo);
            if (_camGo != null) Destroy(_camGo);
            if (_portal != null) Destroy(_portal.gameObject);
            if (_film != null) Destroy(_film.gameObject);
            foreach (var m in _mats) if (m != null) Destroy(m);
            _mats.Clear();

            // the boss stands where it stands in every fight; its brain starts again below
            if (_boss != null)
            {
                _boss.SetActive(true);
                _boss.transform.SetPositionAndRotation(endPos, endRot);
                if (_bossAnimator != null) { _bossAnimator.applyRootMotion = _rootMotion; _bossAnimator.speed = 1f; }
                var ma = _boss.GetComponent<MeleeAnimator>();
                if (ma != null) ma.SetSpeed(0f);
            }
            if (_hero != null) _hero.SetActive(true);   // (he was never hidden; harmless)

            foreach (var t in _plates) if (t != null) t.enabled = true;
            foreach (var a in _ais) if (a != null) a.enabled = true;
            foreach (var b in _brains) if (b != null) b.enabled = true;

            var cb = _onDone;
            _onDone = null;
            if (cb != null) cb();

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // if the object is removed in the middle of the film (a scene change, say), put everything back
            if (!_cleaned && Running) Cleanup();
        }
    }
}
