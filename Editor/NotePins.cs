using UnityEditor;
using UnityEngine;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// Draws a pin in the Scene view at each note's tracked-target position, for notes captured in an open scene.
    /// Clicking a pin frames the Scene view on the camera pose the note was captured with.
    /// </summary>
    [InitializeOnLoad]
    internal static class NotePins
    {
        private const string MenuPath = "Playtest Notes/Show Scene Pins";
        private const string EnabledKey = "PlaytestNotes.ShowScenePins";
        private const string ShowResolvedKey = "PlaytestNotes.ShowResolved";

        static NotePins()
        {
            SceneView.duringSceneGui += OnSceneGui;
            NoteLibrary.Changed += SceneView.RepaintAll;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>Shared with the notes window: resolved notes are hidden in both when this is off.</summary>
        public static bool ShowResolved
        {
            get => EditorPrefs.GetBool(ShowResolvedKey, true);
            set
            {
                EditorPrefs.SetBool(ShowResolvedKey, value);
                SceneView.RepaintAll();
            }
        }

        public static Color ColorFor(NoteCategory category)
        {
            switch (category)
            {
                case NoteCategory.Bug:
                    return new Color(0.95f, 0.3f, 0.3f);
                case NoteCategory.Idea:
                    return new Color(0.35f, 0.7f, 1f);
                case NoteCategory.Polish:
                    return new Color(0.95f, 0.78f, 0.25f);
                case NoteCategory.Other:
                    return new Color(0.7f, 0.5f, 0.95f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        [MenuItem(MenuPath, priority = 20)]
        private static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (!Enabled)
            {
                return;
            }

            bool showResolved = ShowResolved;
            foreach (NoteRecord note in NoteLibrary.Notes)
            {
                NoteData data = note.Data;
                if (!data.HasTarget || (data.Resolved && !showResolved) || !NoteFraming.IsSceneOpen(data))
                {
                    continue;
                }

                Vector3 foot = data.TargetPosition;
                float size = HandleUtility.GetHandleSize(foot) * 0.12f;
                Vector3 head = foot + (Vector3.up * size * 4f);
                Color color = ColorFor(data.Category);
                if (data.Resolved)
                {
                    color.a = 0.35f;
                }

                Handles.color = color;
                Handles.DrawLine(foot, head);
                if (Handles.Button(head, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                {
                    NoteFraming.Frame(note);
                }

                Handles.Label(head + (Vector3.up * size * 1.5f), "#" + note.Number + " " + data.Category, EditorStyles.whiteMiniLabel);
            }
        }
    }
}
