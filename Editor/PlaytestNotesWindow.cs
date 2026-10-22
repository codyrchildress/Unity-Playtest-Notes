using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// Lists the notes newest first: screenshot, category, transcript and a resolved toggle, with buttons to frame
    /// the Scene view on a note, play its audio, open its folder or delete it.
    /// </summary>
    internal sealed class PlaytestNotesWindow : EditorWindow
    {
        private const float ThumbnailWidth = 192f;
        private const float ThumbnailHeight = 108f;

        private Vector2 scroll;
        private GUIStyle transcriptStyle;

        [MenuItem("Playtest Notes/Notes Window", priority = 0)]
        public static void Open()
        {
            GetWindow<PlaytestNotesWindow>("Playtest Notes");
        }

        [MenuItem("Playtest Notes/Rebuild Index", priority = 2)]
        private static void RebuildIndex()
        {
            NoteLibrary.Store.RebuildIndex();
            NoteLibrary.Reload();
            Debug.Log("[Playtest Notes] Rebuilt " + NoteLibrary.Store.IndexPath);
        }

        [MenuItem("Playtest Notes/Open Notes Folder", priority = 3)]
        private static void OpenNotesFolder()
        {
            Directory.CreateDirectory(NoteLibrary.Store.NotesDirectory);
            EditorUtility.RevealInFinder(NoteLibrary.Store.NotesDirectory);
        }

        [MenuItem("GameObject/Playtest Notes Host", false, 10)]
        private static void CreateHost(MenuCommand command)
        {
            var host = new GameObject("Playtest Notes", typeof(PlaytestNotesHost));
            GameObjectUtility.SetParentAndAlign(host, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(host, "Create Playtest Notes Host");
            Selection.activeGameObject = host;
        }

        private void OnEnable()
        {
            NoteLibrary.Changed += Repaint;
        }

        private void OnDisable()
        {
            NoteLibrary.Changed -= Repaint;
        }

        private void OnGUI()
        {
            transcriptStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };
            DrawToolbar();

            var notes = NoteLibrary.Notes;
            if (notes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No notes in " + NoteLibrary.Store.NotesDirectory + ".\n\nAdd a Playtest Notes Host to a scene (GameObject > Playtest Notes Host), "
                    + "enter Play Mode and press F8 (gamepad: hold LB + RB for a second; touch: hold three fingers for a second).",
                    MessageType.Info);
                return;
            }

            bool showResolved = NotePins.ShowResolved;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                if (notes[i].Data.Resolved && !showResolved)
                {
                    continue;
                }

                DrawNote(notes[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    NoteLibrary.Reload();
                }

                int pending = 0;
                int open = 0;
                foreach (NoteRecord note in NoteLibrary.Notes)
                {
                    pending += note.IsTranscriptionPending ? 1 : 0;
                    open += note.Data.Resolved ? 0 : 1;
                }

                using (new EditorGUI.DisabledScope(pending == 0 || TranscriptionRunner.IsRunning))
                {
                    if (GUILayout.Button("Transcribe pending (" + pending + ")", EditorStyles.toolbarButton))
                    {
                        TranscriptionRunner.TranscribePending();
                    }
                }

                if (GUILayout.Button("Rebuild index", EditorStyles.toolbarButton))
                {
                    RebuildIndex();
                }

                if (GUILayout.Button("Open folder", EditorStyles.toolbarButton))
                {
                    OpenNotesFolder();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(open + " open / " + NoteLibrary.Notes.Count, EditorStyles.miniLabel);
                NotePins.ShowResolved = GUILayout.Toggle(NotePins.ShowResolved, "Show resolved", EditorStyles.toolbarButton);
                NotePins.Enabled = GUILayout.Toggle(NotePins.Enabled, "Scene pins", EditorStyles.toolbarButton);
            }
        }

        private void DrawNote(NoteRecord note)
        {
            NoteData data = note.Data;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect thumbnailRect = GUILayoutUtility.GetRect(ThumbnailWidth, ThumbnailHeight, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));
                Texture2D thumbnail = NoteLibrary.GetThumbnail(note);
                if (thumbnail != null)
                {
                    GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit);
                    if (GUI.Button(thumbnailRect, new GUIContent(string.Empty, "Open the screenshot"), GUIStyle.none))
                    {
                        EditorUtility.OpenWithDefaultApp(note.ScreenshotPath);
                    }
                }
                else
                {
                    GUI.Box(thumbnailRect, "No screenshot");
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Color previous = GUI.color;
                        GUI.color = NotePins.ColorFor(data.Category);
                        GUILayout.Label("#" + note.Number, EditorStyles.boldLabel, GUILayout.Width(40f));
                        GUI.color = previous;
                        GUILayout.Label(
                            data.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "  ·  " + data.SceneName + "  ·  " + data.Platform,
                            EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        EditorGUI.BeginChangeCheck();
                        var category = (NoteCategory)EditorGUILayout.EnumPopup(data.Category, GUILayout.Width(110f));
                        bool resolved = GUILayout.Toggle(data.Resolved, "Resolved", GUILayout.Width(80f));
                        if (EditorGUI.EndChangeCheck())
                        {
                            data.Category = category;
                            data.Resolved = resolved;
                            NoteLibrary.Save(note);
                        }
                    }

                    GUILayout.Label(TranscriptText(note), transcriptStyle);
                    if (data.Metadata.Count > 0)
                    {
                        GUILayout.Label(MetadataText(data), EditorStyles.miniLabel);
                    }

                    GUILayout.FlexibleSpace();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!data.HasCamera && !data.HasTarget))
                        {
                            if (GUILayout.Button("Frame in Scene", GUILayout.Width(110f)))
                            {
                                NoteFraming.Frame(note);
                            }
                        }

                        using (new EditorGUI.DisabledScope(!note.HasAudio))
                        {
                            if (GUILayout.Button("Play audio", GUILayout.Width(90f)))
                            {
                                EditorUtility.OpenWithDefaultApp(note.AudioPath);
                            }
                        }

                        if (GUILayout.Button("Show folder", GUILayout.Width(90f)))
                        {
                            EditorUtility.RevealInFinder(Path.Combine(note.Folder, NoteStore.DataFile));
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Delete", GUILayout.Width(70f))
                            && EditorUtility.DisplayDialog("Delete note #" + note.Number, "Delete " + note.Folder + " and everything in it?", "Delete", "Cancel"))
                        {
                            NoteLibrary.Store.Delete(note);
                            NoteLibrary.Store.RebuildIndex();
                            NoteLibrary.Reload();

                            // The list just changed under the loop drawing it.
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }

        private static string TranscriptText(NoteRecord note)
        {
            if (!note.HasAudio)
            {
                return "(no audio)";
            }

            if (note.Transcript == null)
            {
                return "(not transcribed yet: " + note.Data.AudioSeconds.ToString("0.0") + " s of audio)";
            }

            string text = note.Transcript.Trim();
            return text.Length > 0 ? text : "(no speech detected)";
        }

        private static string MetadataText(NoteData data)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var pair in data.Metadata)
            {
                builder.Append(builder.Length > 0 ? "   " : string.Empty).Append(pair.Key).Append(" = ").Append(pair.Value);
            }

            return builder.ToString();
        }
    }
}
