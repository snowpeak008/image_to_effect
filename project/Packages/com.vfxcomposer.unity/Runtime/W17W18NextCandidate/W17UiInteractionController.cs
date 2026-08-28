using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VFXComposer.W17W18NextCandidate
{
    public enum W17UiEffectKind
    {
        ButtonPress,
        ButtonConfirm,
        CardFlip,
        CardMerge,
        ChestOpen,
        GachaSingle,
        GachaTen,
        RewardFly,
        DailyStamp,
        ProgressCharge
    }

    public struct W17UiBudgetSnapshot
    {
        public int GameObjects;
        public int Graphics;
        public int Materials;
        public int ParticleSystems;
        public int PooledRewards;
    }

    /// <summary>
    /// W17-only Canvas Runtime Entry. Every protocol mutates real RectTransform/Graphic carriers;
    /// the state properties below are diagnostics for those carriers, never replacement visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W17UiInteractionController : MonoBehaviour, IVfxRuntimeEntry
    {
        public const int NormalUiElementBudget = 24;
        public const int GachaUiElementBudget = 48;
        public const int RewardPoolCapacity = 12;

        [Header("Identity")]
        [SerializeField] private string effectId;
        [SerializeField] private W17UiEffectKind kind;
        [SerializeField, Min(.08f)] private float duration = .8f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;

        [Header("Canvas carrier")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private RectMask2D hardClip;
        [SerializeField] private RectTransform effectRoot;
        [SerializeField] private Graphic[] graphics = new Graphic[0];
        [SerializeField] private RectTransform[] carriers = new RectTransform[0];

        [Header("Protocol defaults")]
        [SerializeField, Range(1, 5)] private int rarity = 3;
        [SerializeField, Range(1, RewardPoolCapacity)] private int itemCount = 8;
        [SerializeField, Range(2, 3)] private int mergeSourceCount = 2;
        [SerializeField, Min(0f)] private float rewardStagger = .055f;
        [SerializeField, Min(0f)] private float rewardArcHeight = 62f;
        [SerializeField, Range(0f, 1f)] private float fillRatio;
        [SerializeField] private Vector2 buttonRectSize = new Vector2(140f, 70f);

        private Vector2[] basePositions = new Vector2[0];
        private Vector2[] baseSizes = new Vector2[0];
        private Vector3[] baseScales = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Color[] baseColors = new Color[0];
        private readonly int[] tenRarities = { 1, 2, 1, 3, 2, 1, 4, 2, 1, 5 };
        private RectTransform anchorRect;
        private RectTransform[] mergeSources = new RectTransform[0];
        private RectTransform mergeResult;
        private Vector2 rewardStart = new Vector2(-112f, -45f);
        private Vector2 rewardEnd = new Vector2(112f, 48f);
        private Vector3 anchorBaseScale = Vector3.one;
        private Vector3 anchorReturnPosition;
        private Quaternion anchorReturnRotation = Quaternion.identity;
        private Vector3 anchorReturnScale = Vector3.one;
        private bool hasAnchorReturnState;
        private float elapsed;
        private float lastEvaluatedTime;
        private bool playing;
        private bool followAnchor;
        private bool skipped;
        private int playCount;
        private int activeRewardCount;
        private int peakRewardCount;
        private int revealGeneration;
        private string lastProtocolErrorCode;

        public bool IsAlive { get { return playing; } }
        public string EffectId { get { return effectId; } }
        public W17UiEffectKind Kind { get { return kind; } }
        public uint Seed { get { return seed; } }
        public int Rarity { get { return rarity; } }
        public int ItemCount { get { return itemCount; } }
        public int MergeSourceCount { get { return mergeSourceCount; } }
        public Vector2 ButtonRectSize { get { return buttonRectSize; } }
        public float FillRatio { get { return fillRatio; } }
        public float LastEvaluatedTime { get { return lastEvaluatedTime; } }
        public float NormalizedTime { get { return Mathf.Clamp01(lastEvaluatedTime / Mathf.Max(.08f, duration)); } }
        public bool WasSkipped { get { return skipped; } }
        public int PlayCount { get { return playCount; } }
        public int ActiveRewardCount { get { return activeRewardCount; } }
        public int PeakRewardCount { get { return peakRewardCount; } }
        public int RevealGeneration { get { return revealGeneration; } }
        public string LastProtocolErrorCode { get { return lastProtocolErrorCode; } }
        public bool HasHardClip { get { return hardClip != null && hardClip.enabled; } }
        public Rect ClipRect { get { return hardClip == null ? new Rect() : hardClip.rectTransform.rect; } }
        public int VisibleGraphicCount { get { return graphics == null ? 0 : graphics.Count(value => value != null && value.enabled && value.color.a > .001f); } }
        public bool IsRevealVisible { get { return VisibleRolePrefix("RarityBurst_") || VisibleRolePrefix("TenCard_") || VisibleRole("RevealFlash"); } }

        private void Awake()
        {
            CaptureBaseState();
            anchorBaseScale = canvasRoot == null ? Vector3.one : canvasRoot.localScale;
            ResetForPool();
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            EvaluateVisuals(elapsed);
            if (kind != W17UiEffectKind.ProgressCharge && elapsed >= duration) Stop(VfxStopMode.Immediate);
            else if (kind == W17UiEffectKind.ProgressCharge && elapsed >= duration) elapsed -= duration;
        }

        private void LateUpdate()
        {
            if (playing) ApplyAnchorFollow();
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            if (basePositions.Length != carriers.Length) CaptureBaseState();
            RestoreBaseState();
            HideAllGraphics();
            elapsed = 0f;
            lastEvaluatedTime = 0f;
            skipped = false;
            activeRewardCount = 0;
            peakRewardCount = 0;
            playing = true;
            playCount++;
            if (canvas != null) canvas.enabled = true;
            EvaluateVisuals(0f);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "trigger")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.Immediate); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            if (eventId == "skip_to_reveal") return SkipToReveal();
            lastProtocolErrorCode = "E-W17-UNKNOWN-EVENT";
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            playing = false;
            activeRewardCount = 0;
            HideAllGraphics();
            RestoreBaseState();
            if (canvas != null) canvas.enabled = false;
        }

        public void ResetForPool()
        {
            playing = false;
            elapsed = 0f;
            lastEvaluatedTime = 0f;
            skipped = false;
            activeRewardCount = 0;
            peakRewardCount = 0;
            lastProtocolErrorCode = null;
            HideAllGraphics();
            RestoreBaseState();
            RestoreAnchorReturnState();
            anchorRect = null;
            followAnchor = false;
            mergeSources = new RectTransform[0];
            mergeResult = null;
            if (canvas != null) canvas.enabled = false;
        }

        /// <summary>Deterministic sampling used by Preview and machine tests; it moves the same real carriers as Update.</summary>
        public void EvaluateAt(float seconds)
        {
            if (!playing) Play();
            elapsed = Mathf.Max(0f, seconds);
            EvaluateVisuals(elapsed);
        }

        public bool SetAnchorRect(RectTransform value, bool follow)
        {
            if (follow && value == null)
            {
                lastProtocolErrorCode = "E-W17-ANCHOR-NULL";
                return false;
            }
            if (!follow)
            {
                RestoreAnchorReturnState();
                anchorRect = value;
                followAnchor = false;
                lastProtocolErrorCode = null;
                return true;
            }
            if (!followAnchor && canvasRoot != null)
            {
                anchorReturnPosition = canvasRoot.position;
                anchorReturnRotation = canvasRoot.rotation;
                anchorReturnScale = canvasRoot.localScale;
                anchorBaseScale = canvasRoot.localScale;
                hasAnchorReturnState = true;
            }
            anchorRect = value;
            followAnchor = true;
            lastProtocolErrorCode = null;
            ApplyAnchorFollow();
            return true;
        }

        public bool SetRarity(int value)
        {
            if (kind != W17UiEffectKind.CardFlip && kind != W17UiEffectKind.CardMerge && kind != W17UiEffectKind.GachaSingle && kind != W17UiEffectKind.GachaTen)
            {
                lastProtocolErrorCode = "E-W17-RARITY-UNSUPPORTED";
                return false;
            }
            rarity = Mathf.Clamp(value, 1, 5);
            lastProtocolErrorCode = null;
            if (playing) EvaluateVisuals(elapsed);
            return true;
        }

        public bool SetTenRarities(int[] values)
        {
            if (kind != W17UiEffectKind.GachaTen || values == null || values.Length != 10 || values.Any(value => value < 1 || value > 5))
            {
                lastProtocolErrorCode = "E-W17-TEN-RARITY";
                return false;
            }
            Array.Copy(values, tenRarities, 10);
            rarity = tenRarities.Max();
            lastProtocolErrorCode = null;
            if (playing) EvaluateVisuals(elapsed);
            return true;
        }

        public bool SetMergeAnchors(RectTransform[] sources, RectTransform result)
        {
            if (kind != W17UiEffectKind.CardMerge || sources == null || sources.Length < 2 || sources.Length > 3 || sources.Any(value => value == null) || result == null)
            {
                lastProtocolErrorCode = "E-W17-MERGE-ANCHORS";
                return false;
            }
            mergeSources = sources.ToArray();
            mergeResult = result;
            mergeSourceCount = sources.Length;
            lastProtocolErrorCode = null;
            return true;
        }

        public bool SetRewardRoute(Vector2 start, Vector2 end, int count, float arcHeight, float stagger)
        {
            if (kind != W17UiEffectKind.RewardFly || count < 1 || count > RewardPoolCapacity || !Finite(start) || !Finite(end) || !Finite(arcHeight) || !Finite(stagger))
            {
                lastProtocolErrorCode = "E-W17-REWARD-ROUTE";
                return false;
            }
            rewardStart = start;
            rewardEnd = end;
            itemCount = count;
            rewardArcHeight = Mathf.Max(0f, arcHeight);
            rewardStagger = Mathf.Max(0f, stagger);
            lastProtocolErrorCode = null;
            if (playing) EvaluateVisuals(elapsed);
            return true;
        }

        public bool SetFillRatio(float value)
        {
            if (kind != W17UiEffectKind.ProgressCharge || float.IsNaN(value) || float.IsInfinity(value))
            {
                lastProtocolErrorCode = "E-W17-FILL-UNSUPPORTED";
                return false;
            }
            fillRatio = Mathf.Clamp01(value);
            lastProtocolErrorCode = null;
            if (playing) EvaluateVisuals(elapsed);
            return true;
        }

        public bool SetButtonRectSize(Vector2 value)
        {
            if ((kind != W17UiEffectKind.ButtonPress && kind != W17UiEffectKind.ButtonConfirm) || !Finite(value) || value.x < 52f || value.y < 30f || value.x > 260f || value.y > 120f)
            {
                lastProtocolErrorCode = "E-W17-BUTTON-RECT";
                return false;
            }
            buttonRectSize = value;
            lastProtocolErrorCode = null;
            if (playing) EvaluateVisuals(elapsed);
            return true;
        }

        public bool SkipToReveal()
        {
            if (kind != W17UiEffectKind.GachaSingle && kind != W17UiEffectKind.GachaTen)
            {
                lastProtocolErrorCode = "E-W17-SKIP-UNSUPPORTED";
                return false;
            }
            if (!playing) Play();
            skipped = true;
            elapsed = Mathf.Max(elapsed, duration * .74f);
            revealGeneration++;
            lastProtocolErrorCode = null;
            EvaluateVisuals(elapsed);
            return true;
        }

        public W17UiBudgetSnapshot ReadBudget()
        {
            var allGraphics = GetComponentsInChildren<Graphic>(true);
            return new W17UiBudgetSnapshot
            {
                GameObjects = GetComponentsInChildren<Transform>(true).Length,
                Graphics = allGraphics.Length,
                Materials = allGraphics.Select(value => value.materialForRendering).Where(value => value != null).Distinct().Count(),
                ParticleSystems = GetComponentsInChildren<ParticleSystem>(true).Length,
                PooledRewards = carriers == null ? 0 : carriers.Count(value => value != null && value.name.StartsWith("RewardItem_", StringComparison.Ordinal))
            };
        }

        public bool AllVisibleCornersInsideClip(float epsilon)
        {
            if (hardClip == null) return false;
            var clip = hardClip.rectTransform.rect;
            var corners = new Vector3[4];
            foreach (var graphic in graphics)
            {
                if (graphic == null || !graphic.enabled || graphic.color.a <= .001f) continue;
                graphic.rectTransform.GetWorldCorners(corners);
                for (var index = 0; index < corners.Length; index++)
                {
                    var local = hardClip.rectTransform.InverseTransformPoint(corners[index]);
                    if (local.x < clip.xMin - epsilon || local.x > clip.xMax + epsilon || local.y < clip.yMin - epsilon || local.y > clip.yMax + epsilon) return false;
                }
            }
            return true;
        }

        public RectTransform FindCarrier(string role)
        {
            return carriers == null ? null : carriers.FirstOrDefault(value => value != null && value.name == role);
        }

        private void EvaluateVisuals(float time)
        {
            lastEvaluatedTime = time;
            RestoreBaseState();
            HideAllGraphics();
            var normalized = kind == W17UiEffectKind.ProgressCharge ? Mathf.Repeat(time / Mathf.Max(.08f, duration), 1f) : Mathf.Clamp01(time / Mathf.Max(.08f, duration));
            switch (kind)
            {
                case W17UiEffectKind.ButtonPress: EvaluateButtonPress(normalized); break;
                case W17UiEffectKind.ButtonConfirm: EvaluateButtonConfirm(normalized); break;
                case W17UiEffectKind.CardFlip: EvaluateCardFlip(normalized); break;
                case W17UiEffectKind.CardMerge: EvaluateCardMerge(normalized); break;
                case W17UiEffectKind.ChestOpen: EvaluateChest(normalized); break;
                case W17UiEffectKind.GachaSingle: EvaluateGachaSingle(normalized); break;
                case W17UiEffectKind.GachaTen: EvaluateGachaTen(normalized); break;
                case W17UiEffectKind.RewardFly: EvaluateReward(time); break;
                case W17UiEffectKind.DailyStamp: EvaluateStamp(normalized); break;
                case W17UiEffectKind.ProgressCharge: EvaluateProgress(normalized); break;
            }
            ClampAllCarriersToClip();
        }

        private void EvaluateButtonPress(float t)
        {
            SetRect("ButtonSurface", Vector2.zero, buttonRectSize, 0f, Vector3.one);
            Show("ButtonSurface", primary, .34f + .12f * Mathf.Sin(t * Mathf.PI));
            SetRect("Ripple", Vector2.zero, Vector2.one * Mathf.Lerp(18f, Mathf.Min(buttonRectSize.x, buttonRectSize.y) * 1.65f, EaseOut(t)), 0f, Vector3.one);
            Show("Ripple", secondary, 1f - t);
            var edgeSize = buttonRectSize - new Vector2(5f, 5f);
            var radius = Mathf.Clamp(buttonRectSize.y * .18f, 7f, 18f);
            SetRect("EdgeSweep", RoundedRectPoint(t, edgeSize, radius), new Vector2(24f, 7f), RoundedRectAngle(t, edgeSize, radius), Vector3.one);
            Show("EdgeSweep", accent, Mathf.Sin(Mathf.PI * t));
            for (var index = 0; index < 2; index++)
            {
                var phase = Mathf.Clamp01((t - .22f - index * .08f) / .6f);
                var p = new Vector2(index == 0 ? -52f : 58f, -2f) + new Vector2(index == 0 ? -18f : 14f, 34f) * EaseOut(phase);
                SetRect("Star_" + index, p, Vector2.one * Mathf.Lerp(5f, 15f, Mathf.Sin(Mathf.PI * phase)), phase * 140f, Vector3.one);
                Show("Star_" + index, accent, Mathf.Sin(Mathf.PI * phase));
            }
        }

        private void EvaluateButtonConfirm(float t)
        {
            SetRect("ButtonSurface", Vector2.zero, buttonRectSize, 0f, Vector3.one);
            Show("ButtonSurface", primary, .4f);
            SetRect("ConfirmRing", Vector2.zero, Vector2.one * Mathf.Lerp(45f, 176f, EaseOut(t)), 0f, Vector3.one);
            Show("ConfirmRing", secondary, 1f - t);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * 45f;
                var radius = Mathf.Lerp(30f, 84f, EaseOut(t));
                SetRect("Ray_" + index, Direction(angle) * radius, new Vector2(5f, Mathf.Lerp(12f, 32f, t)), -angle, Vector3.one);
                Show("Ray_" + index, accent, Mathf.Sin(Mathf.PI * t));
            }
            var edgeSize = buttonRectSize - new Vector2(4f, 4f);
            var edgeRadius = Mathf.Clamp(buttonRectSize.y * .2f, 7f, 20f);
            SetRect("EdgeSweep", RoundedRectPoint(t, edgeSize, edgeRadius), new Vector2(28f, 8f), RoundedRectAngle(t, edgeSize, edgeRadius), Vector3.one);
            Show("EdgeSweep", accent, 1f);
        }

        private void EvaluateCardFlip(float t)
        {
            var flip = Mathf.Cos(t * Mathf.PI);
            SetRect("CardBody", Vector2.zero, new Vector2(88f, 122f), 0f, new Vector3(Mathf.Max(.035f, Mathf.Abs(flip)), 1f, 1f));
            Show("CardBody", flip >= 0f ? primary : RarityColor(rarity), .86f);
            var flash = 1f - Mathf.Clamp01(Mathf.Abs(t - .5f) / .16f);
            SetRect("RevealFlash", Vector2.zero, Vector2.one * Mathf.Lerp(40f, 152f, flash), 45f, Vector3.one);
            Show("RevealFlash", accent, flash);
            var reveal = Mathf.InverseLerp(.52f, .76f, t);
            for (var index = 0; index < 5; index++)
            {
                var visible = index < rarity ? reveal : 0f;
                var angle = 90f + index * (360f / Mathf.Max(1, rarity));
                SetRect("RarityBurst_" + index, Direction(angle) * Mathf.Lerp(28f, 72f, reveal), new Vector2(8f, 25f + index * 2f), -angle, Vector3.one);
                Show("RarityBurst_" + index, RarityColor(rarity), visible * (1f - Mathf.Max(0f, (t - .84f) * 4f)));
            }
        }

        private void EvaluateCardMerge(float t)
        {
            var collide = Mathf.Clamp01(t / .54f);
            var externalResult = mergeResult == null ? Vector2.zero : ToLocalPoint(mergeResult);
            for (var index = 0; index < 3; index++)
            {
                var fallback = new Vector2((index - 1) * 82f, index == 2 ? 35f : -18f);
                var start = index < mergeSources.Length ? ToLocalPoint(mergeSources[index]) : fallback;
                var shown = index < mergeSourceCount;
                SetRect("MergeSource_" + index, Vector2.Lerp(start, externalResult, EaseIn(collide)), new Vector2(46f, 64f), (index - 1) * 8f * (1f - collide), Vector3.one * Mathf.Lerp(1f, .35f, collide));
                Show("MergeSource_" + index, index % 2 == 0 ? primary : secondary, shown ? 1f - Mathf.InverseLerp(.46f, .58f, t) : 0f);
            }
            var column = Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.48f, 1f, t));
            SetRect("ResultColumn", externalResult + Vector2.up * 28f, new Vector2(24f, 112f), 0f, new Vector3(1f, Mathf.Max(.02f, column), 1f));
            Show("ResultColumn", RarityColor(rarity), column);
            SetRect("ResultCard", externalResult, new Vector2(58f, 78f), 0f, Vector3.one * Mathf.Lerp(.15f, 1f, EaseOut(Mathf.InverseLerp(.52f, .78f, t))));
            Show("ResultCard", RarityColor(rarity), Mathf.InverseLerp(.5f, .7f, t));
        }

        private void EvaluateChest(float t)
        {
            Show("ChestBase", primary, .95f);
            var lidLift = EaseOut(Mathf.InverseLerp(.34f, .62f, t));
            SetRect("ChestLid", new Vector2(0f, 25f + lidLift * 25f), new Vector2(96f, 34f), -lidLift * 22f, Vector3.one);
            Show("ChestLid", primary, .95f);
            var leak = t < .4f ? .35f + .4f * Mathf.Sin(t * 30f) : 1f - Mathf.InverseLerp(.4f, .64f, t);
            SetRect("ChestLeak", new Vector2(0f, 20f), new Vector2(Mathf.Lerp(18f, 108f, Mathf.Clamp01(t / .42f)), 8f), 0f, Vector3.one);
            Show("ChestLeak", accent, Mathf.Clamp01(leak));
            var burst = Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.38f, .8f, t));
            SetRect("ChestBurst", Vector2.up * 30f, Vector2.one * Mathf.Lerp(25f, 164f, EaseOut(Mathf.InverseLerp(.38f, .8f, t))), 0f, Vector3.one);
            Show("ChestBurst", secondary, Mathf.Max(0f, burst));
            for (var index = 0; index < 5; index++)
            {
                var phase = Mathf.Clamp01((t - .5f - index * .035f) / .42f);
                SetRect("Tease_" + index, new Vector2((index - 2) * 23f, 28f + EaseOut(phase) * (42f + index * 5f)), Vector2.one * 10f, phase * 100f, Vector3.one);
                Show("Tease_" + index, accent, Mathf.Sin(Mathf.PI * phase));
            }
        }

        private void EvaluateGachaSingle(float t)
        {
            var buildup = skipped ? 1f : Mathf.Clamp01(t / .7f);
            var fall = Mathf.Clamp01(buildup / .54f);
            var bounce = Mathf.Abs(Mathf.Sin(fall * Mathf.PI * 2.5f)) * (1f - fall) * 18f;
            SetRect("GachaOrb", new Vector2(0f, Mathf.Lerp(76f, -18f, EaseIn(fall)) + bounce), Vector2.one * Mathf.Lerp(26f, 54f, buildup), buildup * 55f, Vector3.one);
            Show("GachaOrb", RarityColor(rarity), 1f - Mathf.InverseLerp(.7f, .78f, t));
            var cracks = Mathf.InverseLerp(.35f, .7f, buildup);
            for (var index = 0; index < 6; index++)
            {
                var angle = index * 60f + 15f;
                SetRect("Crack_" + index, Direction(angle) * 16f, new Vector2(3f, Mathf.Lerp(2f, 27f, cracks)), -angle, Vector3.one);
                Show("Crack_" + index, accent, cracks * (1f - Mathf.InverseLerp(.72f, .82f, t)));
            }
            var reveal = skipped ? 1f : Mathf.InverseLerp(.69f, .83f, t);
            ShowRarityBurst(reveal, t);
            var fullscreen = rarity == 5 ? Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.72f, .96f, t)) : 0f;
            SetRect("FullscreenGrace", Vector2.zero, new Vector2(296f, 164f), 0f, Vector3.one);
            Show("FullscreenGrace", accent, Mathf.Max(0f, fullscreen) * .34f);
        }

        private void EvaluateGachaTen(float t)
        {
            var highest = Array.IndexOf(tenRarities, tenRarities.Max());
            for (var index = 0; index < 10; index++)
            {
                var order = index == highest ? 9 : index < highest ? index : index - 1;
                var revealAt = .08f + order * .065f;
                var phase = skipped ? 1f : Mathf.InverseLerp(revealAt, revealAt + .18f, t);
                var column = index % 5;
                var row = index / 5;
                var target = new Vector2(-104f + column * 52f, 30f - row * 62f);
                var start = new Vector2(target.x + (index % 2 == 0 ? -30f : 30f), 100f + index * 7f);
                var emphasis = index == highest ? 1f + .18f * Mathf.Sin(Mathf.PI * phase) : 1f;
                SetRect("TenCard_" + index, Vector2.Lerp(start, target, EaseOut(phase)), new Vector2(40f, 54f), (1f - phase) * (index % 2 == 0 ? -24f : 24f), Vector3.one * Mathf.Max(.05f, phase) * emphasis);
                Show("TenCard_" + index, RarityColor(tenRarities[index]), phase);
            }
            var pulse = skipped ? 1f : Mathf.InverseLerp(.72f, .9f, t);
            var highestPosition = new Vector2(-104f + (highest % 5) * 52f, 30f - (highest / 5) * 62f);
            SetRect("HighestPulse", highestPosition, Vector2.one * Mathf.Lerp(36f, 82f, pulse), 0f, Vector3.one);
            Show("HighestPulse", RarityColor(tenRarities[highest]), pulse * (1f - Mathf.InverseLerp(.9f, 1f, t)));
        }

        private void EvaluateReward(float time)
        {
            activeRewardCount = 0;
            var flight = Mathf.Max(.15f, duration - rewardStagger * Mathf.Max(0, itemCount - 1));
            for (var index = 0; index < RewardPoolCapacity; index++)
            {
                var u = Mathf.Clamp01((time - index * rewardStagger) / flight);
                var active = index < itemCount && time >= index * rewardStagger && u < 1f;
                if (active) activeRewardCount++;
                var point = Vector2.Lerp(rewardStart, rewardEnd, EaseInOut(u));
                point.y += 4f * u * (1f - u) * rewardArcHeight;
                SetRect("RewardItem_" + index, point, Vector2.one * (14f + (index % 3) * 2f), u * 280f + index * 13f, Vector3.one);
                Show("RewardItem_" + index, index % 2 == 0 ? primary : secondary, active ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f)) : 0f);
            }
            peakRewardCount = Mathf.Max(peakRewardCount, activeRewardCount);
            var arrival = Mathf.InverseLerp(flight, flight + .18f, time - rewardStagger * Mathf.Max(0, itemCount - 1));
            SetRect("EndpointPulse", rewardEnd, Vector2.one * Mathf.Lerp(24f, 72f, arrival), 0f, Vector3.one);
            Show("EndpointPulse", accent, arrival * (1f - arrival));
        }

        private void EvaluateStamp(float t)
        {
            var drop = Mathf.Clamp01(t / .4f);
            var squash = 1f - .18f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.32f, .55f, t));
            SetRect("StampBody", new Vector2(0f, Mathf.Lerp(90f, -5f, EaseIn(drop))), new Vector2(84f, 66f), -8f * (1f - drop), new Vector3(1f / Mathf.Max(.5f, squash), squash, 1f));
            Show("StampBody", primary, 1f - Mathf.InverseLerp(.8f, 1f, t));
            var impact = Mathf.InverseLerp(.35f, .75f, t);
            SetRect("InkRing", new Vector2(0f, -8f), Vector2.one * Mathf.Lerp(30f, 142f, impact), 0f, Vector3.one);
            Show("InkRing", secondary, Mathf.Sin(Mathf.PI * impact));
            var draw = Mathf.InverseLerp(.48f, .88f, t);
            SetRect("CheckStroke", new Vector2(8f, -4f), new Vector2(86f, 12f), -32f, new Vector3(Mathf.Max(.02f, draw), 1f, 1f));
            Show("CheckStroke", accent, draw);
        }

        private void EvaluateProgress(float t)
        {
            Show("ProgressTrack", primary, .28f);
            var width = 244f * fillRatio;
            SetRect("ProgressFill", new Vector2(-122f + width * .5f, 0f), new Vector2(Mathf.Max(.1f, width), 26f), 0f, Vector3.one);
            Show("ProgressFill", secondary, fillRatio > .001f ? .92f : 0f);
            var glintX = -122f + width * Mathf.Repeat(t * (1.2f + fillRatio * 2.8f), 1f);
            SetRect("ProgressGlint", new Vector2(glintX, 0f), new Vector2(12f, 28f), 0f, Vector3.one);
            Show("ProgressGlint", accent, fillRatio > .04f ? .9f : 0f);
            var full = fillRatio >= .999f ? .5f + .5f * Mathf.Sin(t * Mathf.PI * 2f) : 0f;
            SetRect("FullPulse", new Vector2(122f, 0f), Vector2.one * Mathf.Lerp(20f, 48f, full), t * 120f, Vector3.one);
            Show("FullPulse", accent, full);
        }

        private void ShowRarityBurst(float reveal, float t)
        {
            for (var index = 0; index < 12; index++)
            {
                var required = rarity <= 2 ? 4 : rarity == 3 ? 8 : 12;
                var angle = index * 30f + (rarity >= 4 ? t * 90f : 0f);
                var radius = Mathf.Lerp(24f, 94f + rarity * 3f, EaseOut(reveal));
                SetRect("RarityBurst_" + index, Direction(angle) * radius, new Vector2(6f, 20f + rarity * 5f), -angle, Vector3.one);
                Show("RarityBurst_" + index, RarityColor(rarity), index < required ? reveal * (1f - Mathf.InverseLerp(.9f, 1f, t)) : 0f);
            }
            SetRect("RevealFlash", Vector2.zero, Vector2.one * Mathf.Lerp(30f, 156f, reveal), 45f, Vector3.one);
            Show("RevealFlash", accent, reveal * (1f - reveal * .45f));
        }

        private void ApplyAnchorFollow()
        {
            if (!followAnchor || anchorRect == null || canvasRoot == null) return;
            canvasRoot.position = anchorRect.TransformPoint(anchorRect.rect.center);
            canvasRoot.rotation = anchorRect.rotation;
            var reference = Mathf.Max(1f, Mathf.Min(Mathf.Abs(anchorRect.rect.width), Mathf.Abs(anchorRect.rect.height)));
            var scale = Mathf.Clamp(reference / 100f, .45f, 2.5f);
            canvasRoot.localScale = Vector3.Scale(anchorBaseScale, Vector3.one * scale);
        }

        private void RestoreAnchorReturnState()
        {
            if (!hasAnchorReturnState || canvasRoot == null) return;
            canvasRoot.SetPositionAndRotation(anchorReturnPosition, anchorReturnRotation);
            canvasRoot.localScale = anchorReturnScale;
            hasAnchorReturnState = false;
        }

        private void CaptureBaseState()
        {
            carriers = carriers ?? new RectTransform[0];
            graphics = graphics ?? new Graphic[0];
            basePositions = new Vector2[carriers.Length];
            baseSizes = new Vector2[carriers.Length];
            baseScales = new Vector3[carriers.Length];
            baseRotations = new Quaternion[carriers.Length];
            for (var index = 0; index < carriers.Length; index++)
            {
                var carrier = carriers[index];
                if (carrier == null) continue;
                basePositions[index] = carrier.anchoredPosition;
                baseSizes[index] = carrier.sizeDelta;
                baseScales[index] = carrier.localScale;
                baseRotations[index] = carrier.localRotation;
            }
            baseColors = new Color[graphics.Length];
            for (var index = 0; index < graphics.Length; index++) baseColors[index] = graphics[index] == null ? Color.white : graphics[index].color;
        }

        private void RestoreBaseState()
        {
            for (var index = 0; index < carriers.Length && index < basePositions.Length; index++)
            {
                var carrier = carriers[index];
                if (carrier == null) continue;
                carrier.anchoredPosition = basePositions[index];
                carrier.sizeDelta = baseSizes[index];
                carrier.localScale = baseScales[index];
                carrier.localRotation = baseRotations[index];
            }
            for (var index = 0; index < graphics.Length && index < baseColors.Length; index++) if (graphics[index] != null) graphics[index].color = baseColors[index];
        }

        private void HideAllGraphics()
        {
            if (graphics == null) return;
            foreach (var graphic in graphics) if (graphic != null) graphic.enabled = false;
        }

        private void Show(string role, Color color, float alpha)
        {
            var carrier = FindCarrier(role);
            if (carrier == null) return;
            var graphic = carrier.GetComponent<Graphic>();
            if (graphic == null) return;
            color.a *= Mathf.Clamp01(alpha);
            graphic.color = color;
            graphic.enabled = color.a > .001f;
        }

        private void SetRect(string role, Vector2 position, Vector2 size, float rotation, Vector3 scale)
        {
            var rect = FindCarrier(role);
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(Mathf.Max(.1f, size.x), Mathf.Max(.1f, size.y));
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = scale;
        }

        private void ClampAllCarriersToClip()
        {
            if (hardClip == null) return;
            var clip = hardClip.rectTransform.rect;
            foreach (var carrier in carriers)
            {
                if (carrier == null) continue;
                var half = Vector2.Scale(carrier.rect.size * .5f, new Vector2(Mathf.Abs(carrier.localScale.x), Mathf.Abs(carrier.localScale.y)));
                var position = carrier.anchoredPosition;
                position.x = Mathf.Clamp(position.x, clip.xMin + half.x, clip.xMax - half.x);
                position.y = Mathf.Clamp(position.y, clip.yMin + half.y, clip.yMax - half.y);
                carrier.anchoredPosition = position;
            }
        }

        private bool VisibleRole(string role)
        {
            var carrier = FindCarrier(role);
            var graphic = carrier == null ? null : carrier.GetComponent<Graphic>();
            return graphic != null && graphic.enabled && graphic.color.a > .001f;
        }

        private bool VisibleRolePrefix(string prefix)
        {
            return carriers != null && carriers.Any(value => value != null && value.name.StartsWith(prefix, StringComparison.Ordinal) && VisibleRole(value.name));
        }

        private Vector2 ToLocalPoint(RectTransform external)
        {
            if (external == null || effectRoot == null) return Vector2.zero;
            return effectRoot.InverseTransformPoint(external.TransformPoint(external.rect.center));
        }

        private static Vector2 RoundedRectPoint(float t, Vector2 size, float radius)
        {
            // A deterministic rounded perimeter approximation with enough segments to visibly travel the full edge.
            var half = size * .5f;
            var perimeter = 2f * (size.x + size.y - 4f * radius) + 2f * Mathf.PI * radius;
            var distance = Mathf.Repeat(t, 1f) * perimeter;
            var straightX = size.x - radius * 2f;
            var straightY = size.y - radius * 2f;
            var arc = Mathf.PI * .5f * radius;
            if (distance < straightX) return new Vector2(-half.x + radius + distance, half.y);
            distance -= straightX;
            if (distance < arc) { var a = Mathf.PI * .5f - distance / radius; return new Vector2(half.x - radius + Mathf.Cos(a) * radius, half.y - radius + Mathf.Sin(a) * radius); }
            distance -= arc;
            if (distance < straightY) return new Vector2(half.x, half.y - radius - distance);
            distance -= straightY;
            if (distance < arc) { var a = -distance / radius; return new Vector2(half.x - radius + Mathf.Cos(a) * radius, -half.y + radius + Mathf.Sin(a) * radius); }
            distance -= arc;
            if (distance < straightX) return new Vector2(half.x - radius - distance, -half.y);
            distance -= straightX;
            if (distance < arc) { var a = -Mathf.PI * .5f - distance / radius; return new Vector2(-half.x + radius + Mathf.Cos(a) * radius, -half.y + radius + Mathf.Sin(a) * radius); }
            distance -= arc;
            if (distance < straightY) return new Vector2(-half.x, -half.y + radius + distance);
            distance -= straightY;
            var angle = Mathf.PI - distance / radius;
            return new Vector2(-half.x + radius + Mathf.Cos(angle) * radius, half.y - radius + Mathf.Sin(angle) * radius);
        }

        private static float RoundedRectAngle(float t, Vector2 size, float radius)
        {
            const float sample = .002f;
            var a = RoundedRectPoint(t, size, radius);
            var b = RoundedRectPoint(t + sample, size, radius);
            var delta = b - a;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        private static Color RarityColor(int value)
        {
            switch (Mathf.Clamp(value, 1, 5))
            {
                case 1: return new Color(.52f, .72f, 1f, 1f);
                case 2: return new Color(.42f, .92f, .8f, 1f);
                case 3: return new Color(1f, .78f, .22f, 1f);
                case 4: return new Color(.72f, .34f, 1f, 1f);
                default: return new Color(1f, .9f, .42f, 1f);
            }
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float EaseOut(float value) { value = Mathf.Clamp01(value); return 1f - (1f - value) * (1f - value); }
        private static float EaseIn(float value) { value = Mathf.Clamp01(value); return value * value; }
        private static float EaseInOut(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        private static bool Finite(Vector2 value) { return Finite(value.x) && Finite(value.y); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
