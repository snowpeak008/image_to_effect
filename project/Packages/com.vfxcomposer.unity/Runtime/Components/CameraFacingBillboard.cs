using UnityEngine;

namespace VFXComposer
{
    /// <summary>Small runtime-only orientation helper used by the protected S10 billboard template layers.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraFacingBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, camera.transform.up);
        }
    }
}
