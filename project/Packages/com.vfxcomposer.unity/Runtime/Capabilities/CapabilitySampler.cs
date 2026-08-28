using System;
using UnityEngine;

namespace VFXComposer.Capabilities
{
    /// <summary>Pure deterministic behavior sampler. It never reads scene state or renders.</summary>
    public static class CapabilitySampler
    {
        public static CapabilitySampleTrace SampleTrajectory(CapabilitySampleRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            var dt = Mathf.Clamp(request.DeltaTime, 1f / 1000f, .25f);
            var duration = Mathf.Max(dt, request.Duration);
            var trace = new CapabilitySampleTrace
            {
                MotionType = request.MotionType ?? "linear",
                HitType = request.HitType ?? "single",
                EmissionType = request.EmissionType ?? "single",
                TimingType = request.TimingType ?? "instant",
                Seed = request.Seed
            };
            var initialSpeed = (float)request.Get(request.Motion, request.MotionType == "accel" ? "init_speed" : "speed", 4d);
            var state = new State
            {
                Position = request.Origin,
                Velocity = SafeDirection(request.Direction) * Mathf.Max(0f, initialSpeed),
                LastPosition = request.Origin,
                Endpoint = request.Target,
                Random = new DeterministicRandom(request.Seed)
            };
            Emit(trace, "on_launch", 0, 0f, 0, request.Origin, request.EmissionType);
            EmitEmissionEvents(request, trace);
            var frameCount = Mathf.CeilToInt(duration / dt);
            for (var frame = 0; frame <= frameCount; frame++)
            {
                var time = Mathf.Min(duration, frame * dt);
                if (frame > 0) StepMotion(request, state, dt, time);
                ApplyTiming(request, trace, state, frame, time, duration);
                ApplySpatial(request, trace, state, frame, time);
                ApplyHit(request, trace, state, frame, time, duration);
                trace.Frames.Add(new CapabilitySampleFrame
                {
                    Index = frame,
                    Time = time,
                    Position = state.Position,
                    Velocity = state.Velocity,
                    Source = request.Origin,
                    Target = IsBeam(request) ? state.Endpoint : state.Position,
                    Radius = state.Radius,
                    Width = state.Width,
                    Progress = state.Progress,
                    Stage = state.Stage
                });
                state.LastPosition = state.Position;
            }
            Emit(trace, "on_expire", frameCount, duration, 0, state.Position, string.Empty);
            return trace;
        }

