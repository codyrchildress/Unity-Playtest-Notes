using UnityEngine;

namespace PlaytestNotes.Samples
{
    /// <summary>Keeps the camera behind and above the demo player.</summary>
    public sealed class DemoFollowCamera : MonoBehaviour
    {
        private static readonly Vector3 Offset = new Vector3(0f, 6f, -9f);

        public Transform Target { get; set; }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            transform.position = Target.position + Offset;
            transform.LookAt(Target.position + Vector3.up);
        }
    }
}
