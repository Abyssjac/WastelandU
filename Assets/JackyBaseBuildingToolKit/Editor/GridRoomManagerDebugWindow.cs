using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JackyUtility
{
    /// <summary>
    /// Live debug window for <see cref="GridRoomManager"/>.
    /// Open via  Wasteland Debug ¡ú Grid Room Manager  in the Unity menu bar.
    ///
    /// Usage:
    ///   1. Enter Play Mode.
    ///   2. Type a Room ID in the text field and press "Find Room".
    ///   3. The detail panel refreshes in real-time for the selected room.
    /// </summary>
    public class GridRoomManagerDebugWindow : DebugEditorWindow<GridRoomManager>
    {
        [MenuItem("Wasteland Debug/Grid Room Manager")]
        public static void ShowWindow() =>
            GetWindow<GridRoomManagerDebugWindow>("Grid Room Manager").Show();

        private string _roomIdInput    = "";
        private int    _selectedRoomId = -1;

        protected override void DrawContent()
        {
            var rm = Target;

            // ©¤©¤ Overview ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            Header("Overview");
            Row("Active Rooms", rm.ActiveRooms.Count.ToString());
            Row("Policy",       rm.Editor_PolicyType.ToString());

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Force Recalculate", GUILayout.Height(22)))
                rm.Editor_ForceRecalculate();

            // ©¤©¤ Room search ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            Header("Room Inspector");

            EditorGUILayout.BeginHorizontal();
            _roomIdInput = EditorGUILayout.TextField("Room ID", _roomIdInput);
            if (GUILayout.Button("Find Room", GUILayout.Width(80)))
            {
                _selectedRoomId = int.TryParse(_roomIdInput, out int parsed) ? parsed : -1;
            }
            EditorGUILayout.EndHorizontal();

            if (_selectedRoomId < 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Enter a Room ID above and press Find Room.", MessageType.None);
                return;
            }

            // ©¤©¤ Room detail (live refresh every frame) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            RoomData room = rm.GetRoomById(_selectedRoomId);

            if (room == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"No room found with ID {_selectedRoomId}.", MessageType.Warning);
                return;
            }

            DrawSeparator();
            Row("Room ID",    room.RoomId.ToString());
            Row("Stable ID",  room.StableId.ToString());
            Row("Cell Count", room.CellCount.ToString());

            // Furniture tags
            Header("Furniture Tags");
            if (room.TagCounts.Count == 0)
            {
                EditorGUILayout.LabelField("No furniture tags present.");
            }
            else
            {
                foreach (KeyValuePair<FurnitureTag, int> kvp in room.TagCounts)
                    Row(kvp.Key.ToString(), kvp.Value.ToString());
            }

            // First 20 cells
            Header("Cells (first 20)");
            int shown = 0;
            foreach (var cell in room.Cells)
            {
                EditorGUILayout.LabelField(cell.ToString());
                if (++shown >= 20) break;
            }
            if (room.CellCount > 20)
                EditorGUILayout.LabelField(
                    $"... and {room.CellCount - 20} more",
                    EditorStyles.miniLabel);
        }
    }
}
