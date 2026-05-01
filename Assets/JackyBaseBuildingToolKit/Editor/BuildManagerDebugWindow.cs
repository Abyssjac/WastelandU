using UnityEngine;
using UnityEditor;

namespace JackyUtility
{
    /// <summary>
    /// Live debug window for <see cref="BuildManager"/>.
    /// Open via  Wasteland Debug ? Build Manager  in the Unity menu bar.
    /// </summary>
    public class BuildManagerDebugWindow : DebugEditorWindow<BuildManager>
    {
        [MenuItem("Wasteland Debug/Build Manager")]
        public static void ShowWindow() =>
            GetWindow<BuildManagerDebugWindow>("Build Manager").Show();

        // ©¤©¤ Foldout state ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        private bool _foldSelection = true;
        private bool _foldRaycast   = true;
        private bool _foldPlacement = true;
        private bool _foldGrid      = true;

        // ©¤©¤ Content ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        protected override void DrawContent()
        {
            var bm = Target;

            // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            Header("State");

            Color stateColor =
                bm.CurrentState == BuildState.Inactive         ? Color.gray    :
                bm.CurrentState == BuildState.Idle             ? Color.white   :
                bm.CurrentState == BuildState.Placing          ? Color.cyan    :
                bm.CurrentState == BuildState.PlacingBlueprint ? Color.magenta :
                                                                  Color.yellow;

            ColoredRow("Build State",   bm.CurrentState.ToString(), stateColor);
            Row("Mode Active",          bm.IsBuildModeActive.ToString());

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Toggle Build Mode", GUILayout.Height(22)))
                bm.ToggleBuildMode();

            // ©¤©¤ Current Selection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            _foldSelection = EditorGUILayout.Foldout(_foldSelection, "Current Selection", true);
            if (_foldSelection)
            {
                EditorGUI.indentLevel++;
                Row("Selected Property",
                    bm.Editor_SelectedProperty  != null
                        ? bm.Editor_SelectedProperty.EnumKey.ToString()  : "¡ª");
                Row("Selected Blueprint",
                    bm.Editor_SelectedBlueprint != null
                        ? bm.Editor_SelectedBlueprint.EnumKey.ToString() : "¡ª");
                Row("Moving Data",
                    bm.Editor_MovingData        != null
                        ? bm.Editor_MovingData.Property.EnumKey.ToString() : "¡ª");
                Row("Rotation Step",
                    $"{bm.Editor_RotationStep}  ({bm.Editor_RotationStep * 90}¡ã)");
                EditorGUI.indentLevel--;
            }

            // ©¤©¤ Raycast / Position ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            _foldRaycast = EditorGUILayout.Foldout(_foldRaycast, "Raycast / Position", true);
            if (_foldRaycast)
            {
                EditorGUI.indentLevel++;
                var pp = bm.PositionProvider;
                Row("Has Valid Hit", pp.HasValidHit.ToString());
                if (pp.HasValidHit)
                {
                    Row("Cell",           pp.CurrentCell.ToString());
                    Row("Hit World",      pp.CurrentHitWorldPosition.ToString("F3"));
                    Row("Snapped",        pp.CurrentSnappedWorldPosition.ToString("F2"));
                    Row("Snapped Center", pp.CurrentSnappedWorldPositionCenter.ToString("F2"));
                    var hitB = pp.CurrentHitBuildable;
                    Row("Hit Buildable",
                        hitB?.Data != null
                            ? $"{hitB.Data.InstanceId}  ({hitB.Data.Property.EnumKey})" : "¡ª");
                }
                EditorGUI.indentLevel--;
            }

            // ©¤©¤ Placement ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            _foldPlacement = EditorGUILayout.Foldout(_foldPlacement, "Placement", true);
            if (_foldPlacement)
            {
                EditorGUI.indentLevel++;
                Color placeColor = bm.Editor_CanPlace ? Color.green : Color.red;
                ColoredRow("Can Place", bm.Editor_CanPlace.ToString(), placeColor);
                Row("Reason",
                    string.IsNullOrEmpty(bm.Editor_CanPlaceReason)
                        ? "¡ª" : bm.Editor_CanPlaceReason);
                EditorGUI.indentLevel--;
            }

            // ©¤©¤ Grid ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            _foldGrid = EditorGUILayout.Foldout(_foldGrid, "Grid", true);
            if (_foldGrid)
            {
                EditorGUI.indentLevel++;
                Row("Grid Min",       bm.Grid.GridMin.ToString());
                Row("Grid Max",       bm.Grid.GridMax.ToString());
                Row("Placed Objects", bm.Grid.AllPlaced.Count.ToString());
                EditorGUI.indentLevel--;
            }
        }
    }
}