        private static void StepMotion(CapabilitySampleRequest request, State state, float dt, float time)
        {
            if (request.HitType == "chain_hop" && StepChainHopMotion(request, state, dt, time)) return;
            var type = request.MotionType ?? "linear";
            if (type == "stationary") return;
            if (type == "accel")
            {
                var speed = Mathf.Min((float)request.Get(request.Motion, "max_speed", 10d), state.Velocity.magnitude + (float)request.Get(request.Motion, "accel", 3d) * dt);
                state.Velocity = SafeDirection(state.Velocity) * Mathf.Max(0f, speed);
            }
            else if (type == "parabola")
            {
                var flight = Mathf.Max(.05f, (float)request.Get(request.Motion, "flight_time", request.Duration));
                var t = Mathf.Clamp01(time / flight); var apex = (float)request.Get(request.Motion, "apex_height", 2d);
                var next = Vector3.Lerp(request.Origin, request.Target, t) + Vector3.up * (4f * apex * t * (1f - t));
                state.Velocity = (next - state.Position) / dt; state.Position = next; return;
            }
            else if (type == "homing")
            {
                var target = request.Target + request.TargetVelocity * time;
                var desired = SafeDirection(target - state.Position);
                var maxRadians = Mathf.Deg2Rad * Mathf.Max(0f, (float)request.Get(request.Motion, "turn_rate", 180d)) * dt;
                var direction = Vector3.RotateTowards(SafeDirection(state.Velocity), desired, maxRadians, 0f);
                state.Velocity = direction * Mathf.Max(.01f, (float)request.Get(request.Motion, "max_speed", 5d));
            }
            else if (type == "wave")
            {
                var speed = (float)request.Get(request.Motion, "speed", 4d); var amplitude = (float)request.Get(request.Motion, "amplitude", .5d); var frequency = (float)request.Get(request.Motion, "frequency", 2d);
                var forward = SafeDirection(request.Direction); var lateral = Vector3.Cross(forward, Vector3.forward); if (lateral.sqrMagnitude < .001f) lateral = Vector3.up;
                var next = request.Origin + forward * speed * time + lateral.normalized * amplitude * Mathf.Sin(time * frequency * Mathf.PI * 2f);
                state.Velocity = (next - state.Position) / dt; state.Position = next; return;
            }
            else if (type == "boomerang")
            {
                var distance = Mathf.Max(.1f, (float)request.Get(request.Motion, "out_distance", 4d)); var hover = Mathf.Max(0f, (float)request.Get(request.Motion, "hover_time", .2d)); var outSpeed = Mathf.Max(.1f, (float)request.Get(request.Motion, "speed", 4d)); var returnSpeed = Mathf.Max(.1f, (float)request.Get(request.Motion, "return_speed", 5d));
                var outTime = distance / outSpeed; var backStart = outTime + hover; Vector3 next;
                if (time <= outTime) { state.Stage = 0; next = request.Origin + SafeDirection(request.Direction) * Mathf.Min(distance, time * outSpeed); }
                else if (time <= backStart) { state.Stage = 1; next = request.Origin + SafeDirection(request.Direction) * distance; }
                else { state.Stage = 2; next = Vector3.MoveTowards(request.Origin + SafeDirection(request.Direction) * distance, request.Origin + request.TargetVelocity * time, (time - backStart) * returnSpeed); }
                state.Velocity = (next - state.Position) / dt; state.Position = next; return;
            }
            else if (type == "orbit_then_strike")
            {
                var turns = Mathf.Max(.1f, (float)request.Get(request.Motion, "orbit_turns", 1d)); var orbitTime = Mathf.Max(.1f, (float)request.Get(request.Motion, "orbit_time", 1d)); var radius = Mathf.Max(.01f, (float)request.Get(request.Motion, "orbit_radius", 1d)); Vector3 next;
                if (time <= orbitTime) { state.Stage = 0; var angle = time / orbitTime * turns * Mathf.PI * 2f; next = request.Origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius; }
                else { state.Stage = 1; var strike = Mathf.Max(.1f, (float)request.Get(request.Motion, "strike_speed", 8d)); next = Vector3.MoveTowards(state.Position, request.Target + request.TargetVelocity * time, strike * dt); }
                state.Velocity = (next - state.Position) / dt; state.Position = next; return;
            }
            else if (type == "dash")
            {
                var distance = Mathf.Max(0f, (float)request.Get(request.Motion, "distance", 4d));
                var dashDuration = Mathf.Max(.01f, (float)request.Get(request.Motion, "duration", request.Duration));
                var normalized = Mathf.Clamp01(time / dashDuration);
                var eased = normalized * normalized * (3f - 2f * normalized);
                var next = request.Origin + SafeDirection(request.Direction) * distance * eased;
                state.Velocity = (next - state.Position) / dt;
                state.Position = next;
                state.Progress = normalized;
                return;
            }
            else if (type == "sweep")
            {
                var maxSpeed = Mathf.Max(0f, (float)request.Get(request.Motion, "sweep_speed_max", 90d));
                var inertia = Mathf.Max(0f, (float)request.Get(request.Motion, "inertia", .15d));
                var baseVector = request.Target - request.Origin;
                var angle = Mathf.Min(maxSpeed * time, 120f);
                var driven = request.Origin + Quaternion.AngleAxis(angle, Vector3.forward) * baseVector;
                var factor = inertia <= .0001f ? 1f : 1f - Mathf.Exp(-dt / inertia);
                var previous = state.Endpoint;
                state.Endpoint = Vector3.Lerp(state.Endpoint, driven, factor);
                state.Velocity = (state.Endpoint - previous) / dt;
                state.Position = state.Endpoint;
                return;
            }
            else if (type == "bounce")
            {
                StepBounce(request, state, dt, time);
                return;
            }
            else if (type == "expand_ring" || type == "implode" || type == "moving_zone" || type == "growth_stage")
            {
                StepAreaMotion(request, state, dt, time, type); return;
            }
            state.Position += state.Velocity * dt;
        }

