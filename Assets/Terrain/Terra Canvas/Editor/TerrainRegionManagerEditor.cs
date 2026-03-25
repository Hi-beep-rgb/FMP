using UnityEngine;
using UnityEditor;

namespace CodyDreams.Solutions.TerraCanvas
{
    [CustomEditor(typeof(TerrainRegionManager))]
    public class TerrainRegionManagerEditor : Editor
    {
        private const float k_HandleVerticalOffset = 0f;
        private const float k_MinWidth = 0.01f;
        private const float k_MaxWidth = 1000f;

        private void OnSceneGUI()
        {
            TerrainRegionManager manager = (TerrainRegionManager)target;
            // Check for manager existence and ensure it's enabled.
            if (manager == null || !manager.enabled) return;

            // --- 1. HANDLE ROADS (Existing Logic) ---
            DrawRoadHandles(manager);

            // --- 2. HANDLE POCKETS (New Logic) ---
            DrawPocketHandles(manager);

            // Ensure Repaint happens when something changes
            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawRoadHandles(TerrainRegionManager manager)
        {
            if (manager.roads == null) return;

            for (int r = 0; r < manager.roads.Length; r++)
            {
                if (manager.roads[r].points == null) continue;

                for (int i = 0; i < manager.roads[r].points.Length; i++)
                {
                    Vector3 stored = manager.roads[r].points[i];
                    // x=worldX, y=height, z=worldZ
                    Vector3 worldPos = new Vector3(stored.x, manager.roads[r].highetFloat, stored.y);

                    // Draw core & buffer discs
                    Handles.color = new Color(0f, 1f, 0f, 0.6f);
                    float coreRadius = Mathf.Max(0.0001f, stored.z * 0.5f);
                    Handles.DrawWireDisc(worldPos, Vector3.up, coreRadius);

                    Handles.color = new Color(1f, 0f, 0f, 0.25f);
                    Handles.DrawWireDisc(worldPos, Vector3.up, coreRadius + manager.roads[r].BufferWidth);

                    // --- ROAD POSITION HANDLING ---
                    if (Tools.current != Tool.Scale)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(manager, "Move Road Point");
                            // Store back X, Z position and original Width (stored.z)
                            manager.roads[r].points[i] = new Vector3(newWorldPos.x, newWorldPos.z, stored.z);
                            EditorUtility.SetDirty(manager);
                        }
                    }

                    // --- ROAD SCALE HANDLING ---
                    if (Tools.current == Tool.Scale)
                    {
                        // Existing scale logic... (using 3-axis ScaleHandle to adjust stored.z width)
                        float handleSize = HandleUtility.GetHandleSize(worldPos);
                        Vector3 handlePos = worldPos + Vector3.up * k_HandleVerticalOffset;
                        Vector3 currentScaleVec = Vector3.one * Mathf.Max(stored.z, k_MinWidth);

                        EditorGUI.BeginChangeCheck();
                        Vector3 newScaleVec =
                            Handles.ScaleHandle(currentScaleVec, handlePos, Quaternion.identity, handleSize);
                        if (EditorGUI.EndChangeCheck())
                        {
                            float factor = (currentScaleVec.x != 0f) ? newScaleVec.x / currentScaleVec.x : 1f;
                            factor = Mathf.Clamp(factor, 0.1f, 10f);
                            float newWidth = Mathf.Clamp(stored.z * factor, k_MinWidth, k_MaxWidth);

                            Undo.RecordObject(manager, "Scale Road Point Width (3-axis)");
                            manager.roads[r].points[i] = new Vector3(stored.x, stored.y, newWidth);
                            EditorUtility.SetDirty(manager);
                            stored.z = newWidth; // Update stored for label drawing
                        }

                        // Draw scale label
                        Handles.BeginGUI();
                        Vector2 guiPos = HandleUtility.WorldToGUIPoint(handlePos + Vector3.up * (handleSize * 1.6f));
                        GUIStyle style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
                        GUI.Box(new Rect(guiPos.x - 40, guiPos.y - 12, 80, 22), $"W: {stored.z:F2}", style);
                        Handles.EndGUI();
                    }
                    else
                    {
                        // Draw position label
                        Handles.BeginGUI();
                        Vector2 guiPos =
                            HandleUtility.WorldToGUIPoint(worldPos + Vector3.up * (k_HandleVerticalOffset * 0.6f));
                        GUI.Label(new Rect(guiPos.x - 40, guiPos.y - 10, 80, 20), $"W: {stored.z:F2}");
                        Handles.EndGUI();
                    }
                }
            }
        }

