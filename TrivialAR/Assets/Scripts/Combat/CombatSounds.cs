using UnityEngine;
using Fighters;
using Game;

namespace Combat
{
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
        [Tooltip("The hit landed on a raised shield.")]
        public Slot blocked = new Slot { volume = 0.85f };

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

        [Header("Not used by the code yet (parked here so they stay in one place)")]
        public AudioClip[] spare;

        // ------------------------------------------------------------------ used by FighterBrain
        public float VoicePitchFor(bool enemy)
        {
            float p = 1f + Random.Range(-voicePitchSpread, voicePitchSpread);
            return enemy ? p * enemyVoicePitch : p;
        }

        public void PlaySwing(float hitDelay, float voicePitch)
        {
            swing.Play(Mathf.Max(0f, hitDelay - swingLead), 1f);
            if (voicesEnabled) attackGrunt.Play(Mathf.Max(0f, hitDelay - gruntLead), voicePitch);
        }

        public void PlayImpact(string styleKey, bool wasBlocked)
        {
            if (wasBlocked) { blocked.Play(0f, 1f); return; }
            if (styleKey == ProgressionRules.Unarmed) hitUnarmed.Play(0f, 1f);
            else if (styleKey == ProgressionRules.TwoHand) hitHeavy.Play(0f, 1f);
            else hitWeapon.Play(0f, 1f);
        }

        public void PlayHurt(float voicePitch) { if (voicesEnabled) hurtGrunt.Play(0.03f, voicePitch); }
        public void PlayDeath(float voicePitch) { if (voicesEnabled) death.Play(deathDelay, voicePitch); }
        public void PlayDodge() { dodge.Play(0f, 1f); }
        public void PlayPickup() { weaponPickup.Play(0f, 1f); }
    }
}