        private static bool StepChainHopMotion(CapabilitySampleRequest request, State state, float dt, float time)
        {
            var count = Mathf.Max(1, (int)request.Get(request.Hit, "hop_count", 3d));
            var firstHopTime = Mathf.Max(dt, request.Duration * .35f);
            var interval = ChainHopInterval(request.Duration, firstHopTime, count, dt);
            var pathStart = Mathf.Max(0f, firstHopTime - interval);
            if (!state.ChainStarted && time + .00001f < pathStart) return false;
            if (!state.ChainStarted)
            {
                state.ChainStarted = true;
                state.ChainAnchor = state.Position;
            }

            var phase = Mathf.Max(0f, (time - pathStart) / interval);
            var completed = Mathf.Clamp(Mathf.FloorToInt(phase), 0, count);
            Vector3 next;
            if (completed >= count) next = ChainHopTarget(request, state.ChainAnchor, count, count);
            else
            {
                var from = completed == 0 ? state.ChainAnchor : ChainHopTarget(request, state.ChainAnchor, completed, count);
                var to = ChainHopTarget(request, state.ChainAnchor, completed + 1, count);
                var progress = phase - completed;
                progress = progress * progress * (3f - 2f * progress);
                next = Vector3.Lerp(from, to, progress);
            }
            state.Velocity = (next - state.Position) / Mathf.Max(.0001f, dt);
            state.Position = next;
            return true;
        }

        private static float ChainHopInterval(float duration, float firstHopTime, int count, float dt)
        {
            return Mathf.Max(dt, Mathf.Max(dt, duration - firstHopTime) / (count + 1));
        }

        private static Vector3 ChainHopTarget(CapabilitySampleRequest request, Vector3 anchor, int index, int count)
        {
            var forward = SafeDirection(request.Direction);
            var lateral = Vector3.Cross(Vector3.forward, forward);
            if (lateral.sqrMagnitude < .000001f) lateral = Vector3.up;
            lateral.Normalize();
            var range = Mathf.Max(0f, (float)request.Get(request.Hit, "hop_range", 4d));
            var step = range / Mathf.Max(1, count);
            var offset = range <= .000001f ? 0f : Mathf.Min(.45f, Mathf.Max(.18f, step * .4f));
            return anchor + forward * (step * index) + lateral * (index % 2 == 0 ? -offset : offset);
        }

        private static void StepBounce(CapabilitySampleRequest request, State state, float dt, float time)
        {
            var maxBounces = Mathf.Max(1, (int)request.Get(request.Motion, "bounce_count", 3d));
            var damping = Mathf.Clamp01((float)request.Get(request.Motion, "energy_damping", .2d));
            var next = state.Position + state.Velocity * dt;
            if (state.BounceCount >= maxBounces)
            {
                state.Position = next;
                return;
            }

            var normal = Vector3.zero;
            if (next.y < request.CollisionMin.y) { next.y = request.CollisionMin.y + (request.CollisionMin.y - next.y); normal = Vector3.up; }
            else if (next.y > request.CollisionMax.y) { next.y = request.CollisionMax.y - (next.y - request.CollisionMax.y); normal = Vector3.down; }
            else if (next.x < request.CollisionMin.x) { next.x = request.CollisionMin.x + (request.CollisionMin.x - next.x); normal = Vector3.right; }
            else if (next.x > request.CollisionMax.x) { next.x = request.CollisionMax.x - (next.x - request.CollisionMax.x); normal = Vector3.left; }

            if (normal.sqrMagnitude > .5f)
            {
                var incoming = state.Velocity;
                state.Velocity = Vector3.Reflect(incoming, normal) * (1f - damping);
                state.BounceCount++;
                state.PendingBounce = true;
                state.BounceBefore = incoming;
                state.BounceAfter = state.Velocity;
                state.BounceTime = time;
            }
            state.Position = next;
        }