        private void DrawPocketHandles(TerrainRegionManager manager)
        {
            if (manager.manualPockets == null) return;

            // Ensure the array is mutable since struct fields are being modified
            ManualPocket[] pockets = manager.manualPockets;

            for (int p = 0; p < pockets.Length; p++)
            {
                // Use local copy of the struct for mutation before writing back
                ManualPocket pocket = pockets[p];

                // World position from pocket.WorldPosition (X, Y(Height), Z)
                Vector3 worldPos = pocket.WorldPosition;

                // Calculate total size for visualization (Core + Buffer)
                Vector3 totalSize = new Vector3(
                    pocket.Size.x + pocket.BufferSize.x * 2,
                    0,
                    pocket.Size.y + pocket.BufferSize.y * 2
                );

                // --- POCKET GIZMOS (Rectangular visualization) ---

                // Core Size (Yellow)
                Handles.color = new Color(1f, 1f, 0f, 0.4f);
                Handles.DrawWireCube(worldPos, new Vector3(pocket.Size.x, 0, pocket.Size.y));

                // Buffer Size (Orange)
                Handles.color = new Color(1f, 0.5f, 0f, 0.25f);
                Handles.DrawWireCube(worldPos, totalSize);

                // Height Line (To show the height of the pocket)
                Handles.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                Handles.DrawLine(worldPos + Vector3.up * 0.01f, worldPos + Vector3.up * (pocket.Height + 0.01f));


                // --- POCKET POSITION HANDLING ---
                if (Tools.current != Tool.Scale)
                {
                    EditorGUI.BeginChangeCheck();
                    // PositionHandle draws the standard 3-axis arrows
                    Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(manager, "Move Manual Pocket");
                        // Update the WorldPosition field
                        pocket.WorldPosition = newWorldPos;
                        pockets[p] = pocket; // Write back the modified struct
                        EditorUtility.SetDirty(manager);
                    }
                }

                // --- POCKET SCALE HANDLING ---
                if (Tools.current == Tool.Scale)
                {
                    float handleSize = HandleUtility.GetHandleSize(worldPos);
                    Vector3 handlePos = worldPos;

                    // Use the combined size for the scale handle visual feedback
                    Vector3 currentSizeVec = new Vector3(pocket.Size.x, pocket.Height, pocket.Size.y);

                    EditorGUI.BeginChangeCheck();
                    // ScaleHandle allows proportional (center) or axis-specific scaling
                    Vector3 newSizeVec =
                        Handles.ScaleHandle(currentSizeVec, handlePos, Quaternion.identity, handleSize);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(manager, "Scale Manual Pocket Size");

                        // The ScaleHandle gives us the new dimensions directly
                        pocket.Size = new Vector2(
                            Mathf.Clamp(newSizeVec.x, k_MinWidth, k_MaxWidth),
                            Mathf.Clamp(newSizeVec.z, k_MinWidth, k_MaxWidth) // Z component is used for Y-size
                        );

                        // You might want to let the user adjust the height (Y component) too!
                        pocket.Height = newSizeVec.y;

                        pockets[p] = pocket; // Write back the modified struct
                        EditorUtility.SetDirty(manager);
                    }

                    // Draw numeric label for size
                    Handles.BeginGUI();
                    Vector2 guiPos = HandleUtility.WorldToGUIPoint(handlePos + Vector3.up * (handleSize * 1.6f));
                    GUIStyle style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
                    GUI.Box(new Rect(guiPos.x - 40, guiPos.y - 12, 120, 22),
                        $"X: {pocket.Size.x:F2} | Z: {pocket.Size.y:F2}", style);
                    Handles.EndGUI();
                }
            }

            // Write the modified array back to the manager field
            manager.manualPockets = pockets;
        }
    }
}