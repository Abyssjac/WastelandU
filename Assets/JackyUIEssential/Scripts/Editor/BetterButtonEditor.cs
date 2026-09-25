using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Adds BetterButton's selection policy before Unity's standard Button
    /// Inspector while preserving all built-in Button fields.
    /// </summary>
    [CustomEditor(typeof(BetterButton))]
    [CanEditMultipleObjects]
    public sealed class BetterButtonEditor : ButtonEditor
    {
        private SerializedProperty _selectionBehaviour;

        protected override void OnEnable()
        {
            base.OnEnable();
            _selectionBehaviour = serializedObject.FindProperty("_selectionBehaviour");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_selectionBehaviour, new GUIContent("Selection Behaviour"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
