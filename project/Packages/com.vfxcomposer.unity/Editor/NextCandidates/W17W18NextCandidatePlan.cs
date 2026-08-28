using System;
using UnityEngine;
using VFXComposer.W17W18NextCandidate;

namespace VFXComposer.Editor.NextCandidates
{
    public sealed class W17NextCandidatePlan
    {
        public string Id;
        public W17UiEffectKind Kind;
        public float Duration;
        public string Primary;
        public string Secondary;
        public string Accent;
        public int Rarity;
        public int ItemCount;

        public W17NextCandidatePlan(string id, W17UiEffectKind kind, float duration, string primary, string secondary, string accent, int rarity, int itemCount)
        {
            Id = id; Kind = kind; Duration = duration; Primary = primary; Secondary = secondary; Accent = accent; Rarity = rarity; ItemCount = itemCount;
        }
    }

    public enum W18CarrierMesh { Body, Crescent, Diamond, Hexagon, Ring, Gear, Ribbon, Star, TalismanArray, GhostProcession }
    public enum W18CarrierSlot { Root, Hand, Weapon, Chest, Feet }

    public sealed class W18CarrierPlan
    {
        public string Role;
        public W18CarrierMesh Mesh;
        public Vector3 Position;
        public Vector3 Scale;
        public W18CarrierSlot Slot;

        public W18CarrierPlan(string role, W18CarrierMesh mesh, Vector3 position, Vector3 scale, W18CarrierSlot slot = W18CarrierSlot.Root)
        {
            Role = role; Mesh = mesh; Position = position; Scale = scale; Slot = slot;
        }
    }

    public sealed class W18NextCandidatePlan
    {
        public string Id;
        public W18CharacterTheme Theme;
        public string PaletteReference;
        public string ShapeLanguage;
        public string Primary;
        public string Secondary;
        public string Accent;
        public W18CarrierPlan[] Carriers;

        public W18NextCandidatePlan(string id, W18CharacterTheme theme, string paletteReference, string shapeLanguage, string primary, string secondary, string accent, W18CarrierPlan[] carriers)
        {
            Id = id; Theme = theme; PaletteReference = paletteReference; ShapeLanguage = shapeLanguage; Primary = primary; Secondary = secondary; Accent = accent; Carriers = carriers;
        }
    }

    public static class W17W18NextCandidateCatalog
    {
        public static readonly W17NextCandidatePlan[] W17 =
        {
            new W17NextCandidatePlan("button_press_fx_ui_next_candidate", W17UiEffectKind.ButtonPress, .42f, "#19AFC4", "#76F1FF", "#FFFFFF", 1, 1),
            new W17NextCandidatePlan("button_confirm_burst_ui_next_candidate", W17UiEffectKind.ButtonConfirm, .72f, "#F19B17", "#FFE16A", "#FFFFFF", 3, 1),
            new W17NextCandidatePlan("card_flip_reveal_ui_next_candidate", W17UiEffectKind.CardFlip, 1.15f, "#4B52A9", "#A879FF", "#FFF4B0", 4, 1),
            new W17NextCandidatePlan("card_merge_fx_ui_next_candidate", W17UiEffectKind.CardMerge, 1.35f, "#237DA8", "#B25CFF", "#FFFFFF", 4, 1),
            new W17NextCandidatePlan("chest_open_burst_ui_next_candidate", W17UiEffectKind.ChestOpen, 1.45f, "#8D4A15", "#FFB52F", "#FFF2A2", 4, 5),
            new W17NextCandidatePlan("gacha_single_reveal_ui_next_candidate", W17UiEffectKind.GachaSingle, 1.8f, "#5135A3", "#A953FF", "#FFF5C8", 5, 1),
            new W17NextCandidatePlan("gacha_ten_sequence_ui_next_candidate", W17UiEffectKind.GachaTen, 2.6f, "#244877", "#9E63FF", "#FFE984", 5, 10),
            new W17NextCandidatePlan("reward_fly_collect_ui_next_candidate", W17UiEffectKind.RewardFly, 1.55f, "#F29B19", "#FFE05C", "#FFFFFF", 3, 12),
            new W17NextCandidatePlan("daily_check_stamp_ui_next_candidate", W17UiEffectKind.DailyStamp, 1.05f, "#B63131", "#EA6B3D", "#FFF0C2", 2, 1),
            new W17NextCandidatePlan("progress_charge_fx_ui_next_candidate", W17UiEffectKind.ProgressCharge, 1.6f, "#133C55", "#22C4D6", "#F2FFFF", 3, 1)
        };

