using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlaytestNotes.Samples
{
    /// <summary>
    /// A capsule you walk with WASD, the arrow keys or the left stick. It shows both extension points:
    /// it is the note's tracked target (IPlaytestTarget) and it adds its own state to every note (IPlaytestMetadataProvider).
    /// </summary>
    public sealed class DemoPlayer : MonoBehaviour, IPlaytestTarget, IPlaytestMetadataProvider
    {
        [SerializeField]
        private float speed = 4f;

        private float distanceWalked;

        public Pose PlaytestPose => new Pose(transform.position, transform.rotation);

        public void CollectPlaytestMetadata(IDictionary<string, string> metadata)
        {
            metadata["demo.distanceWalked"] = distanceWalked.ToString("0.0", CultureInfo.InvariantCulture);
            metadata["demo.secondsPlayed"] = Time.timeSinceLevelLoad.ToString("0", CultureInfo.InvariantCulture);
        }

        private void Awake()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.up;
            Destroy(body.GetComponent<Collider>());

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(body.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            Destroy(nose.GetComponent<Collider>());
        }

        private void Update()
        {
            Vector2 move = ReadMove();
            Vector3 step = new Vector3(move.x, 0f, move.y) * (speed * Time.deltaTime);
            if (step.sqrMagnitude > 0f)
            {
                transform.position += step;
                transform.rotation = Quaternion.LookRotation(step);
                distanceWalked += step.magnitude;
            }
        }

        private static Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                move.x += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                move.y += (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }

            if (Gamepad.current != null)
            {
                move += Gamepad.current.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(move, 1f);
        }
    }
}
