using System.Collections;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Small player-safe demonstration driver. It contains no game hit, damage, or targeting logic.</summary>
    [DisallowMultipleComponent]
    public sealed class VfxPreviewSequenceDriver : MonoBehaviour
    {
        [SerializeField] private GeneratedVfxController controller;
        [SerializeField, Min(0f)] private float launchDuration = .12f;
        [SerializeField, Min(.01f)] private float travelDuration = 1f;
        private Coroutine sequence;

        public void PlayFullSequence(Vector3 start, Vector3 end)
        {
            if (controller == null) controller = GetComponent<GeneratedVfxController>();
            if (controller == null) return;
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(Play(start, end));
        }

        public void StopSequence()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
        }

        private IEnumerator Play(Vector3 start, Vector3 end)
        {
            controller.ResetForPool();
            controller.SetTravelTransform(start, Quaternion.identity);
            controller.PlayLaunch();
            yield return new WaitForSeconds(launchDuration);
            controller.StartTravel();
            var elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                controller.SetTravelTransform(Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / travelDuration)), Quaternion.identity);
                yield return null;
            }
            controller.PlayImpact(end);
            sequence = null;
        }
    }
}