        private static void StepAreaMotion(CapabilitySampleRequest request, State state, float dt, float time, string type)
        {
            if (type == "expand_ring") state.Radius = Mathf.Min((float)request.Get(request.Motion, "max_radius", 5d), time * (float)request.Get(request.Motion, "expand_speed", 3d));
            else if (type == "implode") { var start = (float)request.Get(request.Motion, "start_radius", 5d); var collapse = Mathf.Max(.01f, (float)request.Get(request.Motion, "collapse_time", 1d)); state.Radius = start * (1f - Mathf.Clamp01(time / collapse)); }
            else if (type == "moving_zone")
            {
                if (state.TerminalFired) return;
                var lag = Mathf.Max(0f, (float)request.Get(request.Motion, "follow_lag", .2d));
                var desired = request.ExternalPath != null && request.ExternalPath.Length >= 2
                    ? EvaluateExternalPath(request.ExternalPath, Mathf.Clamp01(time / Mathf.Max(.01f, request.Duration)))
                    : request.Target + request.TargetVelocity * time;
                var factor = lag <= .0001f ? 1f : 1f - Mathf.Exp(-dt / lag);
                state.Position = Vector3.Lerp(state.Position, desired, factor);
                state.Velocity = (state.Position - state.LastPosition) / dt;
            }
            else { var count = Mathf.Clamp((int)request.Get(request.Motion, "stage_count", 3d), 2, 3); var interval = Mathf.Max(.05f, request.Duration / count); state.Stage = Mathf.Min(count - 1, Mathf.FloorToInt(time / interval)); state.Radius = (float)request.Get(request.Motion, "base_radius", 1d) * (state.Stage + 1); }
        }

        private static void ApplyTiming(CapabilitySampleRequest request, CapabilitySampleTrace trace, State state, int frame, float time, float duration)
        {
            var type = request.TimingType ?? "instant";
            if (type == "telegraph")
            {
                var warn = Mathf.Max(0f, (float)request.Get(request.Timing, "warn_duration", .8d)); state.Stage = time < warn ? 0 : 1;
                state.Progress = warn <= .0001f ? 1f : Mathf.Clamp01(time / warn);
                if (!state.TimingFired && time >= warn) { state.TimingFired = true; Emit(trace, "on_release", frame, time, 0, state.Position, "telegraph_complete"); }
            }
            else if (type == "delay_fuse")
            {
                var fuse = Mathf.Max(0f, (float)request.Get(request.Timing, "fuse_time", 1d)); state.Stage = time < fuse ? 0 : 1;
                state.Progress = fuse <= .0001f ? 1f : Mathf.Clamp01(time / fuse);
                state.Width = 1f + state.Progress * (request.Get(request.Timing, "blink_accelerate", 0d) > .5d ? 6f : 0f);
                if (!state.TimingFired && time >= fuse) { state.TimingFired = true; Emit(trace, "on_release", frame, time, 0, state.Position, "fuse_complete"); }
            }
            else if (type == "tick_pulse")
            {
                var interval = Mathf.Max(.01f, (float)request.Get(request.Timing, "tick_interval", .5d)); var expected = Mathf.FloorToInt((time + .00001f) / interval);
                state.Progress = Mathf.Repeat(time, interval) / interval;
                while (state.TickCount < expected) { state.TickCount++; Emit(trace, "on_tick", frame, time, state.TickCount, state.Position, "tick_visual_slot"); }
            }
            else if (type == "charge_release" || type == "charge_scale")
            {
                if (!state.TerminalFired)
                {
                    var first = (float)request.Get(request.Timing, "level_1", duration * .25f); var second = (float)request.Get(request.Timing, "level_2", duration * .55f); var next = time < first ? 0 : time < second ? 1 : 2;
                    if (next != state.Stage) { state.Stage = next; Emit(trace, "on_charge_level", frame, time, next, state.Position, string.Empty); }
                    var levelScale = type == "charge_release" ? request.Get(request.Timing, "per_level_scale", 1.6d) : request.Get(request.Timing, "per_level_width", 1.6d);
                    state.Width = Mathf.Pow(Mathf.Max(1f, (float)levelScale), state.Stage);
                    state.Progress = Mathf.Clamp01(time / Mathf.Max(.01f, second));
                    var release = request.ReleaseTime >= 0f ? request.ReleaseTime : duration;
                    if (type == "charge_release" && request.CancelTime >= 0f && time >= request.CancelTime)
                    {
                        state.TerminalFired = true;
                        Emit(trace, "on_cancel", frame, time, state.Stage + 1, state.Position, "charge_cancel");
                    }
                    else if (type == "charge_release" && time >= release)
                    {
                        state.TerminalFired = true;
                        state.TimingFired = true;
                        Emit(trace, "on_release", frame, time, state.Stage + 1, state.Position, "charge_release");
                    }
                }
            }
            else if (type == "channel_interrupt")
            {
                var channel = Mathf.Max(.01f, (float)request.Get(request.Timing, "channel_time", duration));
                state.Progress = Mathf.Clamp01(time / channel);
                if (!state.TerminalFired && request.CancelTime >= 0f && time >= request.CancelTime) { state.TerminalFired = true; state.Stage = 2; Emit(trace, "on_cancel", frame, time, 0, state.Position, "channel_interrupted"); }
                else if (!state.TerminalFired && time >= channel) { state.TerminalFired = true; state.Stage = 1; Emit(trace, "on_complete", frame, time, 0, state.Position, "channel_complete"); }
            }
            else if (type == "chain_sequence")
            {
                var interval = Mathf.Max(.01f, (float)request.Get(request.Timing, "interval", .25d)); var count = Mathf.Max(1, (int)request.Get(request.Timing, "count", 4d)); var expected = Mathf.Min(count, Mathf.FloorToInt((time + .00001f) / interval));
                while (state.TickCount < expected) { state.TickCount++; Emit(trace, "on_hit", frame, time, state.TickCount, state.Position + SafeDirection(request.Direction) * state.TickCount, "chain_sequence"); }
                state.Progress = state.TickCount / (float)count;
            }
            else if (type == "hitscan")
            {
                if (!state.TimingFired) { state.TimingFired = true; state.Endpoint = request.Target; Emit(trace, "on_hit", frame, time, 1, request.Target, "hitscan"); }
                var linger = Mathf.Clamp((float)request.Get(request.Timing, "linger", .15d), .1f, .2f);
                state.Progress = Mathf.Clamp01(time / linger);
                state.Width = 1f - state.Progress;
            }
            else if (type == "sustained")
            {
                state.Width = 1f;
                // Sweep owns its trace endpoint. A sustained lifetime must not overwrite the
                // bounded angular/inertial motion calculated by StepMotion.
                if (request.MotionType != "sweep") state.Endpoint = request.Target + request.TargetVelocity * time;
            }
        }

