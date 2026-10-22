using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// The settings the Editor tools use: the first PlaytestNotesSettings asset in the project (by path), or the defaults.
    /// </summary>
    internal static class EditorSettingsLocator
    {
        private static PlaytestNotesSettings defaults;

        public static string ProjectDirectory => Path.GetDirectoryName(Application.dataPath);

        public static PlaytestNotesSettings Load()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(PlaytestNotesSettings));
            string first = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (first == null || string.CompareOrdinal(path, first) < 0)
                {
                    first = path;
                }
            }

            if (first != null)
            {
                return AssetDatabase.LoadAssetAtPath<PlaytestNotesSettings>(first);
            }

            if (defaults == null)
            {
                defaults = ScriptableObject.CreateInstance<PlaytestNotesSettings>();
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }

            return defaults;
        }

        public static NoteStore CreateStore()
        {
            return new NoteStore(NoteStore.ResolveRoot(Load().SaveRoot));
        }

        public static string ResolveProjectPath(string path)
        {
            return Path.GetFullPath(Path.Combine(ProjectDirectory, path ?? string.Empty));
        }
    }
}
