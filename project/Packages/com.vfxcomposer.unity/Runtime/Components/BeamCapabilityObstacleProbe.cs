using System;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>
    /// Explicit, bounded obstacle source for an occluding beam. Runtime entries never search
    /// the scene and an occluding beam fails closed until a configured probe is supplied.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeamCapabilityObstacleProbe : MonoBehaviour
    {
        [SerializeField] private Collider[] blockers = new Collider[0];

        public int ConfiguredBlockerCount
        {
            get
            {
                var count = 0;
                if (blockers == null) return count;
                for (var i = 0; i < blockers.Length; i++)
                    if (blockers[i] != null) count++;
                return count;
            }
        }

        public bool IsConfigured { get { return ConfiguredBlockerCount > 0; } }

        public void SetBlockers(params Collider[] values)
        {
            if (values == null)
            {
                blockers = new Collider[0];
                return;
            }
            blockers = new Collider[values.Length];
            Array.Copy(values, blockers, values.Length);
        }

        public bool TryGetFirstBlocker(Vector3 source, Vector3 target, out Vector3 hitPoint, out Collider blocker)
        {
            hitPoint = target;
            blocker = null;
            if (!IsConfigured) return false;

            var delta = target - source;
            var length = delta.magnitude;
            if (length <= .000001f) return false;
            // Unity 2022.3 does not guarantee that a Transform-driven collider has reached the
            // physics broad phase before the next FixedUpdate. Occlusion is contractually allowed
            // at most two visual frames of response latency, so synchronize once before reading
            // the explicitly bounded blocker set instead of accepting stale bounds.
            Physics.SyncTransforms();
            var ray = new Ray(source, delta / length);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < blockers.Length; i++)
            {
                var candidate = blockers[i];
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                float distance;
                if (!candidate.bounds.IntersectRay(ray, out distance)) continue;
                if (distance < 0f || distance > length + .0001f || distance >= nearest) continue;
                nearest = distance;
                blocker = candidate;
            }
            if (blocker == null) return false;
            hitPoint = ray.GetPoint(nearest);
            return true;
        }
    }
}
