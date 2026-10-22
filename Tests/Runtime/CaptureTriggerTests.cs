using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace PlaytestNotes.Tests
{
    /// <summary>
    /// The default Capture triggers, driven through simulated devices. Each test ends the capture in the frame it
    /// starts, so the microphone never opens and no screenshot is taken.
    /// In a [UnityTest] the fixture only queues events and the player loop processes them, and each queued gamepad
    /// event is a full state snapshot: two presses on one device need a frame between them, or the second hides the first.
    /// </summary>
    public sealed class CaptureTriggerTests : InputTestFixture
    {
        private PlaytestNotesHost host;

        public override void Setup()
        {
            base.Setup();
            host = new GameObject("Playtest Notes Test Host").AddComponent<PlaytestNotesHost>();
        }

        public override void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host.gameObject);
            }

            base.TearDown();
        }

        [Test]
        public void CaptureBindingPerformsOnlyAfterShouldersHeldForASecond()
        {
            var settings = ScriptableObject.CreateInstance<PlaytestNotesSettings>();
            try
            {
                InputAction capture = settings.CaptureAction;
                capture.Enable();
                settings.StopAction.Enable();
                settings.NextCategoryAction.Enable();
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

                Press(gamepad.leftShoulder);
                Press(gamepad.rightShoulder);
                AdvanceTimeAndUpdate(0.5);
                Assert.That(capture.WasPerformedThisFrame(), Is.False, "Performed before the hold time.");

                AdvanceTimeAndUpdate(0.6);
                Assert.That(capture.WasPerformedThisFrame(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [UnityTest]
        public IEnumerator F8StartsCapture()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;

            Press(keyboard.f8Key);
            yield return null;

            Assert.That(EndAndReportCapturing(), Is.True);
        }

        [UnityTest]
        public IEnumerator ShouldersHeldForASecondStartCapture([Values(true, false)] bool rightFirst)
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            Press(rightFirst ? gamepad.rightShoulder : gamepad.leftShoulder);
            yield return null;
            Press(rightFirst ? gamepad.leftShoulder : gamepad.rightShoulder);
            double start = currentTime;
            for (int frame = 0; frame < 40 && !host.IsCapturing; frame++)
            {
                currentTime += 0.05;
                yield return null;
            }

            double held = currentTime - start;
            Assert.That(EndAndReportCapturing(), Is.True);
            Assert.That(held, Is.GreaterThanOrEqualTo(1.0).And.LessThan(1.3), "Capture should start once both shoulders are held for a second.");
        }

        [UnityTest]
        public IEnumerator ShortShoulderPressDoesNotCapture()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            Press(gamepad.leftShoulder);
            yield return null;
            Press(gamepad.rightShoulder);
            currentTime += 0.4;
            yield return null;
            Release(gamepad.rightShoulder);
            yield return null;
            Release(gamepad.leftShoulder);
            for (int frame = 0; frame < 30; frame++)
            {
                currentTime += 0.05;
                yield return null;
            }

            Assert.That(EndAndReportCapturing(), Is.False);
        }

        [UnityTest]
        public IEnumerator RightShoulderAloneDoesNotCapture()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            Press(gamepad.rightShoulder);
            for (int frame = 0; frame < 30; frame++)
            {
                currentTime += 0.05;
                yield return null;
            }

            Assert.That(EndAndReportCapturing(), Is.False);
        }

        [UnityTest]
        public IEnumerator RenderingDebuggerChordDoesNotCapture()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;

            Press(gamepad.rightStickButton);
            yield return null;
            Press(gamepad.leftStickButton);
            yield return null;

            Assert.That(EndAndReportCapturing(), Is.False);
        }

        [UnityTest]
        public IEnumerator ThreeFingerHoldStartsCaptureAfterASecond()
        {
            InputSystem.AddDevice<Touchscreen>();
            yield return null;

            BeginTouch(1, new Vector2(100f, 100f));
            BeginTouch(2, new Vector2(300f, 100f));
            BeginTouch(3, new Vector2(500f, 100f));
            float start = Time.realtimeSinceStartup;
            while (!host.IsCapturing && Time.realtimeSinceStartup - start < 2f)
            {
                yield return null;
            }

            float held = Time.realtimeSinceStartup - start;
            Assert.That(EndAndReportCapturing(), Is.True);
            Assert.That(held, Is.GreaterThan(0.9f), "The capture started before the hold time.");
        }

        [UnityTest]
        public IEnumerator ThreeFingerDoubleTapDoesNotCapture()
        {
            InputSystem.AddDevice<Touchscreen>();
            yield return null;

            for (int tap = 0; tap < 2; tap++)
            {
                BeginTouch(1, new Vector2(100f, 100f));
                BeginTouch(2, new Vector2(300f, 100f));
                BeginTouch(3, new Vector2(500f, 100f));
                yield return null;
                EndTouch(1, new Vector2(100f, 100f));
                EndTouch(2, new Vector2(300f, 100f));
                EndTouch(3, new Vector2(500f, 100f));
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1.2f);

            Assert.That(EndAndReportCapturing(), Is.False);
        }

        // Only for [Test]s, where the fixture processes input when told to rather than once per frame.
        private void AdvanceTimeAndUpdate(double seconds)
        {
            currentTime += seconds;
            InputSystem.Update();
        }

        // Reads the state, then destroys the host in the same frame: the capture ends before its end-of-frame
        // screenshot and before the microphone opens.
        private bool EndAndReportCapturing()
        {
            bool capturing = host.IsCapturing;
            Object.DestroyImmediate(host.gameObject);
            host = null;
            return capturing;
        }
    }
}
