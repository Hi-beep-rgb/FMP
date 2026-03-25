using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CodyDreams.Solutions.TerraCanvas
{
    [CustomEditor(typeof(TerraCanvas))]
    public class TerraCanvasEditor : Editor
    {
        private SerializedProperty _terrainPipeline;
        private SerializedProperty _generationPasses;

        private const float BOX_HEIGHT = 22.0f;
        private const float ICON_SIZE = 21.0f;
        private static GUIStyle _headerStyle;
        private static GUIStyle _boxStyle;

        private Dictionary<GenerationStep, Texture2D> _passIcons;

        private void OnEnable()
        {
            _terrainPipeline = serializedObject.FindProperty("terrainPipeline");

            // Initialize GUI styles for better visuals
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _boxStyle = new GUIStyle("HelpBox")
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(0, 0, 2, 2)
            };

            // Load or create a dictionary of icons for each step
            _passIcons = new Dictionary<GenerationStep, Texture2D>
            {
                { GenerationStep.GenerateHeightmap, EditorIconLoader.GetTextureByName("challenge-icon") },
                { GenerationStep.PaintTerrain, EditorIconLoader.GetTextureByName("color-wheel-icon") },
                { GenerationStep.GenerateTrees, EditorIconLoader.GetTextureByName("trees-icon") },
                { GenerationStep.GenerateDetail, EditorIconLoader.GetTextureByName("leaves-icon") },
                { GenerationStep.Randomize, EditorIconLoader.GetTextureByName("shuffle-button-green-icon") },
                { GenerationStep.SaveBackup, EditorIconLoader.GetTextureByName("verified-symbol-icon") },
                { GenerationStep.LoadBackup, EditorIconLoader.GetTextureByName("verified-symbol-icon") },
                { GenerationStep.None, EditorGUIUtility.FindTexture("d_console.warnicon.sml") }
            };
        }

        public override void OnInspectorGUI()
        {
            // Draw the default inspector properties
            DrawDefaultInspector();
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _boxStyle = new GUIStyle("HelpBox")
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(0, 0, 2, 2)
            };
            if (_passIcons == null)
            {
                _passIcons = new Dictionary<GenerationStep, Texture2D>
                {
                    { GenerationStep.GenerateHeightmap, EditorIconLoader.GetTextureByName("challenge-icon") },
                    { GenerationStep.PaintTerrain, EditorIconLoader.GetTextureByName("color-wheel-icon") },
                    { GenerationStep.GenerateTrees, EditorIconLoader.GetTextureByName("trees-icon") },
                    { GenerationStep.GenerateDetail, EditorIconLoader.GetTextureByName("leaves-icon") },
                    { GenerationStep.Randomize, EditorIconLoader.GetTextureByName("shuffle-button-green-icon") },
                    { GenerationStep.SaveBackup, EditorIconLoader.GetTextureByName("verified-symbol-icon") },
                    { GenerationStep.LoadBackup, EditorIconLoader.GetTextureByName("verified-symbol-icon") },
                    { GenerationStep.None, EditorGUIUtility.FindTexture("d_console.warnicon.sml") }
                };
            }

            // Check if the pipeline asset is assigned
            if (_terrainPipeline.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Please assign a 'TerraCanvasPasses' ScriptableObject to visualize the pipeline.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);

            // Start the custom visualization
            GUILayout.Label("Terrain Generation Pipeline Order", _headerStyle);

            // Use a SerializedObject for the pipeline asset
            SerializedObject pipelineObject = new SerializedObject(_terrainPipeline.objectReferenceValue);
            _generationPasses = pipelineObject.FindProperty("generationPasses");

            // Check if the property exists
            if (_generationPasses == null)
            {
                EditorGUILayout.HelpBox(
                    "The 'generationPasses' list was not found in the pipeline asset. Check the ScriptableObject.",
                    MessageType.Error);
                return;
            }

            for (int i = 0; i < _generationPasses.arraySize; i++)
            {
                SerializedProperty passProperty = _generationPasses.GetArrayElementAtIndex(i);

                // Start the box for a single step
                GUILayout.BeginVertical(_boxStyle);

                // Get the current step
                GenerationStep currentStep = (GenerationStep)passProperty.enumValueIndex;

                // Begin a horizontal group for icon and text
                GUILayout.BeginHorizontal();

// Draw an icon if one exists
                Texture2D iconTexture = null;
                if (_passIcons.TryGetValue(currentStep, out iconTexture) && iconTexture != null)
                {
                    GUILayout.Label(new GUIContent(iconTexture), GUILayout.Width(ICON_SIZE),
                        GUILayout.Height(ICON_SIZE));
                }
                else
                {
                    // Fallback for missing icon
                    GUILayout.Space(ICON_SIZE);
                }

                // Draw the step name and a reorderable handle
                EditorGUILayout.LabelField($"Step {i + 1}: {currentStep.ToString()}", EditorStyles.boldLabel);

                // End horizontal group
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            // Apply changes to the serialized object
            pipelineObject.ApplyModifiedProperties();
        }
    }

    public static class EditorIconLoader
    {
        public static Texture2D GetTextureByName(string textureName)
        {
            // Search for all assets of type Texture2D that match the name
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D {textureName}");

            if (guids.Length == 0)
            {
                Debug.LogError($"Texture '{textureName}' not found in the project.");
                return null;
            }

            // Get the path from the first found GUID
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);

            // Load and return the texture
            Texture2D final = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (final == null) return null;
            return final;
        }
    }
}