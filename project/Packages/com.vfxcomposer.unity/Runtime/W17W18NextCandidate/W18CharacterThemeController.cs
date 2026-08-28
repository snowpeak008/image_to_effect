using System;
using System.Linq;
using UnityEngine;

namespace VFXComposer.W17W18NextCandidate
{
    public enum W18CharacterTheme
    {
        FlameBladeSamurai,
        IceMoonMage,
        MechanicalHunter,
        GhostCurseShrine
    }

    public enum W18KitStage
    {
        Idle,
        BasicChain,
        Mobility,
        Skill,
        Ultimate,
        Hit,
        Death,
        Entrance
    }

    public struct W18KitBudgetSnapshot
    {
        public int GameObjects;
        public int Renderers;
        public int Materials;
        public int ParticleSystems;
        public int ParticleCapacity;
    }

    /// <summary>
    /// W18-only full-skill-chain carrier. A theme changes palette, topology, attachment and timing
    /// together. Preview clipping is shader-enforced in world space and does not rely on labels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W18CharacterThemeController : MonoBehaviour, IVfxRuntimeEntry
    {
        public const int MaxRendererBudget = 14;
        public const int MaxMaterialBudget = 3;
        public const int MaxParticleSystemBudget = 1;
        public const int MaxParticleCapacity = 16;
        public const string RequiredClipShader = "VFXComposer/NextCandidate/WorldCellClip";

        [Header("Theme contract")]
        [SerializeField] private string kitId;
        [SerializeField] private W18CharacterTheme theme;
        [SerializeField] private string paletteReference;
        [SerializeField] private string shapeLanguage;
        [SerializeField, Min(.8f)] private float cycleDuration = 8f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;

        [Header("Real visual carriers")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Renderer[] ownedRenderers = new Renderer[0];
        [SerializeField] private Transform[] visualCarriers = new Transform[0];
        [SerializeField] private Transform[] handCarriers = new Transform[0];
        [SerializeField] private Transform[] weaponCarriers = new Transform[0];
        [SerializeField] private Transform[] chestCarriers = new Transform[0];
        [SerializeField] private Transform[] feetCarriers = new Transform[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];

        [Header("Bounded Preview")]
        [SerializeField] private bool previewHardClip;
        [SerializeField] private Vector4 worldClipRect = new Vector4(-1.48f, -1.04f, 1.48f, 1.04f);
        [SerializeField] private Bounds declaredLocalBounds = new Bounds(new Vector3(0f, .12f, 0f), new Vector3(2.72f, 1.86f, .8f));

        private Transform[] originalParents = new Transform[0];
        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private MaterialPropertyBlock block;
        private Transform boundHand;
        private Transform boundWeapon;
        private Transform boundChest;
        private Transform boundFeet;
        private float elapsed;
        private float lastEvaluatedTime;
        private bool playing;
        private int replayCount;
        private int stageTransitionCount;
        private int visibleGhostCount;
        private int visibleTalismanCount;
        private int basicChainIndex;
        private float dissolveProgress;
        private W18KitStage currentStage;
        private W18KitStage previousStage;

        public bool IsAlive { get { return playing; } }
        public string KitId { get { return kitId; } }
        public W18CharacterTheme Theme { get { return theme; } }
        public uint Seed { get { return seed; } }
        public string PaletteReference { get { return paletteReference; } }
        public string ShapeLanguage { get { return shapeLanguage; } }
        public float CycleDuration { get { return cycleDuration; } }
        public W18KitStage CurrentStage { get { return currentStage; } }
        public int StageTransitionCount { get { return stageTransitionCount; } }
        public int ReplayCount { get { return replayCount; } }
        public int BasicChainIndex { get { return basicChainIndex; } }
        public int VisibleGhostCount { get { return visibleGhostCount; } }
        public int VisibleTalismanCount { get { return visibleTalismanCount; } }
        public float DissolveProgress { get { return dissolveProgress; } }
        public int VisibleRendererCount { get { return ownedRenderers == null ? 0 : ownedRenderers.Count(value => value != null && value.enabled); } }
        public bool PreviewHardClip { get { return previewHardClip; } }
        public Rect PreviewClipRect { get { return new Rect(worldClipRect.x, worldClipRect.y, worldClipRect.z - worldClipRect.x, worldClipRect.w - worldClipRect.y); } }
        public Bounds DeclaredLocalBounds { get { return declaredLocalBounds; } }

        private MaterialPropertyBlock Block { get { if (block == null) block = new MaterialPropertyBlock(); return block; } }

        private void Awake()
        {
            CaptureBaseState();
            ResetForPool();
        }

        private void OnDestroy()
        {
            RestoreOriginalParents();
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (elapsed >= cycleDuration)
            {
                elapsed = Mathf.Repeat(elapsed, cycleDuration);
                replayCount++;
                previousStage = W18KitStage.Entrance;
            }
            EvaluateVisuals(elapsed);
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            if (basePositions.Length != visualCarriers.Length) CaptureBaseState();
            RestoreBaseState();
            HideAllRenderers();
            elapsed = 0f;
            lastEvaluatedTime = 0f;
            previousStage = W18KitStage.Entrance;
            currentStage = W18KitStage.Idle;
            stageTransitionCount = 0;
            replayCount++;
            playing = true;
            EvaluateVisuals(0f);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "showcase")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.Immediate); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            if (eventId == "hit") { EvaluateAt(cycleDuration * .76f); return true; }
            if (eventId == "death") { EvaluateAt(cycleDuration * .87f); return true; }
            if (eventId == "entrance") { EvaluateAt(cycleDuration * .96f); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            playing = false;
            HideAllRenderers();
            StopParticles(mode == VfxStopMode.Immediate);
            RestoreBaseState();
            ClearLines();
            RestoreOriginalParents();
        }

        public void ResetForPool()
        {
            playing = false;
            elapsed = 0f;
            lastEvaluatedTime = 0f;
            currentStage = W18KitStage.Idle;
            previousStage = W18KitStage.Idle;
            stageTransitionCount = 0;
            visibleGhostCount = 0;
            visibleTalismanCount = 0;
            basicChainIndex = 0;
            dissolveProgress = 0f;
            StopParticles(true);
            HideAllRenderers();
            ClearLines();
            RestoreBaseState();
            RestoreOriginalParents();
            ClearPropertyBlocks();
        }

        /// <summary>Moves the production carriers to an exact point in the skill cycle.</summary>
        public void EvaluateAt(float seconds)
        {
            if (!playing) Play();
            elapsed = Mathf.Repeat(Mathf.Max(0f, seconds), Mathf.Max(.8f, cycleDuration));
            EvaluateVisuals(elapsed);
        }

        public void ConfigurePreviewClip(Rect worldRect)
        {
            worldClipRect = new Vector4(worldRect.xMin, worldRect.yMin, worldRect.xMax, worldRect.yMax);
            previewHardClip = true;
            ApplyMaterialState(1f, 0f);
        }

        public void BindCharacterRig(Transform hand, Transform weapon, Transform chest, Transform feet)
        {
            RestoreOriginalParents();
            boundHand = hand;
            boundWeapon = weapon;
            boundChest = chest;
            boundFeet = feet;
            Reparent(handCarriers, boundHand);
            Reparent(weaponCarriers, boundWeapon);
            Reparent(chestCarriers, boundChest);
            Reparent(feetCarriers, boundFeet);
        }

        public bool UsesHardClipShader()
        {
            if (ownedRenderers == null || ownedRenderers.Length == 0) return false;
            return ownedRenderers.Where(value => value != null).All(value => value.sharedMaterial != null && value.sharedMaterial.shader != null && value.sharedMaterial.shader.name == RequiredClipShader);
        }

        public bool AllRenderedGeometryHasClipRect(float epsilon)
        {
            if (!previewHardClip || !UsesHardClipShader()) return false;
            foreach (var renderer in ownedRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(Block);
                var value = Block.GetVector("_ClipRect");
                if (Vector4.Distance(value, worldClipRect) > epsilon) return false;
            }
            return true;
        }

        public W18KitBudgetSnapshot ReadBudget()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            var capacity = 0;
            foreach (var system in systems) capacity += system.main.maxParticles;
            return new W18KitBudgetSnapshot
            {
                GameObjects = GetComponentsInChildren<Transform>(true).Length,
                Renderers = renderers.Length,
                Materials = renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count(),
                ParticleSystems = systems.Length,
                ParticleCapacity = capacity
            };
        }