        private static void ApplySpatial(CapabilitySampleRequest request, CapabilitySampleTrace trace, State state, int frame, float time)
        {
            var type = request.MotionType ?? "linear";
            if (type == "expand_ring")
            {
                var max = Mathf.Max(.01f, (float)request.Get(request.Motion, "max_radius", 5d));
                state.Progress = Mathf.Clamp01(state.Radius / max);
                if (!state.SpatialFired && state.Progress >= .5f) { state.SpatialFired = true; Emit(trace, "on_hit", frame, time, 1, state.Position + Vector3.right * state.Radius, "expanding_edge"); }
            }
            else if (type == "implode")
            {
                var collapse = Mathf.Max(.01f, (float)request.Get(request.Motion, "collapse_time", 1d));
                state.Progress = Mathf.Clamp01(time / collapse);
                if (time < collapse) state.Stage = 0;
                else if (time < collapse + .1f) state.Stage = 1;
                else if (!state.SpatialFired) { state.Stage = 2; state.SpatialFired = true; Emit(trace, "on_release", frame, time, 0, state.Position, "implode_burst"); }
            }
            else if (type == "moving_zone")
            {
                state.Progress = Mathf.Clamp01(time / Mathf.Max(.01f, request.Duration));
                if (!state.TerminalFired && request.CancelTime >= 0f && time >= request.CancelTime)
                {
                    state.TerminalFired = true;
                    Emit(trace, "on_cancel", frame, time, 0, state.Position, "moving_zone_cancel");
                }
                else if (!state.TerminalFired && time >= request.Duration)
                {
                    state.TerminalFired = true;
                    Emit(trace, "on_complete", frame, time, 0, state.Position, "moving_zone_complete");
                }
            }
            else if (type == "growth_stage" && state.Stage != state.LastReportedStage)
            {
                state.LastReportedStage = state.Stage;
                Emit(trace, "on_stage", frame, time, state.Stage + 1, state.Position, "growth_stage");
            }
        }

