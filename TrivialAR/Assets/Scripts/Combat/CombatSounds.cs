using UnityEngine;
using Fighters;
using Game;
using Weaponary;

namespace Combat
{
    /// <summary>How a fighter's voice sounds. Picked per fighter from the Fighter Gender Tag on its prefab (see FighterBrain).</summary>
    public enum VoiceStyle { Normal, Female, Undead, Robot }

    /// <summary>
    /// One voice style: a pitch change plus optional effects (applied live, so you can tweak the numbers in Play mode and hear it),
    /// and optional recordings of its own. The recordings are the BEST way to get a real female / undead / robot voice: drop clips into
    /// the three lists and they are used instead of the normal grunts (the pitch and effects still apply on top).
    /// </summary>
    [System.Serializable]
    public sealed class VoiceLook
    {
        [Tooltip("Voice pitch multiplier. Higher = higher voice (and a little faster, it is the same as speeding the recording up).")]
        [Range(0.5f, 1.6f)] public float pitch = 1f;

        [Header("Effects (all optional)")]
        public bool lowPass;
        [Tooltip("Muffles everything above this frequency. Lower = more muffled / hollow.")]
        [Range(300f, 22000f)] public float lowPassHz = 3000f;
        public bool distortion;
        [Range(0f, 1f)] public float distortionLevel = 0.4f;
        public bool echo;
        [Tooltip("A very short echo (20-60 ms) makes a metallic, robotic buzz. A long one (200+) makes a real echo.")]
        [Range(10f, 500f)] public float echoDelayMs = 40f;
        [Range(0f, 1f)] public float echoDecay = 0.5f;
        [Range(0f, 1f)] public float echoWet = 0.5f;
        public bool chorus;
        [Range(0f, 1f)] public float chorusDepth = 0.5f;
        [Range(0f, 20f)] public float chorusRate = 3f;
        public bool reverb;
        public AudioReverbPreset reverbPreset = AudioReverbPreset.Cave;

        [Header("Own recordings (empty = use the normal voice clips)")]
        public AudioClip[] attackClips;
        public AudioClip[] hurtClips;
        public AudioClip[] deathClips;

        public bool UsesEffects { get { return lowPass || distortion || echo || chorus || reverb; } }

        public static VoiceLook DefaultFemale() { return new VoiceLook { pitch = 1.28f }; }

        public static VoiceLook DefaultUndead()
        {
            return new VoiceLook { pitch = 0.72f, lowPass = true, lowPassHz = 3200f, distortion = true, distortionLevel = 0.18f, reverb = true, reverbPreset = AudioReverbPreset.Cave };
        }

        public static VoiceLook DefaultRobot()
        {
            return new VoiceLook { pitch = 0.92f, distortion = true, distortionLevel = 0.5f, echo = true, echoDelayMs = 30f, echoDecay = 0.45f, echoWet = 0.55f, chorus = true, chorusDepth = 0.5f, chorusRate = 3f };
        }
    }

    /// <summary>
    /// Every fight sound in one asset (Assets/Data/CombatSounds.asset, made by Tools > Kaiju > Audio > Set Up Audio).
    /// Each slot is a list of clips (one is picked at random each time) with its own volume, chance and pitch range.
    /// Swap clips, change volumes or switch the voices off here; no code needed.
    /// All volumes are multiplied by the Effects slider in Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Kaiju/Combat Sounds", fileName = "CombatSounds")]
    public sealed class CombatSounds : ScriptableObject
    {
        [System.Serializable]
        public sealed class Slot
        {
            [Tooltip("One of these is picked at random. Empty = silent.")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("1 = every time. 0.4 = four times in ten.")]
            [Range(0f, 1f)] public float chance = 1f;
            [Tooltip("Random pitch between these two (1 = as recorded).")]
            public Vector2 pitch = new Vector2(0.95f, 1.05f);

            public void Play(float delay, float pitchMul)
            {
                if (clips == null || clips.Length == 0) return;
                if (chance < 1f && Random.value > chance) return;
                var c = clips[Random.Range(0, clips.Length)];
                if (c == null) return;
                GameAudio.PlayEffect(c, volume, Random.Range(pitch.x, pitch.y) * pitchMul, delay);
            }

            /// <summary>A voice line in a voice style: the style's own recordings if it has any, its pitch and its effects.</summary>
            public void PlayVoice(float delay, float pitchMul, VoiceStyle style, VoiceLook look, AudioClip[] own)
            {
                var list = own != null && own.Length > 0 ? own : clips;
                if (list == null || list.Length == 0) return;
                if (chance < 1f && Random.value > chance) return;
                var c = list[Random.Range(0, list.Length)];
                if (c == null) return;
                float p = Random.Range(pitch.x, pitch.y) * pitchMul * (look != null ? look.pitch : 1f);
                GameAudio.PlayVoice(c, volume, p, delay, style, look);
            }
        }

        [Header("Swings and moves")]
        [Tooltip("Whoosh of every swing.")]
        public Slot swing = new Slot { volume = 0.55f };
        [Tooltip("Seconds before the hit lands that the whoosh starts (the whoosh clips peak ~0.1 s in).")]
        public float swingLead = 0.08f;
        public Slot dodge = new Slot { volume = 0.5f };
        [Tooltip("Picking up a weapon.")]
        public Slot weaponPickup = new Slot { volume = 0.6f };