        public Transform FindCarrier(string role)
        {
            return visualCarriers == null ? null : visualCarriers.FirstOrDefault(value => value != null && value.name == role);
        }

        private void EvaluateVisuals(float time)
        {
            lastEvaluatedTime = time;
            var normalized = Mathf.Clamp01(time / Mathf.Max(.8f, cycleDuration));
            var stage = StageAt(normalized);
            if (stage != previousStage)
            {
                stageTransitionCount++;
                previousStage = stage;
            }
            currentStage = stage;
            RestoreBaseState();
            HideAllRenderers();
            visibleGhostCount = 0;
            visibleTalismanCount = 0;
            dissolveProgress = stage == W18KitStage.Death ? Mathf.InverseLerp(.84f, .93f, normalized) : stage == W18KitStage.Entrance ? 1f - Mathf.InverseLerp(.93f, 1f, normalized) : 0f;
            basicChainIndex = stage == W18KitStage.BasicChain ? Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(.12f, .31f, normalized) * 3f), 0, 2) : 0;

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
                ApplyRenderer(bodyRenderer, 1f - dissolveProgress * .84f, normalized, dissolveProgress);
            }
            if (theme == W18CharacterTheme.FlameBladeSamurai) EvaluateFlame(normalized, stage);
            else if (theme == W18CharacterTheme.IceMoonMage) EvaluateIce(normalized, stage);
            else if (theme == W18CharacterTheme.MechanicalHunter) EvaluateMechanical(normalized, stage);
            else EvaluateGhost(normalized, stage);
            ApplyMaterialState(1f, dissolveProgress);
        }

        private void EvaluateFlame(float t, W18KitStage stage)
        {
            if (stage == W18KitStage.Idle)
            {
                var ember = Show("SheathEmber", .82f);
                SpinPulse(ember, t * 180f, .76f + .12f * Mathf.Sin(t * 70f));
            }
            else if (stage == W18KitStage.BasicChain)
            {
                for (var index = 0; index < 3; index++)
                {
                    var carrier = Show("SharpCrescent_" + index, index == basicChainIndex ? 1f : .14f);
                    if (carrier != null) carrier.localRotation = Quaternion.Euler(0f, 0f, -38f + index * 38f + t * 120f);
                }
            }
            else if (stage == W18KitStage.Mobility)
            {
                var dash = Show("DashRibbon", 1f);
                if (dash != null) dash.localScale = new Vector3(1.4f, .38f + .1f * Mathf.Sin(t * 90f), 1f);
            }
            else if (stage == W18KitStage.Skill)
            {
                SpinPulse(Show("FlameSlash", 1f), t * 260f, 1f);
            }
            else if (stage == W18KitStage.Ultimate)
            {
                var blade = Show("BladeTempest", 1f);
                SpinPulse(blade, t * 520f, 1f + .18f * Mathf.Sin(t * 80f));
                SetLine("ThemeLine", 22, .72f, 2.8f, t * 9f);
            }
            else if (stage == W18KitStage.Hit) SpinPulse(Show("ParrySpark", 1f), t * 600f, 1.2f);
            else Show(stage == W18KitStage.Death ? "DissolveEdge" : "EntranceFlare", 1f);
        }

        private void EvaluateIce(float t, W18KitStage stage)
        {
            if (stage == W18KitStage.Idle) SpinPulse(Show("StaffCharge", .9f), -t * 150f, .78f + .08f * Mathf.Sin(t * 60f));
            else if (stage == W18KitStage.BasicChain)
            {
                for (var index = 0; index < 3; index++)
                {
                    var shard = Show("HexShard_" + index, index <= basicChainIndex ? 1f : .12f);
                    if (shard != null) shard.localPosition += new Vector3((index - 1) * .18f, Mathf.Sin(t * 70f + index) * .08f, 0f);
                }
            }
            else if (stage == W18KitStage.Mobility) SpinPulse(Show("MoonWheel", 1f), t * 430f, 1f);
            else if (stage == W18KitStage.Skill)
            {
                Show("FrostNova", 1f);
                SpinPulse(Show("CrystalShield", 1f), -t * 160f, 1f);
            }
            else if (stage == W18KitStage.Ultimate)
            {
                var domain = Show("FrozenDomain", 1f);
                SpinPulse(domain, t * 80f, 1.05f + .12f * Mathf.Sin(t * 35f));
                SetLine("ThemeLine", 18, .62f, 3.2f, -t * 7f);
            }
            else if (stage == W18KitStage.Hit) Show("FrostNova", 1f);
            else Show(stage == W18KitStage.Death ? "DissolveEdge" : "EntranceFlare", 1f);
        }

        private void EvaluateMechanical(float t, W18KitStage stage)
        {
            if (stage == W18KitStage.Idle) SpinPulse(Show("OverheatVent", .86f), t * 90f, .8f + .12f * Mathf.Sin(t * 75f));
            else if (stage == W18KitStage.BasicChain)
            {
                for (var index = 0; index < 3; index++)
                {
                    var muzzle = Show("Muzzle_" + index, index == basicChainIndex ? 1f : .1f);
                    SpinPulse(muzzle, index * 30f, index == basicChainIndex ? 1.25f : .45f);
                }
            }
            else if (stage == W18KitStage.Mobility) Show("SteamDash", 1f);
            else if (stage == W18KitStage.Skill)
            {
                var scan = Show("HoloScan", 1f);
                if (scan != null) scan.localRotation = Quaternion.Euler(0f, 0f, -28f + Mathf.Sin(t * 42f) * 22f);
                SetLine("ThemeLine", 16, .48f, 2.2f, t * 5f);
            }
            else if (stage == W18KitStage.Ultimate)
            {
                Show("EmpNova", 1f);
                Show("ChainGrapple", 1f);
                SetLine("ThemeLine", 12, .35f, 1.4f, t * 14f);
            }
            else if (stage == W18KitStage.Hit) Show("EmpNova", 1f);
            else Show(stage == W18KitStage.Death ? "DissolveEdge" : "EntranceFlare", 1f);
        }

        private void EvaluateGhost(float t, W18KitStage stage)
        {
            if (stage == W18KitStage.Idle)
            {
                var talismans = Show("TalismanArray", .72f);
                SpinPulse(talismans, t * 70f, .92f + .05f * Mathf.Sin(t * 60f));
                visibleTalismanCount = talismans == null ? 0 : 8;
            }
            else if (stage == W18KitStage.BasicChain) SpinPulse(Show("InkMissile", 1f), -t * 220f, 1f);
            else if (stage == W18KitStage.Mobility) Show("GhostHand", 1f);
            else if (stage == W18KitStage.Skill)
            {
                Show("PhantomDomain", 1f);
                SpinPulse(Show("CurseMark", 1f), t * 120f, 1f);
            }
            else if (stage == W18KitStage.Ultimate)
            {
                var procession = Show("HundredGhosts", 1f);
                SpinPulse(procession, t * 75f, 1f);
                SetLine("ThemeLine", 26, .78f, 3.6f, -t * 11f);
                visibleGhostCount = 12;
            }
            else if (stage == W18KitStage.Hit) Show("CurseMark", 1f);
            else Show(stage == W18KitStage.Death ? "DissolveEdge" : "EntranceFlare", 1f);
        }

        private Transform Show(string role, float alpha)
        {
            var carrier = FindCarrier(role);
            if (carrier == null) return null;
            var renderer = carrier.GetComponent<Renderer>();
            if (renderer == null) renderer = carrier.GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                renderer.enabled = alpha > .001f;
                ApplyRenderer(renderer, alpha, Mathf.Repeat(lastEvaluatedTime / Mathf.Max(.8f, cycleDuration), 1f), dissolveProgress);
            }
            return carrier;
        }

        private static void SpinPulse(Transform carrier, float rotation, float scale)
        {
            if (carrier == null) return;
            carrier.localRotation = Quaternion.Euler(0f, 0f, rotation);
            carrier.localScale *= Mathf.Max(.05f, scale);
        }

        private void SetLine(string role, int count, float amplitude, float turns, float phase)
        {
            var carrier = FindCarrier(role);
            var line = carrier == null ? null : carrier.GetComponent<LineRenderer>();
            if (line == null) return;
            line.enabled = true;
            line.useWorldSpace = false;
            line.positionCount = Mathf.Max(2, count);
            for (var index = 0; index < line.positionCount; index++)
            {
                var u = index / (float)(line.positionCount - 1);
                var angle = u * turns * Mathf.PI * 2f + phase;
                var envelope = Mathf.Sin(u * Mathf.PI);
                line.SetPosition(index, new Vector3(Mathf.Lerp(-1.05f, 1.05f, u), Mathf.Sin(angle) * amplitude * envelope, Mathf.Cos(angle) * .08f * envelope));
            }
            ApplyRenderer(line, 1f, Mathf.Repeat(lastEvaluatedTime / cycleDuration, 1f), dissolveProgress);
        }

        private void ApplyMaterialState(float alpha, float dissolve)
        {
            if (ownedRenderers == null) return;
            var phase = Mathf.Repeat(lastEvaluatedTime / Mathf.Max(.8f, cycleDuration), 1f);
            foreach (var renderer in ownedRenderers) if (renderer != null) ApplyRenderer(renderer, renderer.enabled ? alpha : 0f, phase, dissolve);
        }

        private void ApplyRenderer(Renderer renderer, float alpha, float phase, float dissolve)
        {
            renderer.GetPropertyBlock(Block);
            Block.SetColor("_PrimaryColor", primary);
            Block.SetColor("_SecondaryColor", secondary);
            Block.SetColor("_AccentColor", accent);
            Block.SetColor("_Color", Color.Lerp(primary, secondary, Mathf.PingPong(phase * 2f, 1f)));
            Block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
            Block.SetFloat("_Phase", phase);
            Block.SetFloat("_Dissolve", dissolve);
            Block.SetVector("_ClipRect", worldClipRect);
            Block.SetFloat("_UseClip", previewHardClip ? 1f : 0f);
            renderer.SetPropertyBlock(Block);
        }

        private static W18KitStage StageAt(float normalized)
        {
            if (normalized < .12f) return W18KitStage.Idle;
            if (normalized < .31f) return W18KitStage.BasicChain;
            if (normalized < .41f) return W18KitStage.Mobility;
            if (normalized < .55f) return W18KitStage.Skill;
            if (normalized < .74f) return W18KitStage.Ultimate;
            if (normalized < .84f) return W18KitStage.Hit;
            if (normalized < .93f) return W18KitStage.Death;
            return W18KitStage.Entrance;
        }

        private void CaptureBaseState()
        {
            visualCarriers = visualCarriers ?? new Transform[0];
            ownedRenderers = ownedRenderers ?? new Renderer[0];
            lines = lines ?? new LineRenderer[0];
            particles = particles ?? new ParticleSystem[0];
            originalParents = new Transform[visualCarriers.Length];
            basePositions = new Vector3[visualCarriers.Length];
            baseRotations = new Quaternion[visualCarriers.Length];
            baseScales = new Vector3[visualCarriers.Length];
            for (var index = 0; index < visualCarriers.Length; index++)
            {
                var carrier = visualCarriers[index];
                if (carrier == null) continue;
                originalParents[index] = carrier.parent;
                basePositions[index] = carrier.localPosition;
                baseRotations[index] = carrier.localRotation;
                baseScales[index] = carrier.localScale;
            }
        }

        private void RestoreBaseState()
        {
            for (var index = 0; index < visualCarriers.Length && index < basePositions.Length; index++)
            {
                var carrier = visualCarriers[index];
                if (carrier == null) continue;
                carrier.localPosition = basePositions[index];
                carrier.localRotation = baseRotations[index];
                carrier.localScale = baseScales[index];
            }
        }

        private void RestoreOriginalParents()
        {
            if (originalParents == null || originalParents.Length != visualCarriers.Length) return;
            for (var index = 0; index < visualCarriers.Length; index++)
            {
                var carrier = visualCarriers[index];
                if (carrier == null || originalParents[index] == null || carrier.parent == originalParents[index]) continue;
                carrier.SetParent(originalParents[index], false);
                carrier.localPosition = basePositions[index];
                carrier.localRotation = baseRotations[index];
                carrier.localScale = baseScales[index];
            }
            boundHand = null;
            boundWeapon = null;
            boundChest = null;
            boundFeet = null;
        }

        private static void Reparent(Transform[] values, Transform parent)
        {
            if (values == null || parent == null) return;
            foreach (var value in values)
            {
                if (value == null) continue;
                value.SetParent(parent, false);
                value.localPosition = Vector3.zero;
                value.localRotation = Quaternion.identity;
            }
        }

        private void HideAllRenderers()
        {
            if (ownedRenderers != null) foreach (var renderer in ownedRenderers) if (renderer != null) renderer.enabled = false;
        }

        private void ClearLines()
        {
            if (lines == null) return;
            foreach (var line in lines) if (line != null) { line.positionCount = 0; line.enabled = false; }
        }

        private void StopParticles(bool clear)
        {
            if (particles == null) return;
            foreach (var particle in particles) if (particle != null) particle.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void ClearPropertyBlocks()
        {
            if (ownedRenderers == null) return;
            foreach (var renderer in ownedRenderers) if (renderer != null) renderer.SetPropertyBlock(null);
        }
    }
}