        private static void ApplyHit(CapabilitySampleRequest request, CapabilitySampleTrace trace, State state, int frame, float time, float duration)
        {
            if (state.PendingBounce)
            {
                state.PendingBounce = false;
                EmitDirectional(trace, "on_bounce", frame, state.BounceTime, state.BounceCount, state.Position, "bounce", state.BounceBefore, state.BounceAfter);
            }
            var type = request.HitType ?? "single";
            if (type == "pierce")
            {
                var max = Mathf.Max(1, (int)request.Get(request.Hit, "max_hits", 3d)); var spacing = duration / (max + 1);
                while (state.HitCount < max && time + .00001f >= (state.HitCount + 1) * spacing) { state.HitCount++; Emit(trace, "on_hit", frame, time, state.HitCount, state.Position, "pierce"); state.Velocity *= 1f - Mathf.Clamp01((float)request.Get(request.Hit, "damping_per_hit", .2d)); }
            }
            else if (type == "split" && !state.HitFired && time >= duration * .5f)
            {
                state.HitFired = true;
                var count = Mathf.Max(2, (int)request.Get(request.Hit, "child_count", 3d));
                var spread = Mathf.Deg2Rad * Mathf.Clamp((float)request.Get(request.Hit, "split_angle", 60d), 0f, 360f);
                for (var i = 0; i < count; i++)
                {
                    var ratio = count == 1 ? .5f : i / (float)(count - 1);
                    var angle = Mathf.Lerp(-spread * .5f, spread * .5f, ratio);
                    var direction = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward) * SafeDirection(state.Velocity);
                    EmitDirectional(trace, "on_split", frame, time, i + 1, state.Position, "split", Vector3.zero, direction);
                }
            }
            else if (type == "chain_hop")
            {
                var count = Mathf.Max(1, (int)request.Get(request.Hit, "hop_count", 3d));
                var sampleDt = Mathf.Clamp(request.DeltaTime, 1f / 1000f, .25f);
                var firstHopTime = Mathf.Max(sampleDt, duration * .35f);
                var interval = ChainHopInterval(duration, firstHopTime, count, sampleDt);
                var damping = Mathf.Clamp01((float)request.Get(request.Hit, "damping", .15d));
                while (state.HitCount < count)
                {
                    var scheduled = firstHopTime + state.HitCount * interval;
                    if (time + .00001f < scheduled) break;
                    var sequence = state.HitCount + 1;
                    var target = ChainHopTarget(request, state.ChainAnchor, sequence, count);
                    var previous = sequence == 1 ? state.ChainAnchor : ChainHopTarget(request, state.ChainAnchor, sequence - 1, count);
                    var direction = SafeDirection(target - previous);
                    var beforeEnergy = Mathf.Pow(1f - damping, state.HitCount);
                    var afterEnergy = beforeEnergy * (1f - damping);
                    EmitDirectional(trace, "on_hit", frame, scheduled, sequence, target, type, direction * beforeEnergy, direction * afterEnergy);
                    state.HitCount++;
                }
                state.HitFired = state.HitCount >= count;
            }
            else if (type == "arc_link")
            {
                var count = Mathf.Max(1, (int)request.Get(request.Hit, "hop_count", 3d));
                var firstHopTime = Mathf.Max(Mathf.Clamp(request.DeltaTime, 1f / 1000f, .25f), duration * .25f);
                var interval = Mathf.Max(Mathf.Clamp(request.DeltaTime, 1f / 1000f, .25f), duration * .12f);
                var direction = SafeDirection(request.Target - request.Origin);
                var lateral = Vector3.Cross(Vector3.forward, direction);
                if (lateral.sqrMagnitude < .000001f) lateral = Vector3.up;
                lateral.Normalize();
                var range = Mathf.Max(.1f, Vector3.Distance(request.Origin, request.Target));
                while (state.HitCount < count)
                {
                    var scheduled = firstHopTime + state.HitCount * interval;
                    if (time + .00001f < scheduled) break;
                    var sequence = state.HitCount + 1;
                    var position = request.Origin + direction * (range * sequence / count) + lateral * (sequence % 2 == 0 ? -.3f : .3f);
                    var previous = sequence == 1 ? request.Origin : request.Origin + direction * (range * (sequence - 1) / count) + lateral * ((sequence - 1) % 2 == 0 ? -.3f : .3f);
                    EmitDirectional(trace, "on_hit", frame, scheduled, sequence, position, type, SafeDirection(position - previous), SafeDirection(position - previous));
                    state.HitCount++;
                }
                state.HitFired = state.HitCount >= count;
            }
            else if (type == "reflect")
            {
                var max = Mathf.Max(1, (int)request.Get(request.Hit, "max_segments", 3d)); var spacing = duration / (max + 1);
                while (state.HitCount < max && time + .00001f >= (state.HitCount + 1) * spacing)
                {
                    state.HitCount++;
                    var incoming = state.Velocity;
                    state.Velocity = Vector3.Reflect(incoming, state.HitCount % 2 == 0 ? Vector3.up : Vector3.left) * (1f - Mathf.Clamp01((float)request.Get(request.Hit, "damping_per_bounce", .2d)));
                    EmitDirectional(trace, "on_bounce", frame, time, state.HitCount, state.Position, "reflect", incoming, state.Velocity);
                }
            }
            else if (type == "occlude")
            {
                var direction = SafeDirection(request.Target - request.Origin);
                var distance = request.ObstacleChangeTime >= 0f && time >= request.ObstacleChangeTime ? request.ObstacleSecondDistance : request.ObstacleDistance;
                state.Endpoint = request.Origin + direction * Mathf.Max(0f, distance);
                state.Position = state.Endpoint;
                if (!state.HitFired) { state.HitFired = true; Emit(trace, "on_hit", frame, time, 1, state.Endpoint, "occluded"); }
            }
        }

