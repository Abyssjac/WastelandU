using AbyssToolKitUnity.Utility;
using UnityEditor;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Draws only the target Image relevant to the UITracker's selected type.
    /// </summary>
    [CustomEditor(typeof(UITracker))]
    public sealed class UITrackerEditor : UnityEditor.Editor
    {
        private SerializedProperty _isTracking;
        private SerializedProperty _type;
        private SerializedProperty _buttonImage;
        private SerializedProperty _button;
        private SerializedProperty _panelImage;
        private SerializedProperty _slotImage;
        private SerializedProperty _slotButton;
        private SerializedProperty _scrollMenuPanelImage;

        private void OnEnable()
        {
            _isTracking = serializedObject.FindProperty("_isTracking");
            _type = serializedObject.FindProperty("_type");
            _buttonImage = serializedObject.FindProperty("_buttonImage");
            _button = serializedObject.FindProperty("_button");
            _panelImage = serializedObject.FindProperty("_panelImage");
            _slotImage = serializedObject.FindProperty("_slotImage");
            _slotButton = serializedObject.FindProperty("_slotButton");
            _scrollMenuPanelImage = serializedObject.FindProperty("_scrollMenuPanelImage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((UITracker)target), typeof(UITracker), false);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(_isTracking);
            EditorGUILayout.PropertyField(_type);

            CustomUIComponentType type = (CustomUIComponentType)_type.enumValueIndex;
            switch (type)
            {
                case CustomUIComponentType.Button:
                    EditorGUILayout.PropertyField(_buttonImage, new GUIContent("Button Image"));
                    EditorGUILayout.PropertyField(_button, new GUIContent("Button Component"));
                    break;

                case CustomUIComponentType.Panel:
                    EditorGUILayout.PropertyField(_panelImage, new GUIContent("Panel Image"));
                    break;

                case CustomUIComponentType.Slot:
                    EditorGUILayout.PropertyField(_slotImage, new GUIContent("Slot Image"));
                    EditorGUILayout.PropertyField(_slotButton, new GUIContent("Slot Button"));
                    break;

                case CustomUIComponentType.ScrollMenu:
                    EditorGUILayout.PropertyField(_scrollMenuPanelImage, new GUIContent("Scroll Menu Panel Image"));
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
