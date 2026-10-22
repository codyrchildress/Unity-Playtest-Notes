using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// Local speech-to-text with whisper.unity (whisper.cpp). Compiled only when the com.whisper.unity package is
    /// installed. The model is loaded on the first note and reused for the rest of the run.
    /// </summary>
    internal sealed class WhisperTranscriber : ITranscriber
    {
        private readonly string modelPath;
        private readonly string language;
        private WhisperWrapper whisper;

        public WhisperTranscriber()
        {
            PlaytestNotesSettings settings = EditorSettingsLocator.Load();
            modelPath = EditorSettingsLocator.ResolveProjectPath(settings.WhisperModelPath);
            language = settings.WhisperLanguage;
        }

        public string DisplayName => "whisper.unity (" + Path.GetFileName(modelPath) + ")";

        public async Task<string> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
        {
            if (whisper == null)
            {
                if (!File.Exists(modelPath))
                {
                    throw new FileNotFoundException(
                        "No Whisper model at " + modelPath + ". Download a ggml model (for example ggml-base.en.bin) and set its path in the Playtest Notes settings asset.",
                        modelPath);
                }

                whisper = await WhisperWrapper.InitFromFileAsync(modelPath);
                if (whisper == null)
                {
                    throw new InvalidOperationException("whisper.unity could not load the model at " + modelPath + ".");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            RecordedAudio audio = WavFile.Decode(File.ReadAllBytes(wavPath));
            WhisperParams parameters = WhisperParams.GetDefaultParams();
            parameters.Language = language;

            // whisper.unity mixes to mono and resamples to 16 kHz itself.
            WhisperResult result = await whisper.GetTextAsync(audio.Samples, audio.Frequency, audio.Channels, parameters);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null)
            {
                throw new InvalidOperationException("whisper.unity returned no result for " + wavPath + ".");
            }

            return result.Result.Trim();
        }

        public void Dispose()
        {
            // WhisperWrapper has no Dispose; its finalizer frees the native context once nothing references it.
            whisper = null;
        }
    }
}
