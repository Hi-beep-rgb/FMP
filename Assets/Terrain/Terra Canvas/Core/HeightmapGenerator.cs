using UnityEngine;
using System.IO;
using UnityEditor;

namespace CodyDreams.Solutions.TerraCanvas
{
    public class HeightmapGenerator : MonoBehaviour
    {
        public Terrain terrain;
        public Texture2D heightmapTexture;

        void Awake()
        {
            if (terrain == null)
            {
                terrain = GetComponent<Terrain>();
            }
        }

        // Automatically generates a new Texture2D asset in the project.
        [ContextMenu("Create New Heightmap Texture")]
        public void CreateNewHeightmapTexture()
        {
            if (terrain == null)
            {
                Debug.LogError("Terrain reference is missing. Cannot create a new texture.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            int heightmapRes = terrainData.heightmapResolution;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Heightmap Texture",
                "HeightmapTexture_" + heightmapRes,
                "exr",
                "Please enter a filename for the heightmap texture."
            );

            if (string.IsNullOrEmpty(path)) return;

            Texture2D newTexture = new Texture2D(heightmapRes, heightmapRes, TextureFormat.RFloat, false);
            byte[] bytes = newTexture.EncodeToEXR(Texture2D.EXRFlags.None);
            File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path);

            // Find the newly created asset and assign it.
            Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (asset != null)
            {
                heightmapTexture = asset;
                // It's good practice to set import settings for the newly created texture.
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false; // Important for non-color data
                    importer.anisoLevel = 0;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.isReadable = true; // Crucial for a readable texture
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.crunchedCompression = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }

            Debug.Log("New heightmap texture created at: " + path);
            DestroyImmediate(newTexture);
        }

        [ContextMenu("Generate Heightmap & Save")]
        public void SaveHeightmap()
        {
            if (terrain == null || heightmapTexture == null)
            {
                Debug.LogError("Terrain or Heightmap Texture reference is missing.");
                return;
            }

            // This is a safety check to prevent resolution mismatch.
            if (heightmapTexture.width != terrain.terrainData.heightmapResolution ||
                heightmapTexture.height != terrain.terrainData.heightmapResolution)
            {
                Debug.LogError(
                    "The heightmap texture resolution does not match the terrain's heightmap resolution. Please generate a new texture or resize the existing one.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            int heightmapRes = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, heightmapRes, heightmapRes);

            Texture2D newTexture = new Texture2D(heightmapRes, heightmapRes, TextureFormat.RFloat, false);
            for (int y = 0; y < heightmapRes; y++)
            {
                for (int x = 0; x < heightmapRes; x++)
                {
                    newTexture.SetPixel(x, y, new Color(heights[y, x], 0, 0));
                }
            }

            newTexture.Apply();

            string assetPath = AssetDatabase.GetAssetPath(heightmapTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("The referenced texture must be a saved asset in the project.");
                DestroyImmediate(newTexture);
                return;
            }

            File.WriteAllBytes(assetPath, newTexture.EncodeToEXR(Texture2D.EXRFlags.None));
            AssetDatabase.ImportAsset(assetPath);
            Debug.Log("Heightmap saved successfully to " + assetPath);
            DestroyImmediate(newTexture);
        }

        [ContextMenu("Load Heightmap")]
        public void LoadHeightmap()
        {
            if (terrain == null || heightmapTexture == null)
            {
                Debug.LogError("Terrain or Heightmap Texture reference is missing.");
                return;
            }

            // Safety check for resolution mismatch.
            if (heightmapTexture.width != terrain.terrainData.heightmapResolution ||
                heightmapTexture.height != terrain.terrainData.heightmapResolution)
            {
                Debug.LogError(
                    "The heightmap texture resolution does not match the terrain's heightmap resolution. Please generate a new texture or resize the existing one.");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            int heightmapRes = terrainData.heightmapResolution;
            float[,] newHeights = new float[heightmapRes, heightmapRes];

            try
            {
                Texture2D readableTexture = GetReadableTexture(heightmapTexture);
                if (readableTexture.format != TextureFormat.RFloat)
                {
                    Debug.LogError(
                        "Texture format must be RFloat. Please ensure the texture import settings are correct.");
                    return;
                }

                for (int y = 0; y < heightmapRes; y++)
                {
                    for (int x = 0; x < heightmapRes; x++)
                    {
                        newHeights[y, x] = readableTexture.GetPixel(x, y).r;
                    }
                }

                DestroyImmediate(readableTexture); // Clean up the temporary texture
            }
            catch (UnityException e)
            {
                Debug.LogError(
                    "Failed to read texture. Is 'Read/Write Enabled' in the texture's import settings? Error: " +
                    e.Message);
                return;
            }

            terrainData.SetHeights(0, 0, newHeights);
            Debug.Log("Heightmap loaded successfully from " + AssetDatabase.GetAssetPath(heightmapTexture));
        }

        private Texture2D GetReadableTexture(Texture2D source)
        {
            // ... (your existing GetReadableTexture method)
            RenderTexture renderTex =
                RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.RFloat);
            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableText = new Texture2D(source.width, source.height, TextureFormat.RFloat, false);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }
    }
}