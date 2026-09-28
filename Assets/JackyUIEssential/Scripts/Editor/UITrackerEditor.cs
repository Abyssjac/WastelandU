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
        private SerializedProperty _scrollMenuSlidingAreaImage;
        private SerializedProperty _scrollMenuHandleImage;
        private SerializedProperty _tabBackgroundImage;
        private SerializedProperty _tabSelectedImage;
        private SerializedProperty _toggleBackgroundImage;
        private SerializedProperty _toggleCheckmarkImage;
        private SerializedProperty _progressBarBackgroundImage;
        private SerializedProperty _progressBarFillImage;
        private SerializedProperty _sliderBackgroundImage;
        private SerializedProperty _sliderFillImage;
        private SerializedProperty _sliderHandleImage;

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
            _scrollMenuSlidingAreaImage = serializedObject.FindProperty("_scrollMenuSlidingAreaImage");
            _scrollMenuHandleImage = serializedObject.FindProperty("_scrollMenuHandleImage");
            _tabBackgroundImage = serializedObject.FindProperty("_tabBackgroundImage");
            _tabSelectedImage = serializedObject.FindProperty("_tabSelectedImage");
            _toggleBackgroundImage = serializedObject.FindProperty("_toggleBackgroundImage");
            _toggleCheckmarkImage = serializedObject.FindProperty("_toggleCheckmarkImage");
            _progressBarBackgroundImage = serializedObject.FindProperty("_progressBarBackgroundImage");
            _progressBarFillImage = serializedObject.FindProperty("_progressBarFillImage");
            _sliderBackgroundImage = serializedObject.FindProperty("_sliderBackgroundImage");
            _sliderFillImage = serializedObject.FindProperty("_sliderFillImage");
            _sliderHandleImage = serializedObject.FindProperty("_sliderHandleImage");
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
                    EditorGUILayout.PropertyField(_scrollMenuSlidingAreaImage, new GUIContent("Scroll Menu Sliding Area Image"));
                    EditorGUILayout.PropertyField(_scrollMenuHandleImage, new GUIContent("Scroll Menu Handle Image"));
                    break;

                case CustomUIComponentType.Tab:
                    EditorGUILayout.PropertyField(_tabBackgroundImage, new GUIContent("Tab Background Image"));
                    EditorGUILayout.PropertyField(_tabSelectedImage, new GUIContent("Tab Selected Image"));
                    break;

                case CustomUIComponentType.Toggle:
                    EditorGUILayout.PropertyField(_toggleBackgroundImage, new GUIContent("Toggle Background Image"));
                    EditorGUILayout.PropertyField(_toggleCheckmarkImage, new GUIContent("Toggle Checkmark Image"));
                    break;

                case CustomUIComponentType.ProgressBar:
                    EditorGUILayout.PropertyField(_progressBarBackgroundImage, new GUIContent("Progress Bar Background Image"));
                    EditorGUILayout.PropertyField(_progressBarFillImage, new GUIContent("Progress Bar Fill Image"));
                    break;

                case CustomUIComponentType.Slider:
                    EditorGUILayout.PropertyField(_sliderBackgroundImage, new GUIContent("Slider Background Image"));
                    EditorGUILayout.PropertyField(_sliderFillImage, new GUIContent("Slider Fill Image"));
                    EditorGUILayout.PropertyField(_sliderHandleImage, new GUIContent("Slider Handle Image"));
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