        private static void EmitEmissionEvents(CapabilitySampleRequest request, CapabilitySampleTrace trace)
        {
            var type = request.EmissionType ?? "single";
            if (type == "volley_showcase")
            {
                EmitVolleyShowcaseEvents(request, trace);
                return;
            }
            var countKey = type == "converge" ? "source_count" : "count";
            var count = Mathf.Max(1, (int)request.Get(request.Emission, countKey, type == "single" ? 1d : 5d));
            if (type == "fan")
            {
                var spread = Mathf.Clamp((float)request.Get(request.Emission, "spread_angle", 45d), 0f, 360f);
                for (var i = 0; i < count; i++)
                {
                    var ratio = count == 1 ? .5f : i / (float)(count - 1);
                    var direction = Quaternion.AngleAxis(Mathf.Lerp(-spread * .5f, spread * .5f, ratio), Vector3.forward) * SafeDirection(request.Direction);
                    EmitDirectional(trace, "on_emit", 0, 0f, i + 1, request.Origin, type, Vector3.zero, direction);
                }
            }
            else if (type == "ring")
            {
                var radius = Mathf.Max(0f, (float)request.Get(request.Emission, "ring_radius", 0d));
                for (var i = 0; i < count; i++)
                {
                    var angle = Mathf.PI * 2f * i / count;
                    var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    EmitDirectional(trace, "on_emit", 0, 0f, i + 1, request.Origin + direction * radius, type, Vector3.zero, direction);
                }
            }
            else if (type == "converge")
            {
                for (var i = 0; i < count; i++)
                {
                    var angle = Mathf.PI * 2f * i / count;
                    var source = request.Origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    EmitDirectional(trace, "on_emit", 0, 0f, i + 1, source, type, Vector3.zero, SafeDirection(request.Target - source));
                }
            }
            else if (type == "burst_stagger") { var stagger = Mathf.Max(0f, (float)request.Get(request.Emission, "stagger", .08d)); for (var i = 0; i < count; i++) EmitDirectional(trace, "on_emit", Mathf.RoundToInt(i * stagger / Mathf.Max(.0001f, request.DeltaTime)), i * stagger, i + 1, request.Origin, type, Vector3.zero, SafeDirection(request.Direction)); }
        }

