using UnityEngine;

namespace VFXComposer
{
    /// <summary>
    /// W6-W8 semantic executors.  These routines intentionally use different carrier motion,
    /// topology and timing for fluid, wind, weight, growth, corrosion, sanctity, void and runes.
    /// They share only bounded storage/render plumbing with W3-W5.
    /// </summary>
    public sealed partial class ElementNextCandidateVisualExecutor
    {
        public int GetArcaneActivationOrdinal(int logicalGlyph)
        {
            var count = Mathf.Clamp(Mathf.RoundToInt(GetContentNumber("glyph_count", 10f)), 1, 12);
            logicalGlyph = Mathf.Clamp(logicalGlyph, 0, count - 1);
            var order = GetContentText("activate_order", "forward");
            if (order == "reverse") return count - 1 - logicalGlyph;
            if (order != "seeded_random") return logicalGlyph;
            var rank = 0;
            var key = ActivationKey(logicalGlyph);
            for (var index = 0; index < count; index++)
            {
                if (index == logicalGlyph) continue;
                var other = ActivationKey(index);
                if (other < key || (Mathf.Approximately(other, key) && index < logicalGlyph)) rank++;
            }
            return rank;
        }

        private float ActivationKey(int logicalGlyph)
        {
            return Hash01(seed + (uint)(logicalGlyph * 2654435761u));
        }

        private void EvaluateW6W8(float time, float n)
        {
            ResetW6W8Readback();
            if (family == ElementNextCandidateFamily.Water) EvaluateWater(time, n);
            else if (family == ElementNextCandidateFamily.Wind) EvaluateWind(time, n);
            else if (family == ElementNextCandidateFamily.Earth) EvaluateEarth(time, n);
            else if (family == ElementNextCandidateFamily.Nature) EvaluateNature(time, n);
            else if (family == ElementNextCandidateFamily.Toxic) EvaluateToxic(time, n);
            else if (family == ElementNextCandidateFamily.Holy) EvaluateHoly(time, n);
            else if (family == ElementNextCandidateFamily.Shadow) EvaluateShadow(time, n);
            else EvaluateArcane(time, n);
        }

