using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlaytestNotes.Editor
{
    /// <summary>
    /// The notes on disk as the Editor window and Scene view pins see them, with small cached thumbnails.
    /// Reloads when Play Mode ends, since that is when new notes appear.
    /// </summary>
    [InitializeOnLoad]
    internal static class NoteLibrary
    {
        private const int ThumbnailWidth = 256;

        private static readonly Dictionary<string, Texture2D> Thumbnails = new Dictionary<string, Texture2D>();
        private static List<NoteRecord> notes;
        private static NoteStore store;

        static NoteLibrary()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode)
                {
                    Reload();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += ClearThumbnails;
        }

        public static event Action Changed;

        public static NoteStore Store
        {
            get
            {
                if (store == null)
                {
                    store = EditorSettingsLocator.CreateStore();
                }

                return store;
            }
        }

        public static IReadOnlyList<NoteRecord> Notes
        {
            get
            {
                if (notes == null)
                {
                    Reload();
                }

                return notes;
            }
        }

        public static void Reload()
        {
            store = EditorSettingsLocator.CreateStore();
            notes = store.LoadAll();
            ClearThumbnails();
            Changed?.Invoke();
        }

        /// <summary>Saves an edited note.json, rebuilds index.md and tells the views.</summary>
        public static void Save(NoteRecord note)
        {
            Store.WriteData(note);
            Store.RebuildIndex();
            Changed?.Invoke();
        }

        public static Texture2D GetThumbnail(NoteRecord note)
        {
            if (!note.HasScreenshot)
            {
                return null;
            }

            if (Thumbnails.TryGetValue(note.Folder, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D thumbnail = LoadThumbnail(note.ScreenshotPath);
            Thumbnails[note.Folder] = thumbnail;
            return thumbnail;
        }

        private static Texture2D LoadThumbnail(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var full = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!full.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(full);
                return null;
            }

            int width = Mathf.Min(ThumbnailWidth, full.width);
            int height = Mathf.Max(1, Mathf.RoundToInt(width * full.height / (float)full.width));
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(full, target);
            RenderTexture.active = target;
            var thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            thumbnail.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(full);
            return thumbnail;
        }

        private static void ClearThumbnails()
        {
            foreach (Texture2D texture in Thumbnails.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            Thumbnails.Clear();
        }
    }
}
