using UnityEngine;

namespace PlaytestNotes.Samples
{
    /// <summary>
    /// Builds the demo level at runtime from primitives, so the sample renders with whatever pipeline the project
    /// uses (each primitive gets the active pipeline's default material).
    /// </summary>
    public sealed class DemoWorld : MonoBehaviour
    {
        private static readonly Vector3[] PillarPositions =
        {
            new Vector3(-6f, 0f, 4f),
            new Vector3(5f, 0f, 6f),
            new Vector3(8f, 0f, -3f),
            new Vector3(-4f, 0f, -7f),
            new Vector3(0f, 0f, 10f),
        };

        private void Awake()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(transform, false);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            for (int i = 0; i < PillarPositions.Length; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar " + (i + 1);
                pillar.transform.SetParent(transform, false);
                float height = 1f + i;
                pillar.transform.localPosition = PillarPositions[i] + (Vector3.up * (height * 0.5f));
                pillar.transform.localScale = new Vector3(1.5f, height, 1.5f);
            }

            var player = new GameObject("Demo Player");
            player.AddComponent<DemoPlayer>();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<DemoFollowCamera>().Target = player.transform;
        }

        private void OnGUI()
        {
            GUI.Label(
                new Rect(16f, 16f, 900f, 60f),
                "Walk: WASD / left stick.   Capture a note: F8, or hold LB + RB or three fingers for a second.\n"
                + "Then talk, stop (F8 / Enter / A), pick a category and submit. Open Playtest Notes > Notes Window to see it.");
        }
    }
}