        private void EvaluateWater(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.WaterJet)
            {
                var length = GetContentNumber("length", 6f); var pressure = GetContentNumber("pressure", 6f); var foam = GetContentNumber("foam_amount", .5f);
                WaterFlow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .16f, n)); WaterFoam = foam * WaterFlow; Phase = n < .16f ? ElementNextCandidatePhase.Flow : ElementNextCandidatePhase.Sustain;
                var pulse = 1f + Mathf.Sin(time * (8f + pressure)) * (.015f + pressure * .003f);
                ShowRole(0, Vector3.right * length * .5f, Quaternion.identity, new Vector3(length * pulse, .18f + pressure * .025f, .22f), WaterFlow, .9f + pressure * .08f, 7f);
                ShowRole(1, Vector3.right * length * .5f, Quaternion.identity, new Vector3(length * .96f, .06f + pressure * .01f, .08f), WaterFlow * .8f, 1.45f, 2f);
                ShowRole(2, Vector3.right * length * .52f, Quaternion.identity, new Vector3(length * 1.04f, .3f + foam * .24f, .3f), WaterFoam * .42f, .52f, 1f);
                ShowRole(3, Vector3.right * length, Quaternion.Euler(68f, 0f, 0f), Vector3.one * (.3f + foam * .35f), WaterFoam * .7f, .65f, 8f);
                ShowRole(4, Vector3.right * length, Quaternion.identity, Vector3.one * (.18f + pressure * .04f), WaterFlow * .82f, 1.2f, 2f);
                AddArc(0, Vector3.zero, Vector3.right * length, 10, .018f + (10f - pressure) * .004f, WaterFlow * .65f, .025f + pressure * .003f, Mathf.FloorToInt(time * (12f + pressure)));
                AddFlowParticles(Mathf.Min(particleBudget, 18 + Mathf.RoundToInt(foam * 48f)), length, time * (1.5f + pressure * .18f), .04f + foam * .025f, WaterFlow, 1101);
            }
            else if (profile == ElementNextCandidateProfile.TidalWave)
            {
                var width = GetContentNumber("wave_width", 4f); var distance = GetContentNumber("travel_distance", 6f); var curl = GetContentNumber("curl_amount", .65f);
                var rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .18f, n)); var fall = 1f - Mathf.SmoothStep(.68f, .92f, n);
                WaterFlow = rise * fall; WaterFoam = curl * WaterFlow; WaterSplash = Mathf.Sin(Mathf.Clamp01((n - .18f) / .58f) * Mathf.PI); WaterResidue = Mathf.SmoothStep(.62f, .82f, n) * (1f - Mathf.SmoothStep(.94f, 1f, n)); Phase = n < .18f ? ElementNextCandidatePhase.Growth : n < .7f ? ElementNextCandidatePhase.Curl : ElementNextCandidatePhase.Residue;
                var travel = Vector3.right * Mathf.Lerp(-distance * .5f, distance * .5f, n);
                ShowRole(0, travel + Vector3.up * (1.05f + curl), Quaternion.Euler(0f, curl * 22f, -8f - curl * 20f), new Vector3(width * .5f, .85f + curl, .5f), WaterFlow, .92f, 7f);
                ShowRole(1, travel + Vector3.up * (1.55f + curl), Quaternion.Euler(0f, 0f, -24f), new Vector3(width * .48f, .16f + curl * .2f, .34f), WaterFoam, 1.25f, 2f);
                ShowRole(2, travel + Vector3.up * .8f, Quaternion.Euler(0f, 0f, 12f), new Vector3(width * .56f, 1.3f + curl, .68f), WaterFlow * .32f, .5f, 1f);
                ShowRole(3, Vector3.right * distance * .15f, Quaternion.Euler(68f, 0f, 0f), new Vector3(distance * .55f, width * .34f, 1f), WaterResidue * .55f, .52f, 8f);
                ShowRole(4, travel + Vector3.up * .25f, Quaternion.identity, new Vector3(width * .52f, .32f + WaterSplash * .7f, .4f), WaterSplash * .72f, 1.05f, 2f);
                AddCurtainParticles(Mathf.Min(particleBudget, 30 + Mathf.RoundToInt(curl * 60f)), travel.x, width, 1.8f + curl, n, .045f, WaterSplash, 1133);
            }
            else if (profile == ElementNextCandidateProfile.BubbleShield)
            {
                var radius = GetContentNumber("bubble_radius", 1.2f); var wobble = GetContentNumber("wobble", .28f); var popScale = GetContentNumber("pop_splash_scale", 1.2f); var eventAge = elapsed - triggeredAt; var triggered=eventAge>=0f; var pop = triggered && eventAge <= .32f;
                WaterFlow = pop ? 1f - eventAge / .32f : triggered?0f:.74f; WaterSplash = pop ? 1f - eventAge / .32f : 0f; WaterFoam = triggered?0f:wobble * .22f; Phase = pop ? ElementNextCandidatePhase.Pop : triggered?ElementNextCandidatePhase.Residue:ElementNextCandidatePhase.Sustain;
                var wobbleScale = new Vector3(1f + Mathf.Sin(time * 5f) * wobble * .08f, 1f + Mathf.Cos(time * 4f) * wobble * .1f, 1f);
                if (!triggered) ShowRole(0, Vector3.zero, Quaternion.Euler(8f, time * 9f, 0f), Vector3.Scale(Vector3.one * radius, wobbleScale), .42f, .72f, 7f);
                if (!triggered) ShowRole(1, new Vector3(-radius * .26f, radius * .3f, -.02f), Quaternion.Euler(0f, 0f, 28f), new Vector3(radius * .28f, radius * .62f, 1f), .72f, 1.2f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.identity, Vector3.one * radius * 1.04f, triggered ? 0f : .18f, .4f, 1f);
                if (pop) ShowRole(4, triggeredLocalPosition, Quaternion.identity, Vector3.one * popScale * Mathf.Lerp(.35f, 1.45f, eventAge / .32f), WaterSplash, 1.4f, 8f);
                AddRadialParticles(pop ? Mathf.Min(particleBudget, 18) : triggered?0:Mathf.Min(particleBudget, 5), pop ? eventAge * popScale * 3f : radius * .86f, pop ? .12f + eventAge : .1f, .05f, pop ? WaterSplash : .4f, 1169);
            }
            else if (profile == ElementNextCandidateProfile.SplashImpact)
            {
                var crown = GetContentNumber("crown_scale", 1.2f); var droplets = Mathf.RoundToInt(GetContentNumber("droplet_count", 10f)); var rings = Mathf.RoundToInt(GetContentNumber("ring_count", 1f)); var burst = Mathf.Sin(Mathf.Clamp01(n / .7f) * Mathf.PI);
                WaterSplash = burst; WaterFoam = burst * .72f; WaterResidue = Mathf.SmoothStep(.42f, .72f, n) * (1f - Mathf.SmoothStep(.92f, 1f, n)); PrimaryCarrierMultiplicity = rings; Phase = n < .55f ? ElementNextCandidatePhase.Pop : ElementNextCandidatePhase.Residue;
                ShowRole(0, Vector3.up * crown * .22f, Quaternion.identity, new Vector3(crown * (.35f + n), crown * (.25f + burst), 1f), burst, 1.05f, 8f);
                ShowRole(1, Vector3.up * crown * .18f, Quaternion.identity, Vector3.one * crown * (.18f + n * .72f), burst, 1.35f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * crown * (.6f + n), WaterFoam * .42f, .5f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * crown * (1f + rings * .22f), WaterResidue * .62f, .55f, 8f);
                AddRadialParticles(Mathf.Min(particleBudget, droplets), crown * (.2f + n * 1.2f), crown * (burst * .9f - n * .34f), .055f, 1f - n, 1193);
            }
            else
            {
                var radius = GetContentNumber("vortex_radius", 2f); var accel = GetContentNumber("spin_accel", 8f); var height = GetContentNumber("column_height", 2.2f); var grow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .24f, n)); var collapse = 1f - Mathf.SmoothStep(.72f, 1f, n);
                WaterFlow = grow * collapse; WaterFoam = WaterFlow * .6f; WaterSplash = Mathf.Sin(n * Mathf.PI); Phase = n < .24f ? ElementNextCandidatePhase.Growth : n < .72f ? ElementNextCandidatePhase.Flow : ElementNextCandidatePhase.Residue;
                var spin = time * (35f + accel * time * 18f);
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, spin), new Vector3(radius, radius, .45f), WaterFlow * .78f, .9f, 7f);
                ShowRole(1, Vector3.up * height * .35f, Quaternion.Euler(0f, spin * .65f, 0f), new Vector3(radius * .35f, height * .5f, radius * .35f), WaterFlow, 1.3f, 2f);
                ShowRole(2, Vector3.up * height * .25f, Quaternion.Euler(0f, -spin * .35f, 0f), new Vector3(radius * .7f, height * .55f, radius * .7f), WaterFlow * .3f, .45f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, -spin * .18f), Vector3.one * radius * 1.08f, WaterFoam * .5f, .55f, 8f);
                AddArc(0, Vector3.right * radius, Vector3.up * height, 10, radius * .18f, WaterFlow * .68f, .04f, Mathf.FloorToInt(spin * .05f));
                AddArc(1, Vector3.left * radius, Vector3.up * height * .7f, 9, radius * .14f, WaterFlow * .5f, .03f, Mathf.FloorToInt(spin * .05f) + 17);
                AddSpiralParticles(Mathf.Min(particleBudget, 20 + Mathf.RoundToInt(accel * 2f)), radius, height, time * (1f + accel * .12f), false, .05f, WaterFlow, 1229);
            }
        }

        private void EvaluateWind(float time, float n)
        {
            WindOpacity = Mathf.Min(.35f, .14f + GetContentNumber("line_density", 10f) * .004f);
            if (profile == ElementNextCandidateProfile.Tornado)
            {
                var height = GetContentNumber("height", 3.5f); var speed = GetContentNumber("move_speed", 2f); var debris = GetContentText("debris_type", "leaf"); var grow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .2f, n));
                WindOpacity = .24f * grow; WindDebrisCount = debris == "dust" ? 42 : debris == "snow" ? 34 : 26; Phase = n < .2f ? ElementNextCandidatePhase.Growth : ElementNextCandidatePhase.Sustain;
                var drift = Vector3.right * Mathf.Sin(time * Mathf.Max(.2f, speed) * .35f) * .4f;
                ShowRole(0, drift + Vector3.up * height * .45f, Quaternion.Euler(0f, time * (90f + speed * 6f), 0f), new Vector3(.75f, height * .5f, .75f), WindOpacity, .55f, 9f);
                ShowRole(2, drift + Vector3.up * height * .35f, Quaternion.Euler(0f, -time * 72f, 0f), new Vector3(1.15f, height * .55f, 1.15f), WindOpacity * .55f, .38f, 1f);
                ShowRole(3, drift, Quaternion.Euler(68f, 0f, time * 45f), Vector3.one * 1.2f, WindOpacity, .42f, 9f);
                AddArc(0, drift + new Vector3(-.65f, .1f), drift + new Vector3(.35f, height), 10, .23f, WindOpacity * 1.8f, .025f, Mathf.FloorToInt(time * 18f));
                AddArc(1, drift + new Vector3(.65f, .15f), drift + new Vector3(-.2f, height * .82f), 9, .2f, WindOpacity * 1.35f, .02f, Mathf.FloorToInt(time * 18f) + 29);
                AddSpiralParticles(Mathf.Min(particleBudget, WindDebrisCount), .9f, height, time * (1.7f + speed * .08f), false, debris == "leaf" ? .07f : .045f, .82f, 1301);
            }
            else if (profile == ElementNextCandidateProfile.WindBlade)
            {
                var blades = Mathf.Clamp(Mathf.RoundToInt(GetContentNumber("blade_count", 3f)), 1, 3); var length = GetContentNumber("arc_length", 3.2f); var leaves = Mathf.RoundToInt(GetContentNumber("leaf_count", 3f)); var reveal = Mathf.Clamp01(n / .2f); var fade = 1f - Mathf.SmoothStep(.58f, 1f, n);
                WindOpacity = .3f * reveal * fade; WindFlowLineCount = blades; WindDebrisCount = leaves; PrimaryCarrierMultiplicity = blades; Phase = n < .2f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Retract;
                ShowRole(0, Vector3.zero, Quaternion.identity, new Vector3(length * .55f, .5f + blades * .08f, 1f), WindOpacity, .65f, 9f);
                ShowRole(2, Vector3.zero, Quaternion.identity, new Vector3(length * .62f, .72f, 1f), WindOpacity * .35f, .4f, 1f);
                for (var index = 0; index < blades; index++) AddArc(index, new Vector3(-length * .5f, -.28f + index * .28f), new Vector3(length * .5f, .25f + index * .08f), 9, .035f + index * .012f, WindOpacity * (2.2f - index * .3f), .025f, Mathf.FloorToInt(time * 20f) + index * 17);
                AddFlowParticles(Mathf.Min(particleBudget, leaves), length, time * 2.2f, .065f, fade, 1337);
            }
            else
            {
                var length = GetContentNumber("dash_length", 5f); var afterimages = Mathf.Clamp(Mathf.RoundToInt(GetContentNumber("afterimage_count", 2f)), 0, 3); var density = Mathf.RoundToInt(GetContentNumber("line_density", 14f)); var fade = 1f - Mathf.SmoothStep(.66f, 1f, n);
                WindOpacity = Mathf.Min(.32f, .13f + density * .004f) * fade; WindFlowLineCount = density; WindDebrisCount = Mathf.Min(particleBudget, density); PrimaryCarrierMultiplicity = afterimages; Phase = n < .16f ? ElementNextCandidatePhase.Anticipation : n < .7f ? ElementNextCandidatePhase.Flow : ElementNextCandidatePhase.Residue;
                ShowRole(0, Vector3.right * Mathf.Lerp(-length * .5f, length * .5f, n), Quaternion.identity, new Vector3(.36f, .55f, 1f), WindOpacity * 1.7f, .62f, 9f);
                ShowRole(3, Vector3.left * length * .18f, Quaternion.identity, new Vector3(length * .55f, .42f + afterimages * .08f, 1f), WindOpacity, .45f, 9f);
                var lines = Mathf.Min(MaxArcCarriers, Mathf.Max(1, afterimages + 1));
                for (var index = 0; index < lines; index++) AddArc(index, new Vector3(-length * .5f, -.3f + index * .2f), new Vector3(length * .5f, -.2f + index * .16f), 8, .025f, WindOpacity * (1.8f - index * .25f), .018f, Mathf.FloorToInt(time * 22f) - index);
                AddFlowParticles(Mathf.Min(particleBudget, density), length, time * 3f, .035f, fade, 1361);
            }
        }

        private void EvaluateEarth(float time, float n)
        {
            EarthWeight = .9f;
            if (profile == ElementNextCandidateProfile.EarthSpike)
            {
                var count = Mathf.RoundToInt(GetContentNumber("spike_count", 6f)); var speed = GetContentNumber("advance_speed", 5f); var length = GetContentNumber("line_length", 4f); var reveal = Mathf.Clamp01(time * speed / Mathf.Max(.1f, length)); var settle = Mathf.SmoothStep(.58f, 1f, n);
                EarthRise = reveal; EarthOvershoot = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(reveal * 1.15f) * Mathf.PI) * (1f - reveal) * .24f); EarthDust = Mathf.Sin(n * Mathf.PI) * .72f; EarthRevealedSpikeCount = Mathf.Clamp(Mathf.CeilToInt(reveal * count),0,count); PrimaryCarrierMultiplicity = count; Phase = reveal < 1f ? ElementNextCandidatePhase.HeavyRise : ElementNextCandidatePhase.Sustain;
                ShowRoleWithColors(0, Vector3.right * length * (reveal - .5f), Quaternion.identity, new Vector3(length * .5f, reveal + EarthOvershoot, .72f), reveal, .78f, 16f, GetContentColor("rock_tint", primary), secondary, accent);
                ShowRole(1, Vector3.right * length * (reveal - .5f), Quaternion.identity, new Vector3(length * .45f, .2f, .55f), EarthOvershoot * 2.4f, .72f, 2f);
                ShowRole(2, Vector3.right * length * (reveal - .5f), Quaternion.identity, new Vector3(length * .58f, .35f, .8f), EarthDust * .28f, .4f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.identity, new Vector3(length * .55f, .12f, .55f), settle * .48f, .5f, 10f);
                AddRadialParticles(Mathf.Min(particleBudget, count * 5), length * .45f * reveal, .1f - settle * .08f, .075f, EarthDust, 1409);
            }
            else if (profile == ElementNextCandidateProfile.Boulder)
            {
                var scale = GetContentNumber("boulder_scale", 1.2f); var spin = GetContentNumber("spin", 240f); var debris = Mathf.RoundToInt(GetContentNumber("impact_debris_count", 7f)); var dustLife=Mathf.Max(.1f,GetContentNumber("dust_lifetime",1f)); var eventAge = elapsed - triggeredAt; var triggered=eventAge>=0f; var impact = triggered && eventAge <= .38f; var dust=triggered&&eventAge<=dustLife?1f-Mathf.Clamp01(eventAge/dustLife):0f; var travelFade = 1f - Mathf.SmoothStep(.82f, 1f, n);
                EarthRise = triggered ? 0f : travelFade; EarthDust = triggered ? Mathf.Max(impact?1f-eventAge/.38f:0f,dust) : .22f * travelFade; EarthDebrisCount = impact ? debris : 0; Phase = impact ? ElementNextCandidatePhase.Impact : triggered?ElementNextCandidatePhase.Residue:ElementNextCandidatePhase.Flow;
                var position = triggered ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-1f, 1f, n), .1f + Mathf.Sin(n * Mathf.PI) * .25f, 0f);
                if (!triggered) ShowRole(0, position, Quaternion.Euler(time * spin, time * spin * .37f, 0f), Vector3.one * scale, travelFade, .82f, 10f);
                if (!triggered||dust>0f) ShowRole(3, position + (triggered?Vector3.zero:Vector3.left * scale * .8f), Quaternion.identity, new Vector3(scale*(triggered?1.4f:1f), .18f, .45f), EarthDust, .42f, 10f);
                if (impact) ShowRole(4, position, Quaternion.Euler(68f, 0f, 0f), Vector3.one * scale * Mathf.Lerp(.5f, 1.6f, eventAge / .38f), EarthDust, .95f, 10f);
                AddRadialParticles(Mathf.Min(particleBudget, impact ? debris : triggered&&dust>0f?Mathf.Max(4,debris):4), impact ? eventAge * scale * 3f : scale * .65f, impact ? .25f - eventAge * .8f : -.1f, .09f, triggered ? EarthDust : .3f, 1433);
            }
            else
            {
                var cracks = Mathf.RoundToInt(GetContentNumber("crack_count", 5f)); var radius = GetContentNumber("radius", 4f); var rocks = Mathf.RoundToInt(GetContentNumber("float_rock_count", 5f)); var magma = GetContentNumber("magma_glow", .25f); var shock = Mathf.Sin(Mathf.Clamp01(n / .72f) * Mathf.PI);
                EarthRise = shock; EarthDust = shock; EarthDebrisCount = rocks; EarthWeight = 1f; PrimaryCarrierMultiplicity = cracks; Phase = n < .18f ? ElementNextCandidatePhase.Impact : ElementNextCandidatePhase.Residue;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius * Mathf.Max(.02f, n), shock * (.78f + magma * .22f), .72f + magma, 10f);
                for (var index = 0; index < Mathf.Min(cracks, MaxArcCarriers); index++) AddArc(index, Vector3.zero, DeterministicPointOnCircle(radius * Mathf.Clamp01(n * 1.5f), index, 0), 8, .12f, shock * (.8f + magma), .035f, index * 37);
                AddRadialParticles(Mathf.Min(particleBudget, rocks * 4), radius * Mathf.Clamp01(n * 1.4f), shock * .6f - n * .3f, .1f, shock, 1459);
            }
        }

        private void EvaluateNature(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.ThornSnare)
            {
                var radius = GetContentNumber("radius", 3f); var density = Mathf.RoundToInt(GetContentNumber("thorn_density", 16f)); var interval = Mathf.Max(.05f, GetContentNumber("pulse_interval", .7f)); var reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .28f, n)); var cycle = Mathf.Repeat(time, interval) / interval;
                NatureGrowth = reveal; NaturePulse = Mathf.Pow(1f - cycle, 4f); PrimaryCarrierMultiplicity = density; Phase = reveal < 1f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Pulse;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 18f), Vector3.one * radius * reveal, reveal, .82f, 11f);
                ShowRole(4, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius * (1f + NaturePulse * .18f), NaturePulse, 1.25f, 2f);
                for (var index = 0; index < Mathf.Min(3, arcCarriers == null ? 0 : arcCarriers.Length); index++) AddArc(index, DeterministicPointOnCircle(radius, index * 2, 0), DeterministicPointOnCircle(radius, index * 2 + 3, 0), 9, .09f, reveal * .58f, .028f, index * 19);
                AddRadialParticles(Mathf.Min(particleBudget, density / 2), radius, .04f, .045f, reveal, 1501);
            }
            else if (profile == ElementNextCandidateProfile.VineWhip)
            {
                var length = GetContentNumber("whip_length", 4f); var amp = GetContentNumber("wave_amp", .5f); var leaves = Mathf.RoundToInt(GetContentNumber("leaf_count", 6f)); var propagate = Mathf.Clamp01(n / .42f); var retract = Mathf.SmoothStep(.68f, 1f, n); var visible = propagate * (1f - retract);
                NatureGrowth = propagate; NatureWither = retract; NatureBloomCount = leaves; Phase = n < .42f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Retract;
                ShowRole(0, Vector3.right * length * (visible - 1f) * .5f, Quaternion.identity, new Vector3(length * Mathf.Max(.02f, visible), .2f + amp * .15f, 1f), visible, .9f, 11f);
                ShowRole(1, Vector3.right * length * visible, Quaternion.identity, Vector3.one * (.16f + amp * .06f), visible, 1.3f, 2f);
                ShowRole(2, Vector3.right * length * .45f, Quaternion.identity, new Vector3(length * .5f, .32f + amp * .24f, 1f), visible * .26f, .42f, 1f);
                ShowRole(3, Vector3.right * length * .28f, Quaternion.identity, new Vector3(length * .4f, .1f, 1f), retract * (1f - n), .42f, 11f);
                AddCurvedArc(0, Vector3.zero, Vector3.right * length * visible, 11, amp, time * 8f, visible, .055f);
                AddFlowParticles(Mathf.Min(particleBudget, leaves), length * visible, time * 1.4f, .065f, visible, 1523);
            }
            else
            {
                var flowers = Mathf.RoundToInt(GetContentNumber("flower_count", 5f)); var rise = GetContentNumber("rise_speed", 1.4f); var cycle = Mathf.Repeat(time * Mathf.Max(.1f, rise) * .22f, 1f); var revealed = Mathf.Clamp(Mathf.FloorToInt(cycle * (flowers + 1)), 0, flowers);
                NatureGrowth = cycle; NaturePulse = .78f + .22f * Mathf.Sin(time * 3f); NatureBloomCount = revealed; PrimaryCarrierMultiplicity = flowers; Phase = revealed < flowers ? ElementNextCandidatePhase.Bloom : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 9f), Vector3.one * (.65f + revealed * .13f), NaturePulse, .88f, 11f);
                ShowRole(1, Vector3.up * .05f, Quaternion.Euler(68f, 0f, -time * 12f), Vector3.one * (.3f + cycle * .35f), NaturePulse, 1.25f, 2f);
                ShowRole(2, Vector3.up * .35f, Quaternion.identity, new Vector3(.9f, 1.3f, 1f), .22f, .42f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * .9f, .25f, .4f, 11f);
                AddRisingParticles(Mathf.Min(particleBudget, flowers * 5), .85f, time * rise, .055f, .72f, 1549);
            }
        }

        private void EvaluateToxic(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.SporeBurst)
            {
                var radius = GetContentNumber("cloud_radius", 2.4f); var linger = GetContentNumber("linger_time", 1.2f); var spores = Mathf.RoundToInt(GetContentNumber("spore_count", 32f)); var pulseA = Mathf.Exp(-Mathf.Pow((n - .16f) * 11f, 2f)); var pulseB = Mathf.Exp(-Mathf.Pow((n - .34f) * 12f, 2f)); var convergence = 1f - Mathf.SmoothStep(.3f, 1f, n);
                ToxicSwelling = Mathf.Clamp01(pulseA + pulseB * .78f); ToxicLinger = Mathf.Clamp01(linger / 5f) * convergence; Phase = n < .42f ? ElementNextCandidatePhase.Pulse : ElementNextCandidatePhase.Linger;
                ShowRole(0, Vector3.zero, Quaternion.identity, Vector3.one * radius * (.35f + n * .72f), Mathf.Max(ToxicSwelling, ToxicLinger * .72f), .78f, 12f);
                ShowRole(1, Vector3.zero, Quaternion.identity, Vector3.one * radius * (.2f + n), ToxicSwelling, 1.18f, 2f);
                ShowRole(2, Vector3.up * .18f, Quaternion.identity, Vector3.one * radius * (1f + n * .2f), ToxicLinger * .46f, .42f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius, ToxicLinger * .38f, .4f, 12f);
                AddRadialParticles(Mathf.Min(particleBudget, spores), radius * (.2f + n), .15f + Signed(3, 0) * .08f, .055f, Mathf.Max(ToxicSwelling, ToxicLinger), 1601);
            }
            else
            {
                var scale = GetContentNumber("blob_scale", 1f); var drips = GetContentNumber("drip_rate", 8f); var poolLife = Mathf.Max(.1f,GetContentNumber("pool_lifetime", 1.5f)); var bubbles = GetContentNumber("bubble_rate", 5f); var eventAge = elapsed - triggeredAt; var triggered=eventAge>=0f; var poolActive = triggered&&eventAge<=poolLife; var flash=triggered&&eventAge<.25f; var travelFade = 1f - Mathf.SmoothStep(.78f, 1f, n);
                ToxicSwelling = flash ? 1f - Mathf.Clamp01(eventAge / .25f) : triggered?0f:.75f + .25f * Mathf.Sin(time * 9f); ToxicPool = poolActive ? 1f - Mathf.Clamp01(eventAge / poolLife) : 0f; ToxicLinger = ToxicPool; ToxicBubbleCount = poolActive ? Mathf.Min(particleBudget, Mathf.RoundToInt(bubbles * Mathf.Min(poolLife, 2f))) : triggered?0:Mathf.RoundToInt(drips * .4f); Phase = poolActive ? ElementNextCandidatePhase.Linger : triggered?ElementNextCandidatePhase.Residue:ElementNextCandidatePhase.Flow;
                var position = triggered ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-1f, 1f, n), Mathf.Sin(n * Mathf.PI) * .8f, 0f);
                if (!triggered) ShowRole(0, position, Quaternion.Euler(0f, 0f, time * 90f), Vector3.one * scale * (1f + ToxicSwelling * .12f), travelFade, .85f, 12f);
                if (!triggered) ShowRole(2, position + Vector3.left * scale * .55f, Quaternion.identity, new Vector3(scale * .7f, .22f, 1f), travelFade * .4f, .45f, 1f);
                if (poolActive) { var spread=Mathf.Clamp01(eventAge/.3f); ShowRole(3, position, Quaternion.Euler(68f, 0f, 0f), new Vector3(scale * (1.2f + spread*.5f), scale * (.8f + spread*.3f), 1f), ToxicPool, .62f, 12f); }
                if (flash) ShowRole(4, position, Quaternion.identity, Vector3.one * scale * Mathf.Lerp(.25f, 1.4f, eventAge * 4f), 1f - eventAge * 4f, 1.22f, 2f);
                AddRisingParticles(Mathf.Min(particleBudget, ToxicBubbleCount), poolActive ? scale * 1.1f : scale * .35f, time * (poolActive ? bubbles : drips) * .12f, .055f, poolActive ? ToxicPool : triggered?0f:travelFade, 1637);
            }
        }

        private void EvaluateHoly(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.DivineSmite)
            {
                var height = GetContentNumber("pillar_height", 7f); var radius = GetContentNumber("pillar_radius", .7f); var feathers = Mathf.RoundToInt(GetContentNumber("feather_count", 8f)); var after = GetContentNumber("afterglow", .5f); var reveal = Mathf.Clamp01(n / .1f); var retract = Mathf.SmoothStep(.62f, .88f, n); var visible = reveal * (1f - retract);
                var crossReveal=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.1f,.16f,n))*(1f-retract);var ornamentReveal=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.16f,.26f,n))*(1f-retract);
                HolyVerticalReveal = reveal; HolyOrderedReveal = n < .1f ? reveal * .33f : n < .2f ? .33f + (n - .1f) * 3.3f : Mathf.Min(1f, .66f + (n - .2f) * 2f); HolyAfterglow = Mathf.Clamp01(after / 3f) * Mathf.SmoothStep(.58f, .72f, n) * (1f - n); HolyFeatherCount = feathers; Phase = n < .2f ? ElementNextCandidatePhase.Reveal : n < .62f ? ElementNextCandidatePhase.Impact : ElementNextCandidatePhase.Retract;
                var pillarY=n<.1f?height*(1f-visible*.5f):height*(.5f+retract*.5f);
                ShowRole(0, Vector3.up * pillarY, Quaternion.identity, new Vector3(radius, height * .5f * visible, radius), visible, 1.05f, 13f);
                ShowRole(1, Vector3.up * height * .5f, Quaternion.identity, new Vector3(radius * .22f, height * .48f * visible, radius * .22f), visible, 1.7f, 2f);
                ShowRole(2, Vector3.up * height * .42f, Quaternion.identity, new Vector3(radius * 1.7f, height * .56f, radius * 1.7f), HolyAfterglow, .42f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius * (1f + n * 2f), HolyAfterglow + ornamentReveal * .3f, .55f, 13f);
                AddArc(0, new Vector3(-radius * 1.4f, height * .55f), new Vector3(radius * 1.4f, height * .55f), 2, 0f, crossReveal, .045f, 0);
                AddArc(1, Vector3.up * height * .3f, Vector3.up * height * .8f, 2, 0f, crossReveal, .045f, 0);
                AddRisingParticles(Mathf.Min(particleBudget, feathers), radius * 1.7f, time * 1.4f, .075f, Mathf.Max(ornamentReveal, HolyAfterglow), 1709);
            }
            else if (profile == ElementNextCandidateProfile.HolyHalo)
            {
                var tilt = GetContentNumber("halo_tilt", 24f); var dust = Mathf.RoundToInt(GetContentNumber("dust_density", 22f)); var sparkle = GetContentNumber("sparkle_rate", 4f); var tick = Mathf.FloorToInt(time * Mathf.Max(.1f, sparkle));
                HolyOrderedReveal = Mathf.Clamp01(n / .2f); HolyAfterglow = .35f; Phase = n < .2f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.up * .45f, Quaternion.Euler(68f - tilt, 0f, time * 12f), new Vector3(1.2f, .68f, 1f), .65f, .88f, 13f);
                ShowRole(1, Vector3.up * .45f, Quaternion.Euler(68f - tilt, 0f, -time * 18f), new Vector3(.82f, .45f, 1f), .7f, 1.4f, 2f);
                AddArc(0, new Vector3(-.16f, .45f), new Vector3(.16f, .45f), 2, 0f, tick % 2 == 0 ? .9f : .3f, .025f, 0);
                AddArc(1, new Vector3(0f, .29f), new Vector3(0f, .61f), 2, 0f, tick % 2 == 0 ? .9f : .3f, .025f, 0);
                AddRisingParticles(Mathf.Min(particleBudget, dust), 1.1f, time * .45f, .035f, .55f, 1733);
            }
            else
            {
                var radius = GetContentNumber("gate_radius", 2f); var height = GetContentNumber("column_height", 5f); var spiral = GetContentNumber("feather_spiral_speed", 3f); var reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .28f, n)); var retract = Mathf.SmoothStep(.72f, 1f, n); var visible = reveal * (1f - retract);
                HolyOrderedReveal = reveal; HolyVerticalReveal = Mathf.Clamp01((n - .12f) / .24f); HolyAfterglow = (1f - n) * retract; HolyFeatherCount = Mathf.Min(particleBudget, 12); Phase = n < .28f ? ElementNextCandidatePhase.Reveal : n < .72f ? ElementNextCandidatePhase.Bloom : ElementNextCandidatePhase.Retract;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 16f), Vector3.one * radius * reveal, visible, .92f, 13f);
                ShowRole(1, Vector3.up * height * .5f, Quaternion.identity, new Vector3(radius * .32f, height * .5f * HolyVerticalReveal, radius * .32f), visible, 1.35f, 2f);
                ShowRole(2, Vector3.up * height * .45f, Quaternion.identity, new Vector3(radius * .72f, height * .54f, radius * .72f), visible * .28f, .42f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, -time * 12f), Vector3.one * radius * 1.08f, Mathf.Max(HolyAfterglow, visible * .34f), .52f, 13f);
                ShowRole(4, Vector3.up * height * .62f, Quaternion.identity, new Vector3(radius * .65f, .12f, 1f), visible, 1.1f, 2f);
                AddArc(0, Vector3.left * radius, Vector3.right * radius, 7, .03f, visible * .65f, .03f, Mathf.FloorToInt(time * 8f));
                AddSpiralParticles(HolyFeatherCount, radius, height, time * spiral, false, .07f, visible, 1759);
            }
        }

        private void EvaluateShadow(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.ShadowClaw)
            {
                var claws = Mathf.RoundToInt(GetContentNumber("claw_count", 3f)); var jag = GetContentNumber("tear_jaggedness", .6f); var mist = GetContentNumber("mist_amount", .55f); var reveal = Mathf.Clamp01(n / .25f); var close = Mathf.SmoothStep(.62f, 1f, n); var visible = reveal * (1f - close);
                ShadowNegativeSpace = visible; ShadowMist = mist * (1f - n); PrimaryCarrierMultiplicity = claws; Phase = n < .25f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Retract;
                ShowRole(0, Vector3.zero, Quaternion.identity, new Vector3(1.4f, .75f * (1f - close), 1f), visible, .86f, 17f);
                ShowRole(2, Vector3.down * n * .2f, Quaternion.identity, new Vector3(1.55f, .95f, 1f), ShadowMist * .5f, .4f, 1f);
                for (var index = 0; index < Mathf.Min(claws, MaxArcCarriers); index++)
                {
                    var stagger = Mathf.Clamp01((n - index * .055f) / .2f) * (1f - close);
                    AddArc(index, new Vector3(-.85f, -.45f + index * .34f), new Vector3(.85f, -.18f + index * .3f), 10, .08f + jag * .22f, stagger, .055f, index * 47);
                }
                AddRisingParticles(Mathf.Min(particleBudget, Mathf.RoundToInt(mist * 35f)), 1.1f, -time * .45f, .055f, ShadowMist, 1801);
            }
            else if (profile == ElementNextCandidateProfile.VoidOrb)
            {
                var radius = GetContentNumber("orb_radius", .7f); var rate = Mathf.RoundToInt(GetContentNumber("suction_particle_rate", 30f)); var implodeScale = GetContentNumber("implode_scale", 1.25f); var eventAge = elapsed - triggeredAt; var triggered=eventAge>=0f; var impact = triggered && eventAge <= .32f; var travelFade = 1f - Mathf.SmoothStep(.82f, 1f, n);
                ShadowSuction = triggered ? 0f : travelFade; ShadowImplode = impact ? 1f - eventAge / .32f : 0f; ShadowNegativeSpace = impact ? ShadowImplode : triggered?0f:travelFade; Phase = impact ? ElementNextCandidatePhase.Implode : triggered?ElementNextCandidatePhase.Residue:ElementNextCandidatePhase.Suction;
                var position = triggered ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-1f, 1f, n), Mathf.Sin(time * 5f) * .08f, 0f);
                if(!triggered||impact)ShowRoleWithColors(0, position, Quaternion.Euler(time * 41f, time * 27f, 0f), Vector3.one * radius * (impact ? Mathf.Lerp(implodeScale, .02f, eventAge / .32f) : 1f), ShadowNegativeSpace, .22f, 14f, Color.black, primary, accent);
                if(!triggered||impact)ShowRole(1, position, Quaternion.Euler(0f, 0f, -time * 55f), Vector3.one * radius * (impact ? Mathf.Lerp(1.8f, .1f, eventAge / .32f) : 1.16f), ShadowNegativeSpace, 1.25f, 2f);
                if(!triggered)ShowRole(2, position, Quaternion.identity, Vector3.one * radius * 1.55f, ShadowSuction * .28f, .38f, 1f);
                if(!triggered)ShowRole(3, position + Vector3.left * radius, Quaternion.identity, new Vector3(radius * 1.3f, .18f, 1f), .32f * travelFade, .38f, 14f);
                if (impact) ShowRole(4, position, Quaternion.identity, Vector3.one * implodeScale * Mathf.Lerp(1.6f, .2f, eventAge / .32f), ShadowImplode, 1.35f, 2f);
                AddSpiralParticles(triggered&&!impact?0:Mathf.Min(particleBudget, rate), radius * 2.2f, radius, time * 2.4f, true, .045f, Mathf.Max(ShadowSuction, ShadowImplode), 1823);
            }
            else if (profile == ElementNextCandidateProfile.ShadowGrasp)
            {
                var radius = GetContentNumber("pool_radius", 3f); var hands = Mathf.RoundToInt(GetContentNumber("hand_count", 3f)); var interval = Mathf.Max(.05f, GetContentNumber("tick_interval", .65f)); var height = GetContentNumber("hand_height", 1.5f); var cycle = Mathf.Repeat(time, interval) / interval; var reveal = Mathf.Sin(cycle * Mathf.PI);
                ShadowNegativeSpace = .82f; ShadowSuction = reveal; ShadowHandCount = hands; PrimaryCarrierMultiplicity = hands; Phase = cycle < .5f ? ElementNextCandidatePhase.Reveal : ElementNextCandidatePhase.Retract;
                ShowRoleWithColors(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 9f), Vector3.one * radius, .82f, .3f, 14f, Color.black, primary, accent);
                ShowRole(2, Vector3.up * height * reveal * .5f, Quaternion.identity, new Vector3(radius * .62f, height * reveal, 1f), .35f, .42f, 1f);
                ShowRole(4, Vector3.up * height * reveal * .5f, Quaternion.identity, new Vector3(radius * .46f, height * reveal, 1f), reveal, .95f, 14f);
                for (var index = 0; index < Mathf.Min(hands, MaxArcCarriers); index++) AddCurvedArc(index, DeterministicPointOnCircle(radius * .65f, index, 0), DeterministicPointOnCircle(radius * .18f, index + 4, 0) + Vector3.up * height * reveal, 8, .18f, index, reveal, .05f);
                AddRadialParticles(Mathf.Min(particleBudget, hands * 5), radius * .8f, .05f, .05f, .55f, 1847);
            }
            else
            {
                var glyph = Mathf.RoundToInt(GetContentNumber("mark_glyph", 2f)); var rate = GetContentNumber("pulse_rate", 1.5f); var smoke = GetContentNumber("smoke_amount", .4f); var pulse = .55f + .45f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * rate * Mathf.PI * 2f)), 4f);
                ShadowNegativeSpace = pulse; ShadowMist = smoke; PrimaryCarrierMultiplicity = glyph; Phase = ElementNextCandidatePhase.Pulse;
                ShowRoleWithColors(0, Vector3.zero, Quaternion.identity, new Vector3(.8f + glyph * .08f, .8f + glyph * .08f, 1f), pulse, .45f, 14f, Color.black, primary, accent);
                ShowRole(1, Vector3.zero, Quaternion.Euler(0f, 0f, time * -12f), Vector3.one * (.62f + glyph * .06f), pulse, 1.12f, 2f);
                ShowRole(2, Vector3.up * .35f, Quaternion.identity, Vector3.one * (1f + smoke * .4f), smoke * .32f, .38f, 1f);
                ShowRole(3, Vector3.down * .15f, Quaternion.identity, Vector3.one * .9f, smoke * .35f, .38f, 14f);
                AddRisingParticles(Mathf.Min(particleBudget, Mathf.RoundToInt(smoke * 24f)), .7f, time * .3f, .045f, smoke, 1871);
            }
        }

        private void EvaluateArcane(float time, float n)
        {
            if (profile == ElementNextCandidateProfile.ArcaneMissile)
            {
                var count = Mathf.Clamp(Mathf.RoundToInt(GetContentNumber("missile_count", 3f)), 1, 5); var stagger = Mathf.Max(0f, GetContentNumber("stagger_interval", .1f)); var wobble = GetContentNumber("wobble_amp", .22f); var eventAge = elapsed - triggeredAt; var triggered=eventAge>=0f; var impact = triggered && eventAge <= .28f; var elapsedLaunches = stagger <= .001f ? count : Mathf.Clamp(Mathf.FloorToInt(time / stagger) + 1, 0, count);
                ArcaneMissileCount = count; ArcaneStaggerStep = elapsedLaunches; ArcaneActivation = triggered?(impact?1f-eventAge/.28f:0f):elapsedLaunches / (float)count; PrimaryCarrierMultiplicity = count; Phase = impact ? ElementNextCandidatePhase.Impact : triggered?ElementNextCandidatePhase.Residue:elapsedLaunches < count ? ElementNextCandidatePhase.Activation : ElementNextCandidatePhase.Flow;
                if (!triggered) ShowRole(0, Vector3.right * Mathf.Lerp(-.85f, .85f, n), Quaternion.Euler(0f, 0f, time * 120f), new Vector3(.32f + count * .05f, .24f + wobble * .08f, 1f), 1f - Mathf.SmoothStep(.82f, 1f, n), .95f, 15f);
                for (var index = 0; !triggered && index < elapsedLaunches && index < MaxArcCarriers; index++)
                {
                    var localAge = Mathf.Max(0f, time - index * stagger); var end = impact ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-.9f, .9f, n), Mathf.Sin(localAge * 8f + index) * wobble + (index - (count - 1) * .5f) * .12f, 0f);
                    AddCurvedArc(index, end + Vector3.left * (.65f + index * .08f), end, 8, wobble, localAge * 9f + index, impact ? 1f - eventAge / .28f : 1f - n * .35f, .035f);
                }
                if (impact) ShowRole(4, triggeredLocalPosition, Quaternion.identity, Vector3.one * Mathf.Lerp(.25f, 1.25f, eventAge / .28f), 1f - eventAge / .28f, 1.35f, 2f);
                AddRadialParticles(triggered&&!impact?0:Mathf.Min(particleBudget, impact ? count * 4 : count * 2), impact ? eventAge * 2.5f : .3f, 0f, .045f, impact ? 1f - eventAge / .28f : triggered?0f:.65f, 1901);
            }
            else
            {
                var radius = GetContentNumber("ring_radius", 2f); var glyphs = Mathf.Clamp(Mathf.RoundToInt(GetContentNumber("glyph_count", 10f)), 8, 12); var speed = GetContentNumber("spin_speed", 3f); var activate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .46f, n)); var retract = Mathf.SmoothStep(.72f, 1f, n); var active = Mathf.Clamp(Mathf.FloorToInt(activate * (glyphs + 1)), 0, glyphs);
                ArcaneGlyphCount = glyphs; ArcaneStaggerStep = active; ArcaneActivation = activate * (1f - retract); PrimaryCarrierMultiplicity = glyphs; Phase = n < .46f ? ElementNextCandidatePhase.Activation : n < .72f ? ElementNextCandidatePhase.Sustain : ElementNextCandidatePhase.Retract;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * speed * 18f), Vector3.one * radius * Mathf.Max(.02f, activate), ArcaneActivation, .9f, 15f);
                ShowRole(1, Vector3.zero, Quaternion.Euler(68f, 0f, -time * speed * 27f), Vector3.one * radius * .72f * Mathf.Max(.02f, activate), ArcaneActivation, 1.35f, 2f);
                ShowRole(2, Vector3.up * .08f, Quaternion.Euler(68f, 0f, time * speed * 8f), Vector3.one * radius * 1.12f, ArcaneActivation * .24f, .38f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, -time * speed * 12f), Vector3.one * radius, retract * (1f - n), .45f, 15f);
                AddArc(0, DeterministicPointOnCircle(radius, GetArcaneActivationOrdinal(0), 0), DeterministicPointOnCircle(radius, GetArcaneActivationOrdinal(glyphs / 2), 0), 9, .035f, ArcaneActivation * .7f, .026f, 0);
                AddArc(1, DeterministicPointOnCircle(radius * .72f, GetArcaneActivationOrdinal(1), 0), DeterministicPointOnCircle(radius * .72f, GetArcaneActivationOrdinal(glyphs / 2 + 1), 0), 9, .035f, ArcaneActivation * .55f, .022f, 17);
                AddRadialParticles(Mathf.Min(particleBudget, active * 2), radius, .08f, .045f, ArcaneActivation, 1933);
            }
        }

        private float W6W8TailDuration()
        {
            if (family == ElementNextCandidateFamily.Water && profile == ElementNextCandidateProfile.WaterJet) return .3f;
            if (family == ElementNextCandidateFamily.Earth && profile == ElementNextCandidateProfile.Boulder) return Mathf.Clamp(GetContentNumber("dust_lifetime", 1f), .2f, 1.5f);
            if (family == ElementNextCandidateFamily.Nature && profile == ElementNextCandidateProfile.ThornSnare) return Mathf.Clamp(GetContentNumber("wither_time", .8f), .2f, 1.5f);
            if (family == ElementNextCandidateFamily.Toxic && profile == ElementNextCandidateProfile.AcidLob) return Mathf.Clamp(GetContentNumber("pool_lifetime", 1.5f), .25f, 1.5f);
            if (family == ElementNextCandidateFamily.Holy) return Mathf.Clamp(GetContentNumber("afterglow", .45f), .25f, 1.25f);
            return .42f;
        }

        private void EvaluateW6W8Tail(float t, float alpha)
        {
            ResetW6W8Readback();
            if (family == ElementNextCandidateFamily.Water)
            {
                Phase = profile == ElementNextCandidateProfile.WaterJet ? ElementNextCandidatePhase.Flow : ElementNextCandidatePhase.Residue; WaterSag = profile == ElementNextCandidateProfile.WaterJet ? t : 0f; WaterResidue = alpha;
                ShowRole(3, Vector3.down * WaterSag * .35f, Quaternion.Euler(68f, 0f, 0f), new Vector3(1f + t, .22f + t * .24f, 1f), alpha * .62f, .52f, 8f); AddRadialParticles(Mathf.Min(particleBudget, 10), .35f + t, -.12f - t * .4f, .05f, alpha, 2003);
            }
            else if (family == ElementNextCandidateFamily.Wind)
            {
                Phase = ElementNextCandidatePhase.Residue; WindOpacity = alpha * .24f; WindDebrisCount = Mathf.Min(particleBudget, 10); ShowRole(3, Vector3.right * t * .4f, Quaternion.identity, new Vector3(1f + t, .3f, 1f), WindOpacity, .38f, 9f); AddFlowParticles(WindDebrisCount, 1.5f, t * 2f, .04f, alpha, 2027);
            }
            else if (family == ElementNextCandidateFamily.Earth)
            {
                Phase = ElementNextCandidatePhase.Residue; EarthDust = alpha; ShowRole(3, Vector3.down * t * .08f, Quaternion.Euler(68f, 0f, 0f), new Vector3(1f + t * .7f, .2f + t * .15f, 1f), alpha * .55f, .42f, 10f); AddRadialParticles(Mathf.Min(particleBudget, 12), .45f + t, -.08f - t * .18f, .065f, alpha, 2053);
            }
            else if (family == ElementNextCandidateFamily.Nature)
            {
                Phase = ElementNextCandidatePhase.Wither; NatureWither = t; var brown = new Color(.31f, .2f, .06f, 1f); ShowRoleWithColors(0, Vector3.down * t * .16f, Quaternion.identity, new Vector3(1f - t * .45f, 1f - t * .55f, 1f), alpha, .62f, 11f, Color.Lerp(primary, brown, t), brown, accent); AddRadialParticles(Mathf.Min(particleBudget, 8), .5f, -.05f - t * .2f, .045f, alpha, 2081);
            }
            else if (family == ElementNextCandidateFamily.Toxic)
            {
                var scale=profile==ElementNextCandidateProfile.AcidLob?GetContentNumber("blob_scale",1f):1f;Phase = ElementNextCandidatePhase.Linger; ToxicLinger = ToxicPool = alpha; ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), new Vector3(scale*(1f + t * .35f), scale*(.75f + t * .12f), 1f), alpha * .65f, .5f, 12f); AddRisingParticles(Mathf.Min(particleBudget, 8), scale*.8f, t, .045f, alpha, 2111);
            }
            else if (family == ElementNextCandidateFamily.Holy)
            {
                Phase = ElementNextCandidatePhase.Afterglow; HolyAfterglow = alpha; ShowRole(2, Vector3.up * t * .5f, Quaternion.identity, Vector3.one * (1f + t * .3f), alpha * .42f, .42f, 1f); AddRisingParticles(Mathf.Min(particleBudget, 8), .7f, t, .05f, alpha, 2137);
            }
            else if (family == ElementNextCandidateFamily.Shadow)
            {
                Phase = ElementNextCandidatePhase.Residue; ShadowMist = alpha; ShowRole(2, Vector3.down * t * .25f, Quaternion.identity, Vector3.one * (1f + t * .25f), alpha * .4f, .35f, 1f); AddRisingParticles(Mathf.Min(particleBudget, 8), .7f, -t, .05f, alpha, 2161);
            }
            else
            {
                Phase = ElementNextCandidatePhase.Retract; ArcaneActivation = alpha; ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, -t * 90f), Vector3.one * (1f - t * .72f), alpha * .55f, .45f, 15f); AddRadialParticles(Mathf.Min(particleBudget, 6), .7f * (1f - t), 0f, .04f, alpha, 2187);
            }
        }

        private void ResetW6W8Readback()
        {
            WaterFlow = WaterFoam = WaterSplash = WaterResidue = WaterSag = 0f;
            WindOpacity = 0f; WindDebrisCount = WindFlowLineCount = 0;
            EarthWeight = EarthRise = EarthOvershoot = EarthDust = 0f; EarthDebrisCount = EarthRevealedSpikeCount = 0;
            NatureGrowth = NaturePulse = NatureWither = 0f; NatureBloomCount = 0;
            ToxicSwelling = ToxicLinger = ToxicPool = 0f; ToxicBubbleCount = 0;
            HolyOrderedReveal = HolyVerticalReveal = HolyAfterglow = 0f; HolyFeatherCount = 0;
            ShadowNegativeSpace = ShadowMist = ShadowSuction = ShadowImplode = 0f; ShadowHandCount = 0;
            ArcaneActivation = 0f; ArcaneGlyphCount = ArcaneMissileCount = ArcaneStaggerStep = 0;
        }

        private float ParticleCarrierMode()
        {
            return family == ElementNextCandidateFamily.Lightning ? 5f : family == ElementNextCandidateFamily.Wind ? 9f : family == ElementNextCandidateFamily.Shadow ? 14f : family == ElementNextCandidateFamily.Arcane ? 15f : 2f;
        }

        private float SemanticProgress()
        {
            if(family==ElementNextCandidateFamily.Water)return Mathf.Max(WaterFlow,Mathf.Max(WaterSplash,WaterResidue));
            if(family==ElementNextCandidateFamily.Wind)return Mathf.Clamp01(WindOpacity/.35f);
            if(family==ElementNextCandidateFamily.Earth)return EarthRise;
            if(family==ElementNextCandidateFamily.Nature)return NatureWither>0f?1f-NatureWither:NatureGrowth;
            if(family==ElementNextCandidateFamily.Toxic)return Mathf.Max(ToxicSwelling,Mathf.Max(ToxicLinger,ToxicPool));
            if(family==ElementNextCandidateFamily.Holy)return HolyOrderedReveal;
            if(family==ElementNextCandidateFamily.Shadow)return Mathf.Max(ShadowNegativeSpace,Mathf.Max(ShadowSuction,ShadowImplode));
            if(family==ElementNextCandidateFamily.Arcane)return ArcaneActivation;
            return NormalizedTime;
        }

        private void AddFlowParticles(int count, float length, float phase, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var t = Mathf.Repeat((index + .5f) / Mathf.Max(1, count) + phase * (.08f + Hash01(seed + (uint)(salt + index)) * .08f), 1f);
                AddParticle(new Vector3(Mathf.Lerp(-length * .5f, length * .5f, t), Signed(salt + index * 3, 0) * .28f, Signed(salt + index * 5, 1) * .12f), size, alpha, salt + index);
            }
        }

        private void AddCurtainParticles(int count, float centerX, float width, float height, float phase, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var x = centerX + Signed(salt + index * 3, 0) * width * .5f; var fall = Mathf.Repeat(phase * 1.6f + Hash01(seed + (uint)(salt + index)), 1f);
                AddParticle(new Vector3(x, height * (1f - fall), Signed(salt + index * 7, 1) * .24f), size, alpha, salt + index);
            }
        }

        private void AddSpiralParticles(int count, float radius, float height, float phase, bool inward, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var lane = (index + .5f) / Mathf.Max(1, count); var cycle = Mathf.Repeat(lane + phase * .08f, 1f); var radial = radius * (inward ? 1f - cycle * .78f : .22f + cycle * .78f); var angle = lane * Mathf.PI * 8f + phase;
                AddParticle(new Vector3(Mathf.Cos(angle) * radial, inward ? Signed(salt + index, 0) * height * .12f : cycle * height, Mathf.Sin(angle) * radial), size, alpha, salt + index);
            }
        }

        private void AddRisingParticles(int count, float radius, float phase, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var t = Mathf.Repeat(Hash01(seed + (uint)(salt + index * 11)) + phase * (.12f + Hash01(seed + (uint)(salt + index * 17)) * .12f), 1f); var angle = Hash01(seed + (uint)(salt + index * 23)) * Mathf.PI * 2f;
                AddParticle(new Vector3(Mathf.Cos(angle) * radius * (.35f + .65f * t), Mathf.Lerp(-.25f, 1.25f, t), Mathf.Sin(angle) * radius * .25f), size, alpha, salt + index);
            }
        }

        private void AddCurvedArc(int index, Vector3 start, Vector3 end, int pointCount, float amplitude, float phase, float alpha, float width)
        {
            if (alpha <= .001f || arcCarriers == null || index < 0 || index >= arcCarriers.Length || index >= MaxArcCarriers) return;
            var line = arcCarriers[index]; if (line == null) return; pointCount = Mathf.Clamp(pointCount, 2, MaxArcPoints); line.useWorldSpace = false; line.positionCount = pointCount; line.widthMultiplier = Mathf.Max(.004f, width); line.enabled = true;
            for (var point = 0; point < pointCount; point++)
            {
                var t = point / (float)(pointCount - 1); var envelope = Mathf.Sin(t * Mathf.PI); var value = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI * 2f + phase) * amplitude * envelope;
                line.SetPosition(point, value); sampledArcPoints[index, point] = value;
            }
            sampledArcPointCounts[index] = pointCount; ApplyProperties(line, alpha, 1.35f, (float)family + 5f); VisibleArcCount++; ActiveLayerCount++; framePeakAlpha = Mathf.Max(framePeakAlpha, Mathf.Clamp01(alpha));
        }
    }
}