        [Header("Impacts (played when a hit connects)")]
        public Slot hitUnarmed = new Slot { volume = 0.8f };
        public Slot hitWeapon = new Slot { volume = 0.8f };
        [Tooltip("Two-handed weapons.")]
        public Slot hitHeavy = new Slot { volume = 0.85f };
        [Tooltip("The hit landed on a raised METAL shield (every shield that is not on the wood list below).")]
        public Slot blocked = new Slot { volume = 0.85f };
        [Tooltip("The hit landed on a raised WOOD shield.")]
        public Slot blockedWood = new Slot { volume = 0.85f };
        [Tooltip("Names of the shield PREFABS (the model in the weapon's Offhand Visual Prefab) that count as wood. Exact name, not case sensitive. Everything else is metal.")]
        public string[] woodShields = { "paladin_shield", "Round_Shield" };

        [Header("Voices (the 'Action Combat Voice Pack'). Turn the whole group off here if you don't like them.")]
        public bool voicesEnabled = true;
        public Slot attackGrunt = new Slot { volume = 0.4f, chance = 0.4f };
        [Tooltip("Seconds before the hit lands that the grunt starts (the grunt clips peak ~0.2 to 0.45 s in).")]
        public float gruntLead = 0.2f;
        public Slot hurtGrunt = new Slot { volume = 0.4f, chance = 0.6f };
        public Slot death = new Slot { volume = 0.5f, pitch = new Vector2(0.97f, 1.03f) };
        [Tooltip("Seconds after the fighter goes down before the death voice starts.")]
        public float deathDelay = 0.1f;
        [Tooltip("Every fighter gets its own voice pitch for the whole fight (1 +/- this). Higher = more different.")]
        [Range(0f, 0.3f)] public float voicePitchSpread = 0.08f;
        [Tooltip("Enemies talk lower by this factor, so you can tell who is who.")]
        [Range(0.6f, 1.2f)] public float enemyVoicePitch = 0.88f;

        [Header("Voice styles: who gets which is set on each prefab's Fighter Gender Tag (Female / Undead / Robot; Either = by the name)")]
        public VoiceLook femaleVoice = VoiceLook.DefaultFemale();
        public VoiceLook undeadVoice = VoiceLook.DefaultUndead();
        public VoiceLook robotVoice = VoiceLook.DefaultRobot();

        [Header("Not used by the code yet (parked here so they stay in one place)")]
        public AudioClip[] spare;

        // ------------------------------------------------------------------ used by FighterBrain
        public float VoicePitchFor(bool enemy)
        {
            float p = 1f + Random.Range(-voicePitchSpread, voicePitchSpread);
            return enemy ? p * enemyVoicePitch : p;
        }

        VoiceLook LookOf(VoiceStyle style)
        {
            switch (style)
            {
                case VoiceStyle.Female: return femaleVoice;
                case VoiceStyle.Undead: return undeadVoice;
                case VoiceStyle.Robot:  return robotVoice;
                default:                return null;
            }
        }

        public void PlaySwing(float hitDelay, float voicePitch, VoiceStyle style = VoiceStyle.Normal)
        {
            swing.Play(Mathf.Max(0f, hitDelay - swingLead), 1f);
            if (!voicesEnabled) return;
            var look = LookOf(style);
            attackGrunt.PlayVoice(Mathf.Max(0f, hitDelay - gruntLead), voicePitch, style, look, look != null ? look.attackClips : null);
        }

        public bool IsWoodShield(WeaponDefinition shieldLoadout)
        {
            if (shieldLoadout == null || shieldLoadout.offhandVisualPrefab == null || woodShields == null) return false;
            string n = shieldLoadout.offhandVisualPrefab.name;
            foreach (var w in woodShields)
                if (!string.IsNullOrEmpty(w) && string.Equals(w.Trim(), n, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <param name="defenderWeapon">The weapon (loadout) of the fighter who blocked; decides wood or metal.</param>
        public void PlayImpact(string styleKey, bool wasBlocked, WeaponDefinition defenderWeapon)
        {
            if (wasBlocked)
            {
                if (IsWoodShield(defenderWeapon)) blockedWood.Play(0f, 1f); else blocked.Play(0f, 1f);
                return;
            }
            if (styleKey == ProgressionRules.Unarmed) hitUnarmed.Play(0f, 1f);
            else if (styleKey == ProgressionRules.TwoHand) hitHeavy.Play(0f, 1f);
            else hitWeapon.Play(0f, 1f);
        }

        public void PlayHurt(float voicePitch, VoiceStyle style = VoiceStyle.Normal)
        {
            if (!voicesEnabled) return;
            var look = LookOf(style);
            hurtGrunt.PlayVoice(0.03f, voicePitch, style, look, look != null ? look.hurtClips : null);
        }

        public void PlayDeath(float voicePitch, VoiceStyle style = VoiceStyle.Normal)
        {
            if (!voicesEnabled) return;
            var look = LookOf(style);
            death.PlayVoice(deathDelay, voicePitch, style, look, look != null ? look.deathClips : null);
        }
        public void PlayDodge() { dodge.Play(0f, 1f); }
        public void PlayPickup() { weaponPickup.Play(0f, 1f); }
    }
}
