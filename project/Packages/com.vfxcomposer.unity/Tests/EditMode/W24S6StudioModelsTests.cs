using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using VFXComposer.Editor.UI;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6StudioModelsTests
    {
        [Test]
        public void Indexing_IsDeterministicAndDefaultsToVisualPending()
        {
            var indexed = VfxStudioLibrary.IndexForTests(new[]
            {
                new VfxStudioLibraryItem { Id = "zeta", RecipePath = "Assets/z.json" },
                new VfxStudioLibraryItem { Id = "alpha", RecipePath = "Assets/a.json" },
                new VfxStudioLibraryItem { Id = "alpha", RecipePath = "Assets/b.json" }
            });

            Assert.That(indexed.Select(item => item.Id), Is.EqualTo(new[] { "alpha", "zeta" }));
            Assert.That(indexed[0].RecipePath, Is.EqualTo("Assets/a.json"));
            Assert.That(indexed.All(item => item.ProductionStatus == "VISUAL_PENDING"), Is.True);
            Assert.That(indexed.All(item => item.CommercialEligible == false), Is.True);
            Assert.That(indexed.All(item => item.Maturity == "UNASSESSED"), Is.True);
        }

        [Test]
        public void Filters_KeepCapabilityCarrierLifecycleAndStatusIndependent()
        {
            var item = new VfxStudioLibraryItem
            {
                Id = "sustained_flame_3d", Name = "Sustained Flame", Capabilities = new[] { "sustained" },
                Carriers = new[] { "particle-system", "light" }, Lifecycle = "sustained",
                Maturity = "L3_ProductionCandidate", ProductionStatus = "VISUAL_PENDING"
            };
            var filter = new VfxStudioLibraryFilter { Capability = "sustained", Carrier = "light", Lifecycle = "sustained", Maturity = "L3_ProductionCandidate", ProductionStatus = "VISUAL_PENDING" };
            Assert.That(filter.Matches(item), Is.True);
            filter.ProductionStatus = "L4";
            Assert.That(filter.Matches(item), Is.False, "Working status must not be inferred from maturity or evidence.");
        }

        [Test]
        public void Indexing_CannotTrustAForgeableL4DisplayValue()
        {
            var indexed = VfxStudioLibrary.IndexForTests(new[]
            {
                new VfxStudioLibraryItem { Id = "forged", RecipePath = "Assets/forged.json", ProductionStatus = "L4", Maturity = "L4_UserSignedProductionReady", CommercialEligible = true, HasMachineEvidence=true, HasVisualQaEvidence=true }
            });
            Assert.That(indexed[0].ProductionStatus, Is.EqualTo("VISUAL_PENDING"));
            Assert.That(indexed[0].CommercialEligible, Is.False);
            Assert.That(indexed[0].Maturity, Is.EqualTo("UNASSESSED"), "Unverified maturity labels must not survive Studio indexing.");
            Assert.That(indexed[0].HasMachineEvidence,Is.False);
            Assert.That(indexed[0].HasVisualQaEvidence,Is.False);
        }

        [Test]
        public void Indexing_CannotPromoteAnUnverifiedL3StatusOrMaturity()
        {
            var indexed = VfxStudioLibrary.IndexForTests(new[]
            {
                new VfxStudioLibraryItem { Id = "claimed_l3", RecipePath = "Assets/claimed.json", ProductionStatus = "L3", Maturity = "L3_ProductionCandidate" }
            });

            Assert.That(indexed[0].ProductionStatus, Is.EqualTo("VISUAL_PENDING"));
            Assert.That(indexed[0].Maturity, Is.EqualTo("UNASSESSED"));
            Assert.That(indexed[0].CommercialEligible, Is.False);
        }

        [Test]
        public void MissingData_IsFilterableAndDoesNotInventEvidence()
        {
            var item = VfxStudioLibrary.IndexForTests(new[] { new VfxStudioLibraryItem { Id = "missing", RecipePath = "Assets/missing.json" } }).Single();
            Assert.That(item.HasContract, Is.False);
            Assert.That(item.HasTrace, Is.False);
            Assert.That(item.HasEvidence, Is.False);
            Assert.That(item.MachineGate, Is.EqualTo("NOT_RECORDED"));
            Assert.That(item.VisualQa, Is.EqualTo("NOT_RECORDED"));
            Assert.That(item.UserVerdict, Is.EqualTo("NOT_RECORDED"));
            Assert.That(new VfxStudioLibraryFilter { Carrier = "particle-system" }.Matches(item), Is.False);
        }

        [Test]
        public void OwnershipManifest_DoesNotCountAsStrictBudgetOrIdempotenceEvidence()
        {
            var item = new VfxStudioLibraryItem { Id="manifest-only", Strict=true, HasEvidence=true, HasStrictBudgetEvidence=false, HasIdempotenceEvidence=false };
            var review = VfxStudioAutomaticReviewChecks.Evaluate(item, true, true, true, true);
            Assert.That(review.Manifest, Is.False, "Ownership Manifest remains pending until the S5 byte verifier is reused.");
            Assert.That(review.StrictBudget, Is.False, "A strict ownership Manifest is not a budget-validation record.");
            Assert.That(review.Idempotence, Is.False, "Manifest existence is not repeated-build/idempotence evidence.");
            Assert.That(review.AutomaticComplete, Is.False);
        }

        [Test]
        public void SelfReportedTelemetryAndPlaybackProbe_RemainPendingWithoutS5ByteVerifier()
        {
            var item=new VfxStudioLibraryItem{Strict=true,HasContract=true,HasTrace=true,HasEvidence=true,HasStrictBudgetEvidence=true,HasIdempotenceEvidence=true};
            VfxStudioAutomaticReviewChecks.RefreshVerificationFlags(item);
            var review=VfxStudioAutomaticReviewChecks.Evaluate(item,true,true,true,true);
            Assert.That(review.StrictBudget,Is.False);
            Assert.That(review.Idempotence,Is.False);
            Assert.That(review.PlaybackReset,Is.False);
        }

        [Test]
        public void ReviewReset_ClearsAutomaticManualAndReviewerIndependentState()
        {
            var review=new VfxStudioReviewState{Schema=true,RuntimeEntry=true,Manifest=true,StrictBudget=true,Idempotence=true,PlaybackReset=true,Evidence=true,Shape=true,Layers=true,Motion=true,Dissipation=true,Depth=true};
            review.Reset();
            Assert.That(review.AutomaticComplete,Is.False);
            Assert.That(review.ManualComplete,Is.False);
        }

        [Test]
        public void StudioManifestProjection_UsesStrictJsonAndTheSegmentedRuntimeEntryValidator()
        {
            var valid="{\"effectId\":\"fire\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/fire/a/VFX_fire.prefab\"}}";
            Assert.DoesNotThrow(()=>VfxStudioLibrary.ParseExactManifest(valid,"fire"));
            Assert.Throws<JsonSerializationException>(()=>VfxStudioLibrary.ParseExactManifest("{\"effectId\":\"fire\",\"effectId\":\"fire\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/fire/a.prefab\"}}","fire"));
            Assert.Throws<JsonSerializationException>(()=>VfxStudioLibrary.ParseExactManifest("{\"effectId\":\"fire\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/fire//a.prefab\"}}","fire"));
            Assert.Throws<JsonSerializationException>(()=>VfxStudioLibrary.ParseExactManifest(valid,"other"));
        }
    }
}
