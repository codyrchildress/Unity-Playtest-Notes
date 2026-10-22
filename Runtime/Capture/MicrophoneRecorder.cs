using UnityEngine;

namespace PlaytestNotes
{
    /// <summary>Records the default microphone into a non-looping clip and hands back the recorded part.</summary>
    internal sealed class MicrophoneRecorder
    {
        // Whisper works at 16 kHz; recording at that rate keeps files small and skips resampling.
        private const int PreferredFrequency = 16000;

        // Grace period before a stopped device (unplugged headset) counts as the end of the recording.
        private const float StartupSeconds = 0.5f;

        private AudioClip clip;
        private float startTime;

        public bool IsRecording => clip != null;

        public float Elapsed => clip != null ? Time.realtimeSinceStartup - startTime : 0f;

        public bool ReachedLimit => clip != null
            && (Elapsed >= clip.length || (Elapsed > StartupSeconds && !Microphone.IsRecording(null)));

        /// <summary>Returns false when there is no microphone or it refused to start.</summary>
        public bool Start(int maxSeconds)
        {
            if (clip != null || Microphone.devices.Length == 0)
            {
                return false;
            }

            Microphone.GetDeviceCaps(null, out int minFrequency, out int maxFrequency);
            int frequency = minFrequency == 0 && maxFrequency == 0
                ? PreferredFrequency
                : Mathf.Clamp(PreferredFrequency, minFrequency, maxFrequency);
            clip = Microphone.Start(null, false, maxSeconds, frequency);
            startTime = Time.realtimeSinceStartup;
            return clip != null;
        }

        /// <summary>Stops and returns what was recorded, or null when nothing was.</summary>
        public RecordedAudio Stop()
        {
            if (clip == null)
            {
                return null;
            }

            bool stillRecording = Microphone.IsRecording(null);
            int position = Microphone.GetPosition(null);
            Microphone.End(null);

            // A non-looping clip that filled up stops recording and its position is no longer meaningful.
            int frames = stillRecording ? position : clip.samples;
            RecordedAudio audio = null;
            if (frames > 0)
            {
                var samples = new float[frames * clip.channels];
                clip.GetData(samples, 0);
                audio = new RecordedAudio(samples, clip.frequency, clip.channels);
            }

            Discard();
            return audio;
        }

        public void Cancel()
        {
            if (clip == null)
            {
                return;
            }

            Microphone.End(null);
            Discard();
        }

        private void Discard()
        {
            Object.Destroy(clip);
            clip = null;
        }
    }
}