        public static readonly W18NextCandidatePlan[] W18 =
        {
            new W18NextCandidatePlan(
                "flame_blade_samurai_kit_next_candidate", W18CharacterTheme.FlameBladeSamurai,
                "theme.flame_blade_samurai.next", "sharp crescents + rising blade diagonals", "#8C160D", "#F04A13", "#FFD15A",
                new[]
                {
                    C("SharpCrescent_0", W18CarrierMesh.Crescent, -.48f, .26f, .8f, .35f, W18CarrierSlot.Weapon),
                    C("SharpCrescent_1", W18CarrierMesh.Crescent, 0f, .35f, .95f, .4f, W18CarrierSlot.Weapon),
                    C("SharpCrescent_2", W18CarrierMesh.Crescent, .48f, .26f, 1.08f, .45f, W18CarrierSlot.Weapon),
                    C("DashRibbon", W18CarrierMesh.Ribbon, 0f, -.28f, 1.45f, .28f, W18CarrierSlot.Feet),
                    C("FlameSlash", W18CarrierMesh.Crescent, .22f, .1f, 1.28f, .54f, W18CarrierSlot.Weapon),
                    C("BladeTempest", W18CarrierMesh.Ring, 0f, .02f, 1.24f, 1.24f),
                    C("ParrySpark", W18CarrierMesh.Star, .48f, .25f, .72f, .72f, W18CarrierSlot.Hand),
                    C("DissolveEdge", W18CarrierMesh.Ring, 0f, .08f, .72f, 1.25f, W18CarrierSlot.Chest),
                    C("EntranceFlare", W18CarrierMesh.Star, 0f, .08f, 1.25f, 1.25f),
                    C("SheathEmber", W18CarrierMesh.Crescent, -.45f, -.2f, .62f, .28f, W18CarrierSlot.Weapon)
                }),
            new W18NextCandidatePlan(
                "ice_moon_mage_kit_next_candidate", W18CharacterTheme.IceMoonMage,
                "theme.ice_moon_mage.next", "hexagons + moon circles", "#1D4C88", "#58C8F4", "#E7FCFF",
                new[]
                {
                    C("HexShard_0", W18CarrierMesh.Hexagon, -.5f, .32f, .38f, .72f, W18CarrierSlot.Hand),
                    C("HexShard_1", W18CarrierMesh.Hexagon, 0f, .48f, .42f, .8f, W18CarrierSlot.Hand),
                    C("HexShard_2", W18CarrierMesh.Hexagon, .5f, .32f, .38f, .72f, W18CarrierSlot.Hand),
                    C("MoonWheel", W18CarrierMesh.Ring, .36f, .15f, .72f, .72f, W18CarrierSlot.Hand),
                    C("FrostNova", W18CarrierMesh.Star, 0f, -.24f, 1.35f, .62f, W18CarrierSlot.Feet),
                    C("CrystalShield", W18CarrierMesh.Hexagon, 0f, .12f, 1.08f, 1.25f, W18CarrierSlot.Chest),
                    C("FrozenDomain", W18CarrierMesh.Ring, 0f, -.15f, 1.48f, .9f),
                    C("StaffCharge", W18CarrierMesh.Hexagon, .42f, .25f, .48f, .48f, W18CarrierSlot.Weapon),
                    C("DissolveEdge", W18CarrierMesh.Ring, 0f, .08f, .72f, 1.25f, W18CarrierSlot.Chest),
                    C("EntranceFlare", W18CarrierMesh.Star, 0f, .08f, 1.2f, 1.2f)
                }),
            new W18NextCandidatePlan(
                "mechanical_hunter_kit_next_candidate", W18CharacterTheme.MechanicalHunter,
                "theme.mechanical_hunter.next", "gears + straight targeting lines", "#5C3B21", "#E28B2C", "#5CE7FF",
                new[]
                {
                    C("Muzzle_0", W18CarrierMesh.Star, .52f, .32f, .32f, .32f, W18CarrierSlot.Weapon),
                    C("Muzzle_1", W18CarrierMesh.Star, .58f, .22f, .38f, .38f, W18CarrierSlot.Weapon),
                    C("Muzzle_2", W18CarrierMesh.Star, .64f, .12f, .45f, .45f, W18CarrierSlot.Weapon),
                    C("SteamDash", W18CarrierMesh.Ribbon, -.15f, -.28f, 1.4f, .35f, W18CarrierSlot.Feet),
                    C("HoloScan", W18CarrierMesh.Crescent, .25f, .18f, 1.35f, .64f, W18CarrierSlot.Hand),
                    C("EmpNova", W18CarrierMesh.Gear, 0f, .04f, 1.2f, 1.2f),
                    C("ChainGrapple", W18CarrierMesh.Diamond, .46f, .2f, .58f, .58f, W18CarrierSlot.Weapon),
                    C("OverheatVent", W18CarrierMesh.Gear, -.38f, .4f, .42f, .42f, W18CarrierSlot.Chest),
                    C("DissolveEdge", W18CarrierMesh.Ring, 0f, .08f, .72f, 1.25f, W18CarrierSlot.Chest),
                    C("EntranceFlare", W18CarrierMesh.Gear, 0f, .08f, 1.2f, 1.2f)
                }),
            new W18NextCandidatePlan(
                "ghost_curse_shrine_kit_next_candidate", W18CharacterTheme.GhostCurseShrine,
                "theme.ghost_curse_shrine.next", "ink ribbons + eight talismans", "#2A193B", "#73509A", "#E9D7B2",
                new[]
                {
                    C("TalismanArray", W18CarrierMesh.TalismanArray, 0f, .08f, 1f, 1f),
                    C("InkMissile", W18CarrierMesh.Ribbon, .35f, .24f, .92f, .28f, W18CarrierSlot.Weapon),
                    C("GhostHand", W18CarrierMesh.Crescent, -.35f, .08f, .78f, .72f, W18CarrierSlot.Hand),
                    C("PhantomDomain", W18CarrierMesh.Ring, 0f, -.18f, 1.42f, .82f, W18CarrierSlot.Feet),
                    C("CurseMark", W18CarrierMesh.Diamond, 0f, .25f, .7f, .7f, W18CarrierSlot.Chest),
                    C("HundredGhosts", W18CarrierMesh.GhostProcession, 0f, .08f, 1f, 1f),
                    C("DissolveEdge", W18CarrierMesh.Ring, 0f, .08f, .72f, 1.25f, W18CarrierSlot.Chest),
                    C("EntranceFlare", W18CarrierMesh.Star, 0f, .08f, 1.16f, 1.16f)
                })
        };

        private static W18CarrierPlan C(string role, W18CarrierMesh mesh, float x, float y, float sx, float sy, W18CarrierSlot slot = W18CarrierSlot.Root)
        {
            return new W18CarrierPlan(role, mesh, new Vector3(x, y, 0f), new Vector3(sx, sy, 1f), slot);
        }
    }
}
