using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// "Playtest Notes > Transcribe Pending Notes": runs the installed <see cref="ITranscriber"/> over every note
    /// with audio and no transcript.txt, as a background task with a cancel button, then rebuilds index.md.
    /// </summary>
    internal static class TranscriptionRunner
    {
        private const string MenuPath = "Playtest Notes/Transcribe Pending Notes";

        public static bool IsRunning { get; private set; }

        [MenuItem(MenuPath, priority = 1)]
        public static void TranscribePending()
        {
            RunAsync();
        }

        [MenuItem(MenuPath, true)]
        private static bool TranscribePendingValidate()
        {
            return !IsRunning;
        }

        private static ITranscriber CreateTranscriber()
        {
            Type chosen = null;
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ITranscriber>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                if (chosen == null || string.CompareOrdinal(type.FullName, chosen.FullName) < 0)
                {
                    chosen = type;
                }
            }

            return chosen != null ? (ITranscriber)Activator.CreateInstance(chosen) : null;
        }

        // async void: a menu command has no caller to return a Task to. Every exception is caught and logged below.
        private static async void RunAsync()
        {
            NoteLibrary.Reload();
            var pending = new List<NoteRecord>();
            foreach (NoteRecord note in NoteLibrary.Notes)
            {
                if (note.IsTranscriptionPending)
                {
                    pending.Add(note);
                }
            }

            if (pending.Count == 0)
            {
                Debug.Log("[Playtest Notes] No notes are waiting for transcription.");
                return;
            }

            ITranscriber transcriber = CreateTranscriber();
            if (transcriber == null)
            {
                EditorUtility.DisplayDialog(
                    "Playtest Notes",
                    "No transcriber is installed.\n\nAdd the whisper.unity package (see the Playtest Notes README) to use the built-in local transcriber, "
                    + "or implement PlaytestNotes.ITranscriber yourself.",
                    "OK");
                return;
            }

            IsRunning = true;
            NoteStore store = NoteLibrary.Store;
            int progressId = Progress.Start("Transcribing playtest notes", transcriber.DisplayName);
            var cancellation = new CancellationTokenSource();
            Progress.RegisterCancelCallback(progressId, () =>
            {
                cancellation.Cancel();
                return true;
            });

            int done = 0;
            Progress.Status outcome = Progress.Status.Succeeded;
            try
            {
                foreach (NoteRecord note in pending)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    Progress.Report(progressId, done, pending.Count, "#" + note.Number + " " + note.FolderName);
                    string transcript = await transcriber.TranscribeAsync(note.AudioPath, cancellation.Token);
                    store.WriteTranscript(note, transcript);
                    done++;
                }
            }
            catch (OperationCanceledException)
            {
                outcome = Progress.Status.Canceled;
            }
            catch (Exception e)
            {
                outcome = Progress.Status.Failed;
                Debug.LogError("[Playtest Notes] Transcription stopped: " + e);
            }
            finally
            {
                transcriber.Dispose();
                cancellation.Dispose();
                store.RebuildIndex();
                IsRunning = false;
                NoteLibrary.Reload();
            }

            Progress.Finish(progressId, outcome);
            Debug.Log("[Playtest Notes] Transcribed " + done + " of " + pending.Count + " notes with " + transcriber.DisplayName + ". Index: " + store.IndexPath);
        }
    }
}
