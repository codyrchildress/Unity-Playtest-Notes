using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlaytestNotes.Editor
{
    /// <summary>Puts the Scene view camera where the game camera was when a note was captured.</summary>
    internal static class NoteFraming
    {
        public static bool IsSceneOpen(NoteData data)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                bool match = string.IsNullOrEmpty(data.ScenePath)
                    ? scene.name == data.SceneName
                    : scene.path == data.ScenePath;
                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Frame(NoteRecord note)
        {
            NoteData data = note.Data;
            if (!data.HasCamera && !data.HasTarget)
            {
                return;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode && !IsSceneOpen(data) && !string.IsNullOrEmpty(data.ScenePath)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(data.ScenePath) != null)
            {
                if (EditorUtility.DisplayDialog("Playtest Notes", "Note #" + note.Number + " was captured in " + data.ScenePath + ". Open it?", "Open", "Stay")
                    && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(data.ScenePath);
                }
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                view = EditorWindow.GetWindow<SceneView>();
            }

            Vector3 position = data.HasCamera ? data.CameraPosition : data.TargetPosition + (data.TargetRotation * new Vector3(0f, 1.5f, -4f));
            Quaternion rotation = data.HasCamera ? data.CameraRotation : Quaternion.LookRotation(data.TargetPosition - position);
            if (data.HasCamera)
            {
                view.orthographic = data.CameraOrthographic;
                if (!data.CameraOrthographic)
                {
                    view.cameraSettings.fieldOfView = data.CameraFieldOfView;
                }
            }

            var marker = new GameObject("Playtest Note Camera") { hideFlags = HideFlags.HideAndDontSave };
            marker.transform.SetPositionAndRotation(position, rotation);
            view.AlignViewToObject(marker.transform);
            Object.DestroyImmediate(marker);
            view.Repaint();
            view.Focus();
        }
    }
}
