using UnityEngine;
using UnityEditor;

namespace JackyUtility
{
    /// <summary>
    /// Generic base class for live debug windows in the Unity Editor.
    /// Automatically locates the target MonoBehaviour during Play Mode,
    /// keeps the window refreshed every editor tick, and provides
    /// common layout helpers for subclasses.
    ///
    /// Usage:
    ///   public class FooDebugWindow : DebugEditorWindow&lt;FooComponent&gt;
    ///   {
    ///       [MenuItem("Wasteland Debug/Foo")]
    ///       public static void ShowWindow() =&gt;
    ///           GetWindow&lt;FooDebugWindow&gt;("Foo").Show();
    ///
    ///       protected override void DrawContent() { /* EditorGUILayout here */ }
    ///   }
    /// </summary>
    public abstract class DebugEditorWindow<T> : EditorWindow where T : MonoBehaviour
    {
        private T _target;
        private Vector2 _scrollPos;

        // ©¤©¤ Target resolution ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

        /// <summary>
        /// The runtime target. Resolved lazily via FindObjectOfType during Play Mode.
        /// Returns null when not playing or if no instance exists in the scene.
        /// </summary>
        protected T Target
        {
            get
            {
                if (!Application.isPlaying)
                {
                    _target = null;
                    return null;
                }

                if (_target == null)
                    _target = FindFirstObjectByType<T>();

                return _target;
            }
        }

        // ©¤©¤ Unity callbacks ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

        /// <summary>Request repaint every editor update so data stays live.</summary>
        private void Update()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to view live debug data.",
                    MessageType.Info);
                return;
            }

            if (Target == null)
            {
                EditorGUILayout.HelpBox(
                    $"No {typeof(T).Name} found in the active scene.",
                    MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawContent();
            EditorGUILayout.EndScrollView();
        }

        // ©¤©¤ Abstract ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

        /// <summary>
        /// Implement to draw the window content with EditorGUILayout.
        /// Called only when Application.isPlaying and Target != null.
        /// </summary>
        protected abstract void DrawContent();

        // ©¤©¤ Layout helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

        /// <summary>Bold section header followed by a separator line.</summary>
        protected static void Header(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawSeparator();
        }

        /// <summary>Two-column label row, identical to the Inspector style.</summary>
        protected static void Row(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        /// <summary>Two-column label row with a custom text colour.</summary>
        protected static void ColoredRow(string label, string value, Color color)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(label, value, EditorStyles.boldLabel);
            GUI.contentColor = prev;
        }

        /// <summary>Draws a thin horizontal separator line.</summary>
        protected static void DrawSeparator()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space(2);
        }
    }
}
