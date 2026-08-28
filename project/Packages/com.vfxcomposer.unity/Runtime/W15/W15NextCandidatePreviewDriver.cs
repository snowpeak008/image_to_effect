using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W15NextCandidate
{
    /// <summary>Preview-only deterministic W15 comparison driver.  Its simple type name remains on the production deny-list.</summary>
    [DisallowMultipleComponent]
    public sealed class NewArchetypePreviewDriver : MonoBehaviour
    {
        [SerializeField] private W15NextCandidateController[] entries = new W15NextCandidateController[0];
        [SerializeField] private Transform[] decalAnchors = new Transform[0];
        [SerializeField] private W15NextCandidateController[] decalEntries = new W15NextCandidateController[0];
        [SerializeField] private W15NextCandidateController[] fastWeaponEntries = new W15NextCandidateController[0];
        [SerializeField] private W15NextCandidateController[] slowWeaponEntries = new W15NextCandidateController[0];
        [SerializeField] private W15NextCandidateController deathEntry;
        [SerializeField] private Renderer[] deathCharacter = new Renderer[0];
        [SerializeField] private W15NextCandidateController entranceEntry;
        [SerializeField] private Renderer[] entranceCharacter = new Renderer[0];
        [SerializeField] private W15NextCandidateController portalEntry;
        [SerializeField] private W15NextCandidateController portalExit;
        [SerializeField] private W15NextCandidateController[] lootEntries = new W15NextCandidateController[0];
        [SerializeField] private Transform lootPickupTarget;
        [SerializeField, Min(5f)] private float cycleDuration = 6f;

        private readonly List<Vector3> origins = new List<Vector3>();
        private float elapsed;
        private bool started;
        private bool pickupStarted;
        private int cycleIndex;

        public int CycleIndex { get { return cycleIndex; } }
        public float Elapsed { get { return elapsed; } }
        public int EntryCount { get { return entries == null ? 0 : entries.Length; } }

        private void Start()
        {
            origins.Clear();
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                origins.Add(entry == null ? Vector3.zero : entry.transform.position);
                if (entry != null) { entry.gameObject.SetActive(true); entry.ResetForPool(); }
            }
            BindCharacters();
            ConfigureComparisons();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (!started && elapsed >= .1f) { started = true; StartSemanticCycle(); }
            DriveWeaponComparisons();
            if (!pickupStarted && elapsed >= 3.65f)
            {
                pickupStarted = true;
                for (var index = 0; index < lootEntries.Length; index++) if (lootEntries[index] != null)
                {
                    var target = lootPickupTarget == null ? lootEntries[index].transform.position + Vector3.up * .8f : lootPickupTarget.position;
                    lootEntries[index].SetPickupTarget(target);
                    lootEntries[index].BeginPickup();
                }
            }
            if (elapsed >= cycleDuration) ResetCycle();
        }

        private void ConfigureComparisons()
        {
            if (portalEntry != null) portalEntry.ConfigurePortal("w15_next_twin_pair", PortalEndpointRole.Entry);
            if (portalExit != null) portalExit.ConfigurePortal("w15_next_twin_pair", PortalEndpointRole.Exit);
            for (var index = 0; index < lootEntries.Length; index++) if (lootEntries[index] != null) lootEntries[index].ConfigureRarity(index + 1);
        }

        private void BindCharacters()
        {
            if (deathEntry != null) deathEntry.BindCharacterRenderers(deathCharacter);
            if (entranceEntry != null) entranceEntry.BindCharacterRenderers(entranceCharacter);
        }

        private void StartSemanticCycle()
        {
            for (var index = 0; index < decalEntries.Length && index < decalAnchors.Length; index++)
            {
                var entry = decalEntries[index]; var anchor = decalAnchors[index];
                if (entry != null && anchor != null) entry.AttachToSurface("w15_next_surface_" + index, anchor.position, anchor.forward, anchor.up);
            }
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index]; if (entry == null) continue;
                if (entry.Archetype == W15NextArchetype.Destruction) entry.TriggerDestruction(new Vector3(.24f, .15f, .08f));
                else if (entry.Archetype == W15NextArchetype.LifeCycle) entry.Play();
                else if (entry.Archetype == W15NextArchetype.Loot) entry.Play();
            }
            if (portalEntry != null) portalEntry.TriggerTraverse();
            if (portalExit != null) portalExit.TriggerTraverse();
        }

        private void DriveWeaponComparisons()
        {
            for (var index = 0; index < fastWeaponEntries.Length; index++) DriveWeapon(fastWeaponEntries[index], true, index);
            for (var index = 0; index < slowWeaponEntries.Length; index++) DriveWeapon(slowWeaponEntries[index], false, index);
        }

        private void DriveWeapon(W15NextCandidateController entry, bool fast, int index)
        {
            if (entry == null) return;
            var pivot = entry.transform.position + new Vector3(0f, -.16f, 0f);
            float angle;
            if (fast)
            {
                var cycle = Mathf.Repeat(elapsed + index * .12f, 1.18f);
                var swing = Mathf.Clamp01(cycle / .36f);
                angle = Mathf.Lerp(-125f, 78f, Mathf.SmoothStep(0f, 1f, swing));
            }
            else
            {
                var cycle = Mathf.Repeat(elapsed + index * .2f, 2.5f);
                var swing = Mathf.Clamp01(cycle / 1.9f);
                angle = Mathf.Lerp(-62f, 38f, Mathf.SmoothStep(0f, 1f, swing));
            }
            var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            var root = pivot + direction * .12f;
            var tip = pivot + direction * (fast ? .72f : .68f);
            entry.DriveWeaponEndpoints(root, tip, Mathf.Max(.0001f, Time.deltaTime));
        }

        private void ResetCycle()
        {
            for (var index = 0; index < entries.Length; index++) if (entries[index] != null)
            {
                entries[index].Stop(VfxStopMode.Immediate);
                entries[index].transform.position = origins[index];
            }
            BindCharacters();
            ConfigureComparisons();
            elapsed = 0f;
            started = false;
            pickupStarted = false;
            cycleIndex++;
        }
    }
}
