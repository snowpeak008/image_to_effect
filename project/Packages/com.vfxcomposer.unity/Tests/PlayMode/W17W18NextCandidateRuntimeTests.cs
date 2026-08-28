using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VFXComposer.W17W18NextCandidate;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W17W18NextCandidateRuntimeTests
    {
        private static readonly string[] W17Ids =
        {
            "button_press_fx_ui_next_candidate",
            "button_confirm_burst_ui_next_candidate",
            "card_flip_reveal_ui_next_candidate",
            "card_merge_fx_ui_next_candidate",
            "chest_open_burst_ui_next_candidate",
            "gacha_single_reveal_ui_next_candidate",
            "gacha_ten_sequence_ui_next_candidate",
            "reward_fly_collect_ui_next_candidate",
            "daily_check_stamp_ui_next_candidate",
            "progress_charge_fx_ui_next_candidate"
        };

        private static readonly string[] W18Ids =
        {
            "flame_blade_samurai_kit_next_candidate",
            "ice_moon_mage_kit_next_candidate",
            "mechanical_hunter_kit_next_candidate",
            "ghost_curse_shrine_kit_next_candidate"
        };

        [UnityTest]
        public IEnumerator W17_ButtonPerimeterSupportsThreeRectsAndAnchorActuallyFollowsExternalRect()
        {
            var entries = new[]
            {
                CreateW17("button_press_fx_ui_next_candidate"),
                CreateW17("button_press_fx_ui_next_candidate"),
                CreateW17("button_press_fx_ui_next_candidate")
            };
            var sizes = new[] { new Vector2(92f, 44f), new Vector2(140f, 70f), new Vector2(220f, 92f) };
            var paths = new HashSet<string>();
            for (var index = 0; index < entries.Length; index++)
            {
                Assert.That(entries[index].SetButtonRectSize(sizes[index]), Is.True);
                entries[index].Play();
                entries[index].EvaluateAt(.09f);
                var first = entries[index].FindCarrier("EdgeSweep").anchoredPosition;
                entries[index].EvaluateAt(.27f);
                var second = entries[index].FindCarrier("EdgeSweep").anchoredPosition;
                Assert.That(Vector2.Distance(first, second), Is.GreaterThan(20f));
                Assert.That(entries[index].FindCarrier("ButtonSurface").sizeDelta, Is.EqualTo(sizes[index]));
                paths.Add(Rounded(first) + "->" + Rounded(second));
            }
            Assert.That(paths.Count, Is.EqualTo(3), "Three button sizes drive three real rounded-perimeter paths.");

            var anchorObject = new GameObject("ExternalGameplayButton", typeof(RectTransform));
            var anchor = anchorObject.GetComponent<RectTransform>();
            anchor.sizeDelta = new Vector2(180f, 68f);
            anchor.position = new Vector3(3f, 2f, 0f);
            entries[1].Play();
            Assert.That(entries[1].SetAnchorRect(anchor, true), Is.True);
            Assert.That(entries[1].transform.position, Is.EqualTo(anchor.TransformPoint(anchor.rect.center)));
            anchor.position = new Vector3(-2f, 1.25f, 0f);
            yield return null;
            Assert.That(Vector3.Distance(entries[1].transform.position, anchor.TransformPoint(anchor.rect.center)), Is.LessThan(.001f));
            entries[1].ResetForPool();
            Assert.That(Vector3.Distance(entries[1].transform.position, Vector3.zero), Is.LessThan(.001f), "Pool reset releases the external UI anchor and restores the pre-bind placement.");
            foreach (var entry in entries) Object.Destroy(entry.gameObject);
            Object.Destroy(anchorObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator W17_GachaRaritySkipAndTenSequenceMutateRealBurstAndCardGeometry()
        {
            var single = CreateW17("gacha_single_reveal_ui_next_candidate");
            single.SetRarity(1);
            single.Play();
            single.EvaluateAt(1.43f);
            var lowBurst = VisiblePrefix(single, "RarityBurst_");
            Assert.That(lowBurst, Is.EqualTo(4));
            single.SetRarity(5);
            single.EvaluateAt(1.43f);
            var highBurst = VisiblePrefix(single, "RarityBurst_");
            Assert.That(highBurst, Is.EqualTo(12));
            Assert.That(highBurst, Is.GreaterThan(lowBurst));
            single.ResetForPool();
            single.Play();
            single.EvaluateAt(.08f);
            Assert.That(single.IsRevealVisible, Is.False);
            Assert.That(single.SkipToReveal(), Is.True);
            Assert.That(single.WasSkipped, Is.True);
            Assert.That(single.IsRevealVisible, Is.True);
            Assert.That(single.RevealGeneration, Is.EqualTo(1));

            var ten = CreateW17("gacha_ten_sequence_ui_next_candidate");
            Assert.That(ten.SetTenRarities(new[] { 1, 2, 3, 1, 2, 4, 2, 1, 3, 5 }), Is.True);
            ten.Play();
            ten.EvaluateAt(2.34f);
            var cards = Enumerable.Range(0, 10).Select(index => ten.FindCarrier("TenCard_" + index)).ToArray();
            Assert.That(cards.All(value => value.GetComponent<Graphic>().enabled), Is.True);
            Assert.That(cards.Select(value => Mathf.RoundToInt(value.anchoredPosition.x)).Distinct().Count(), Is.EqualTo(5));
            Assert.That(cards.Select(value => Mathf.RoundToInt(value.anchoredPosition.y)).Distinct().Count(), Is.EqualTo(2));
            Assert.That(ten.FindCarrier("HighestPulse").GetComponent<Graphic>().enabled, Is.True);
            Object.Destroy(single.gameObject);
            Object.Destroy(ten.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator W17_RewardUsesFixedTwelvePoolBezierAndProgressUsesRealFillWidthWithoutLeaks()
        {
            var reward = CreateW17("reward_fly_collect_ui_next_candidate");
            Assert.That(reward.ReadBudget().PooledRewards, Is.EqualTo(12));
            Assert.That(reward.SetRewardRoute(new Vector2(-120f, -52f), new Vector2(118f, 46f), 12, 76f, .05f), Is.True);
            reward.Play();
            reward.EvaluateAt(.66f);
            Assert.That(reward.ActiveRewardCount, Is.EqualTo(12));
            Assert.That(reward.PeakRewardCount, Is.EqualTo(12));
            var rewardRects = Enumerable.Range(0, 12).Select(index => reward.FindCarrier("RewardItem_" + index)).ToArray();
            Assert.That(rewardRects.Select(value => Rounded(value.anchoredPosition)).Distinct().Count(), Is.EqualTo(12));
            Assert.That(rewardRects.Max(value => value.anchoredPosition.y), Is.GreaterThan(46f), "At least one pooled item occupies the authored Bezier arch above the endpoint.");
            reward.Stop(VfxStopMode.Immediate);
            Assert.That(reward.ActiveRewardCount, Is.Zero);
            Assert.That(reward.VisibleGraphicCount, Is.Zero);

            var progress = CreateW17("progress_charge_fx_ui_next_candidate");
            progress.Play();
            progress.SetFillRatio(.25f);
            progress.EvaluateAt(.3f);
            var quarterWidth = progress.FindCarrier("ProgressFill").sizeDelta.x;
            progress.SetFillRatio(.9f);
            progress.EvaluateAt(.3f);
            var highWidth = progress.FindCarrier("ProgressFill").sizeDelta.x;
            Assert.That(quarterWidth, Is.EqualTo(61f).Within(.01f));
            Assert.That(highWidth, Is.EqualTo(219.6f).Within(.02f));
            Assert.That(highWidth, Is.GreaterThan(quarterWidth * 3f));
            progress.SetFillRatio(1f);
            progress.EvaluateAt(.4f);
            Assert.That(progress.FindCarrier("FullPulse").GetComponent<Graphic>().enabled, Is.True);
            Object.Destroy(reward.gameObject);
            Object.Destroy(progress.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator W17_ConfirmFlipMergeChestAndStampOwnDifferentObservableGeometry()
        {
            var confirm = CreateW17("button_confirm_burst_ui_next_candidate");
            confirm.Play();
            confirm.EvaluateAt(.31f);
            Assert.That(VisiblePrefix(confirm, "Ray_"), Is.EqualTo(8));
            Assert.That(confirm.FindCarrier("EdgeSweep").GetComponent<Graphic>().enabled, Is.True);

            var flip = CreateW17("card_flip_reveal_ui_next_candidate");
            flip.SetRarity(4);
            flip.Play();
            flip.EvaluateAt(.7f);
            Assert.That(VisiblePrefix(flip, "RarityBurst_"), Is.EqualTo(4));
            Assert.That(flip.FindCarrier("CardBody").localScale.x, Is.InRange(.02f, .65f), "The card owns a real x-axis flip, not a rarity label.");

            var merge = CreateW17("card_merge_fx_ui_next_candidate");
            var sourceObjects = new[] { new GameObject("SourceA", typeof(RectTransform)), new GameObject("SourceB", typeof(RectTransform)), new GameObject("SourceC", typeof(RectTransform)) };
            var resultObject = new GameObject("ResultCard", typeof(RectTransform));
            sourceObjects[0].GetComponent<RectTransform>().position = new Vector3(-1.2f, -.3f, 0f);
            sourceObjects[1].GetComponent<RectTransform>().position = new Vector3(1.2f, -.3f, 0f);
            sourceObjects[2].GetComponent<RectTransform>().position = new Vector3(0f, 1f, 0f);
            resultObject.GetComponent<RectTransform>().position = Vector3.zero;
            Assert.That(merge.SetMergeAnchors(sourceObjects.Select(value => value.GetComponent<RectTransform>()).ToArray(), resultObject.GetComponent<RectTransform>()), Is.True);
            merge.Play();
            merge.EvaluateAt(.45f);
            var earlyMergePositions = Enumerable.Range(0, 3).Select(index => merge.FindCarrier("MergeSource_" + index).anchoredPosition).ToArray();
            Assert.That(earlyMergePositions.Select(Rounded).Distinct().Count(), Is.EqualTo(3));
            merge.EvaluateAt(1f);
            Assert.That(merge.FindCarrier("ResultCard").GetComponent<Graphic>().enabled, Is.True);
            Assert.That(merge.FindCarrier("ResultColumn").sizeDelta.y, Is.GreaterThan(80f));

            var chest = CreateW17("chest_open_burst_ui_next_candidate");
            chest.Play();
            chest.EvaluateAt(1.1f);
            Assert.That(chest.FindCarrier("ChestLid").anchoredPosition.y, Is.GreaterThan(40f));
            Assert.That(VisiblePrefix(chest, "Tease_"), Is.GreaterThanOrEqualTo(3));

            var stamp = CreateW17("daily_check_stamp_ui_next_candidate");
            stamp.Play();
            stamp.EvaluateAt(.68f);
            Assert.That(stamp.FindCarrier("InkRing").GetComponent<Graphic>().enabled, Is.True);
            Assert.That(stamp.FindCarrier("CheckStroke").localScale.x, Is.GreaterThan(.2f));

            Object.Destroy(confirm.gameObject);
            Object.Destroy(flip.gameObject);
            Object.Destroy(merge.gameObject);
            Object.Destroy(chest.gameObject);
            Object.Destroy(stamp.gameObject);
            foreach (var value in sourceObjects) Object.Destroy(value);
            Object.Destroy(resultObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator W18_FourThemesTraverseFullStagesWithDistinctRealTopologyHardClipAndBoundedBudgets()
        {
            var entries = W18Ids.Select(CreateW18).ToArray();
            Assert.That(entries.Select(value => value.PaletteReference).Distinct().Count(), Is.EqualTo(4));
            Assert.That(entries.Select(value => value.ShapeLanguage).Distinct().Count(), Is.EqualTo(4));
            foreach (var entry in entries)
            {
                entry.ConfigurePreviewClip(new Rect(-1.45f, -1.02f, 2.9f, 2.04f));
                var budget = entry.ReadBudget();
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxRendererBudget), entry.KitId);
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxMaterialBudget), entry.KitId);
                Assert.That(budget.ParticleSystems, Is.LessThanOrEqualTo(W18CharacterThemeController.MaxParticleSystemBudget), entry.KitId);
                Assert.That(entry.UsesHardClipShader(), Is.True, entry.KitId);
                entry.Play();
                entry.EvaluateAt(entry.CycleDuration * .05f);
                Assert.That(entry.CurrentStage, Is.EqualTo(W18KitStage.Idle));
                Assert.That(entry.VisibleRendererCount, Is.GreaterThanOrEqualTo(2), entry.KitId + " idle owns a visible themed state plus body.");
                entry.EvaluateAt(entry.CycleDuration * .22f);
                Assert.That(entry.CurrentStage, Is.EqualTo(W18KitStage.BasicChain));
                Assert.That(entry.BasicChainIndex, Is.InRange(0, 2));
                entry.EvaluateAt(entry.CycleDuration * .64f);
                Assert.That(entry.CurrentStage, Is.EqualTo(W18KitStage.Ultimate));
                Assert.That(entry.GetComponentsInChildren<LineRenderer>(true).Single().positionCount, Is.GreaterThan(0), entry.KitId + " ultimate owns sampled line geometry.");
                entry.EvaluateAt(entry.CycleDuration * .88f);
                Assert.That(entry.CurrentStage, Is.EqualTo(W18KitStage.Death));
                Assert.That(entry.DissolveProgress, Is.InRange(.1f, .8f));
                Assert.That(entry.AllRenderedGeometryHasClipRect(.0001f), Is.True, entry.KitId);
            }
            Assert.That(entries.Single(value => value.Theme == W18CharacterTheme.GhostCurseShrine).FindCarrier("TalismanArray").GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.EqualTo(16));
            foreach (var entry in entries) Object.Destroy(entry.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator W18_RigAttachmentsRestoreParentsAndReplayCleansAllThemeGeometry()
        {
            var flame = CreateW18("flame_blade_samurai_kit_next_candidate");
            var weaponCarrier = flame.FindCarrier("SharpCrescent_0");
            var originalParent = weaponCarrier.parent;
            var rig = new GameObject("ExternalCharacterRig");
            var hand = Child(rig.transform, "Hand");
            var weapon = Child(rig.transform, "Weapon");
            var chest = Child(rig.transform, "Chest");
            var feet = Child(rig.transform, "Feet");
            flame.BindCharacterRig(hand, weapon, chest, feet);
            Assert.That(weaponCarrier.parent, Is.EqualTo(weapon));
            Assert.That(flame.FindCarrier("ParrySpark").parent, Is.EqualTo(hand));
            Assert.That(flame.FindCarrier("DissolveEdge").parent, Is.EqualTo(chest));
            Assert.That(flame.FindCarrier("DashRibbon").parent, Is.EqualTo(feet));
            flame.Play();
            flame.EvaluateAt(flame.CycleDuration * .64f);
            Assert.That(flame.VisibleRendererCount, Is.GreaterThan(1));
            var firstLine = ReadLine(flame.GetComponentInChildren<LineRenderer>(true));
            flame.Stop(VfxStopMode.Immediate);
            Assert.That(weaponCarrier.parent, Is.EqualTo(originalParent));
            Assert.That(flame.VisibleRendererCount, Is.Zero);
            Assert.That(flame.GetComponentInChildren<LineRenderer>(true).positionCount, Is.Zero);
            flame.Play();
            flame.EvaluateAt(flame.CycleDuration * .64f);
            var replayLine = ReadLine(flame.GetComponentInChildren<LineRenderer>(true));
            CollectionAssert.AreEqual(firstLine, replayLine, "The visible ultimate geometry is deterministic across reset/replay.");
            flame.Stop(VfxStopMode.Immediate);

            var ghost = CreateW18("ghost_curse_shrine_kit_next_candidate");
            var ghostWeapon = ghost.FindCarrier("InkMissile");
            var ghostHand = ghost.FindCarrier("GhostHand");
            var ghostChest = ghost.FindCarrier("CurseMark");
            var ghostFeet = ghost.FindCarrier("PhantomDomain");
            var ghostWeaponParent = ghostWeapon.parent;
            var ghostHandParent = ghostHand.parent;
            var ghostChestParent = ghostChest.parent;
            var ghostFeetParent = ghostFeet.parent;
            ghost.BindCharacterRig(hand, weapon, chest, feet);
            Assert.That(ghostWeapon.parent, Is.EqualTo(weapon));
            Assert.That(ghostHand.parent, Is.EqualTo(hand));
            Assert.That(ghostChest.parent, Is.EqualTo(chest));
            Assert.That(ghostFeet.parent, Is.EqualTo(feet));
            ghost.Play();
            ghost.EvaluateAt(ghost.CycleDuration * .64f);
            Assert.That(ghost.VisibleRendererCount, Is.GreaterThan(1));
            ghost.Stop(VfxStopMode.Immediate);
            Assert.That(ghostWeapon.parent, Is.EqualTo(ghostWeaponParent));
            Assert.That(ghostHand.parent, Is.EqualTo(ghostHandParent));
            Assert.That(ghostChest.parent, Is.EqualTo(ghostChestParent));
            Assert.That(ghostFeet.parent, Is.EqualTo(ghostFeetParent));
            Assert.That(ghost.VisibleRendererCount, Is.Zero);
            Object.Destroy(ghost.gameObject);
            Object.Destroy(flame.gameObject);
            Object.Destroy(rig);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllProductionPrefabs_CloseBothStopPathsAndPreviewRunsNaturalCleanGapReplay()
        {
            var uiEntries = W17Ids.Select(CreateW17).ToArray();
            var themeEntries = W18Ids.Select(CreateW18).ToArray();

            foreach (var entry in uiEntries)
            {
                entry.Play();
                entry.EvaluateAt(.35f);
                Assert.That(entry.VisibleGraphicCount, Is.GreaterThan(0), entry.EffectId);
                entry.Stop(VfxStopMode.AllowTail);
                Assert.That(entry.IsAlive, Is.False, entry.EffectId);
                Assert.That(entry.VisibleGraphicCount, Is.Zero, entry.EffectId);
                entry.Play();
                entry.EvaluateAt(.18f);
                entry.Stop(VfxStopMode.Immediate);
                Assert.That(entry.IsAlive, Is.False, entry.EffectId);
                Assert.That(entry.VisibleGraphicCount, Is.Zero, entry.EffectId);
            }

            foreach (var entry in themeEntries)
            {
                entry.Play();
                entry.EvaluateAt(entry.CycleDuration * .64f);
                Assert.That(entry.VisibleRendererCount, Is.GreaterThan(0), entry.KitId);
                entry.Stop(VfxStopMode.AllowTail);
                Assert.That(entry.IsAlive, Is.False, entry.KitId);
                Assert.That(entry.VisibleRendererCount, Is.Zero, entry.KitId);
                entry.Play();
                entry.EvaluateAt(entry.CycleDuration * .28f);
                entry.Stop(VfxStopMode.Immediate);
                Assert.That(entry.IsAlive, Is.False, entry.KitId);
                Assert.That(entry.VisibleRendererCount, Is.Zero, entry.KitId);
            }

            var driverObject = new GameObject("W17W18NaturalCleanGapFixture");
            var driver = driverObject.AddComponent<W17W18NextCandidatePreviewDriver>();
            SetPrivate(driver, "uiEntries", uiEntries);
            SetPrivate(driver, "themeEntries", themeEntries);
            SetPrivate(driver, "playDuration", .2f);
            SetPrivate(driver, "cleanGap", .08f);
            yield return null;
            var firstReplay = driver.ReplayCount;
            Assert.That(firstReplay, Is.GreaterThanOrEqualTo(1));
            var cleanGapDeadline = Time.realtimeSinceStartup + 2f;
            while (!driver.InCleanGap && Time.realtimeSinceStartup < cleanGapDeadline) yield return null;
            Assert.That(driver.InCleanGap, Is.True);
            Assert.That(driver.AllEntriesIdle, Is.True);
            var replayDeadline = Time.realtimeSinceStartup + 2f;
            while (driver.ReplayCount <= firstReplay && Time.realtimeSinceStartup < replayDeadline) yield return null;
            Assert.That(driver.ReplayCount, Is.GreaterThan(firstReplay));
            Assert.That(driver.InCleanGap, Is.False);
            Assert.That(uiEntries.All(value => value.IsAlive), Is.True);
            Assert.That(themeEntries.All(value => value.IsAlive), Is.True);

            driver.EnterCleanGap();
            Object.Destroy(driverObject);
            foreach (var entry in uiEntries) Object.Destroy(entry.gameObject);
            foreach (var entry in themeEntries) Object.Destroy(entry.gameObject);
            yield return null;
        }

        private static W17UiInteractionController CreateW17(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/W17W18NextCandidate/W17/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id + " must be built first.");
            return Object.Instantiate(prefab).GetComponent<W17UiInteractionController>();
        }

        private static W18CharacterThemeController CreateW18(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/W17W18NextCandidate/W18/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id + " must be built first.");
            return Object.Instantiate(prefab).GetComponent<W18CharacterThemeController>();
        }

        private static int VisiblePrefix(W17UiInteractionController entry, string prefix)
        {
            return entry.GetComponentsInChildren<Graphic>(true).Count(value => value.name.StartsWith(prefix) && value.enabled && value.color.a > .001f);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Vector3[] ReadLine(LineRenderer line)
        {
            var values = new Vector3[line.positionCount];
            line.GetPositions(values);
            return values;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static string Rounded(Vector2 value)
        {
            return Mathf.RoundToInt(value.x * 10f) + ":" + Mathf.RoundToInt(value.y * 10f);
        }
    }
}
