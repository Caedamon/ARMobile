using Characters;
using Combat;
using Core;
using UnityEngine;

namespace Fighters
{
    /// <summary>Put on a spawned fighter. Pushes FighterData into the components that use it.</summary>
    [DisallowMultipleComponent]
    public sealed class FighterIdentity : MonoBehaviour
    {
        public FighterData Data { get; private set; }

        public void Apply(FighterData d)
        {
            Data = d;
            gameObject.name = d.displayName + " (" + d.modelId + ")";

            // Team layer on the whole hierarchy. The portrait cameras only render their team's layer, and most
            // fighter prefabs only have the ROOT on that layer (the meshes are on Default), so portraits were empty.
            var ai = GetComponent<MeleeAI>();
            int layer = LayerMask.NameToLayer(ai != null && ai.team == Team.Enemy ? "Enemy" : "Main");
            if (layer >= 0) SetLayerRecursive(transform, layer);
            else Debug.LogWarning("[FighterIdentity] Layer 'Main' or 'Enemy' not found in Project Settings > Tags and Layers.", this);

            // Stats (fighter prefabs don't have this component yet, so add it)
            var stats = GetComponent<CharacterStats>();
            if (stats == null) stats = gameObject.AddComponent<CharacterStats>();
            stats.MIGHT = d.might;
            stats.EGO = d.ego;
            stats.EDUCATION = d.education;
            stats.INSIGHT = d.insight;
            stats.LUCK = d.luck;
            stats.Level = Mathf.Max(1, d.level);

            // MIGHT -> max HP
            var hp = GetComponent<Health>();
            if (hp != null)
            {
                hp.maxHP = StatRules.MaxHP(d);
                hp.ResetHP();
            }

            // Preferred weapon style + INSIGHT -> how far they notice weapons
            var seeker = GetComponent<WeaponSeeker>();
            if (seeker != null)
            {
                seeker.hasPreferredClass = d.hasPreferredClass;
                seeker.preferredClass = d.preferredClass;
                seeker.searchRadius = StatRules.SearchRadius(d);
            }
        }

        public static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