        private static void EmitVolleyShowcaseEvents(CapabilitySampleRequest request, CapabilitySampleTrace trace)
        {
            var phaseDuration = Mathf.Max(request.DeltaTime, (float)request.Get(request.Emission, "phase_duration", request.Duration / 3f));
            var fanCount = Mathf.Clamp((int)request.Get(request.Emission, "fan_count", 5d), 1, 24);
            var fanSpread = Mathf.Clamp((float)request.Get(request.Emission, "fan_spread_angle", 50d), 0f, 360f);
            for (var i = 0; i < fanCount; i++)
            {
                var ratio = fanCount == 1 ? .5f : i / (float)(fanCount - 1);
                var direction = Quaternion.AngleAxis(Mathf.Lerp(-fanSpread * .5f, fanSpread * .5f, ratio), Vector3.forward) * SafeDirection(request.Direction);
                EmitDirectional(trace, "on_emit", 0, 0f, i + 1, request.Origin, "fan", Vector3.zero, direction);
            }

            var burstCount = Mathf.Clamp((int)request.Get(request.Emission, "burst_count", 5d), 1, 24);
            var stagger = Mathf.Max(0f, (float)request.Get(request.Emission, "burst_stagger", .08d));
            for (var i = 0; i < burstCount; i++)
            {
                var eventTime = phaseDuration + i * stagger;
                EmitDirectional(trace, "on_emit", Mathf.RoundToInt(eventTime / Mathf.Max(.0001f, request.DeltaTime)), eventTime, i + 1, request.Origin, "burst_stagger", Vector3.zero, SafeDirection(request.Direction));
            }

            var ringCount = Mathf.Clamp((int)request.Get(request.Emission, "ring_count", 8d), 1, 24);
            var ringRadius = Mathf.Max(0f, (float)request.Get(request.Emission, "ring_radius", .45d));
            var ringTime = phaseDuration * 2f;
            for (var i = 0; i < ringCount; i++)
            {
                var angle = Mathf.PI * 2f * i / ringCount;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                EmitDirectional(trace, "on_emit", Mathf.RoundToInt(ringTime / Mathf.Max(.0001f, request.DeltaTime)), ringTime, i + 1, request.Origin + direction * ringRadius, "ring", Vector3.zero, direction);
            }
        }

        private static void Emit(CapabilitySampleTrace trace, string type, int frame, float time, int sequence, Vector3 position, string detail)
        {
            trace.Events.Add(new CapabilitySampleEvent { Type = type, Frame = frame, Time = time, Sequence = sequence, Position = position, Detail = detail ?? string.Empty });
        }

        private static void EmitDirectional(CapabilitySampleTrace trace, string type, int frame, float time, int sequence, Vector3 position, string detail, Vector3 before, Vector3 after)
        {
            trace.Events.Add(new CapabilitySampleEvent { Type = type, Frame = frame, Time = time, Sequence = sequence, Position = position, Detail = detail ?? string.Empty, Before = before, After = after });
        }

        private static Vector3 SafeDirection(Vector3 value) { return value.sqrMagnitude < .000001f ? Vector3.right : value.normalized; }

        private static Vector3 EvaluateExternalPath(Vector3[] points, float progress)
        {
            if (points == null || points.Length == 0) return Vector3.zero;
            if (points.Length == 1) return points[0];
            var scaled = Mathf.Clamp01(progress) * (points.Length - 1);
            var index = Mathf.Min(points.Length - 2, Mathf.FloorToInt(scaled));
            var local = scaled - index;
            local = local * local * (3f - 2f * local);
            return Vector3.Lerp(points[index], points[index + 1], local);
        }

        private static bool IsBeam(CapabilitySampleRequest request)
        {
            return request.MotionType == "sweep" || request.HitType == "reflect" || request.HitType == "occlude" || request.HitType == "arc_link" || request.EmissionType == "converge" || request.TimingType == "hitscan" || request.TimingType == "sustained" || request.TimingType == "charge_scale";
        }

        private sealed class State
        {
            public Vector3 Position, LastPosition, Velocity, Endpoint, BounceBefore, BounceAfter, ChainAnchor; public float Radius, Width = 1f, BounceTime, Progress; public int Stage, TickCount, HitCount, BounceCount, LastReportedStage = -1; public bool TimingFired, HitFired, PendingBounce, TerminalFired, SpatialFired, ChainStarted; public DeterministicRandom Random;
        }

        private sealed class DeterministicRandom
        {
            private uint state;
            public DeterministicRandom(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
            public float Next01() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state & 0x00FFFFFFu) / 16777216f; }
        }
    }
}
