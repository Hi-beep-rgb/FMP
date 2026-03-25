using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace CodyDreams.Solutions.TerraCanvas
{
    public class RenderTextureTerrainGenerator : MonoBehaviour
    {
        public Terrain terrain;
        public float scale = 20f;
        public float heightMultiplier = 50f;
        public Vector2 offset;

        [FormerlySerializedAs("frameskipperrate")]
        public int rowskipper = 4096;

        public TerrainRegionManager regionManager;

        // Use a RenderTexture instead of a regular Texture2D
        public CustomRenderTexture heightmapRenderTexture;

        // A private array to store the sampled pixel data once.
        private float[,] heightsFromTexture;
        private float[,] originalHeights;

        private void Start()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();

            if (heightmapRenderTexture != null)
            {
                // Start the coroutine to handle the asynchronous update
                StartCoroutine(GenerateTerrainFromTextureCoroutine());
            }
            else
            {
                offset = new Vector2(Random.Range(0f, 9999f), Random.Range(0f, 9999f));
                GenerateTerrain();
            }
        }

        /// <summary>
        /// Generates the terrain heightmap by sampling a RenderTexture
        /// after forcing an update and waiting for the next frame.
        /// </summary>
        public IEnumerator GenerateTerrainFromTextureCoroutine()
        {
            if (heightmapRenderTexture == null)
            {
                Debug.LogError("Heightmap RenderTexture is not assigned!");
                yield break;
            }

            var currenttime = Time.realtimeSinceStartup;
            // Force the CustomRenderTexture to update its contents in the next frame
            heightmapRenderTexture.Update();

            // Wait for one frame so the update can complete
            yield return null;


            // Get the dimensions of the RenderTexture.
            var width = heightmapRenderTexture.width;
            var height = heightmapRenderTexture.height;

            // Read the pixels from the active RenderTexture.
            var request = AsyncGPUReadback.Request(heightmapRenderTexture);
            yield return
                new WaitUntil(() =>
                    request.done); // Cache the WaitUntil if possible, but the delegate closure allocates.
            if (request.hasError)
            {
                Debug.LogError("GPU readback failed!");
                yield break;
            }

            var heightData = request.GetData<float>();
            var flatHeights = new float[heightData.Length];
            heightData.CopyTo(flatHeights);

            heightsFromTexture = new float[width, height];

            yield return null;

            var terrainData = terrain.terrainData;
            var heightmapResolution = terrainData.heightmapResolution;

            var heights = new float[heightmapResolution, heightmapResolution];
            originalHeights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);

// Y loop outside, X loop inside, as required for heights[Y, X]
            for (var y = 0; y < heightmapResolution; y++)
            {
                var worldY = (float)y / (terrainData.heightmapResolution - 1) * terrainData.size.z;
                for (var x = 0; x < heightmapResolution; x++)
                {
                    var worldX = (float)x / (terrainData.heightmapResolution - 1) * terrainData.size.x;
                    Vector2 position = new Vector2(worldX, worldY);
                    var roadInfluence = regionManager.IsInsideRoad(position);
                    var pocketInfluence = regionManager.IsInsidePocket(position);

                    // Lerp from the calculated height to the original height.
                    heights[y, x] = Mathf.Lerp(flatHeights[y * heightmapResolution + x] * heightMultiplier,
                        originalHeights[y, x], pocketInfluence + roadInfluence);
                }

                if (y % rowskipper == 0) yield return null;
            }

            Debug.Log(Time.realtimeSinceStartup - currenttime);

            terrainData.SetHeights(0, 0, heights);

            yield return null;
        }

        /// <summary>
        /// Generates the terrain heightmap using Perlin noise.
        /// </summary>
        public void GenerateTerrain()
        {
            var terrainData = terrain.terrainData;
            var heightmapResolution = terrainData.heightmapResolution;

            var heights = new float[heightmapResolution, heightmapResolution];

            for (var x = 0; x < heightmapResolution; x++)
            for (var y = 0; y < heightmapResolution; y++)
            {
                var height = GetHeight(x, y, true, terrainData.GetHeight(y, x));
                heights[y, x] = height;
            }

            terrainData.SetHeights(0, 0, heights);
        }

        /// <summary>
        /// Calculates and returns the normalized height (0-1) for a given heightmap coordinate.
        /// It can use either Perlin noise or a sampled texture.
        /// </summary>
        /// <param name="x">The X-coordinate in the heightmap array.</param>
        /// <param name="y">The Y-coordinate in the heightmap array.</param>
        /// <param name="usePerlin">If true, uses Perlin noise; otherwise, uses the sampled texture.</param>
        /// <returns>A float representing the normalized height (0 to 1).</returns>
        public float GetHeight(int x, int y, bool usePerlin, float normalhight)
        {
            var terrainData = terrain.terrainData;
            float height;
            float worldX;
            float worldY;
            if (usePerlin)
            {
                // Calculate height using Perlin noise
                worldX = (float)x / (terrainData.heightmapResolution - 1) * terrainData.size.x;
                worldY = (float)y / (terrainData.heightmapResolution - 1) * terrainData.size.z;
                height = Mathf.PerlinNoise(worldX / scale + offset.x, worldY / scale + offset.y);
            }
            else
            {
                // Sample height from the pre-populated array
                //    int texX = (int)Mathf.Lerp(0, heightsFromTexture.GetLength(0) - 1, (float)x / (terrainData.heightmapResolution - 1));
                //     int texY = (int)Mathf.Lerp(0, heightsFromTexture.GetLength(1) - 1, (float)y / (terrainData.heightmapResolution - 1));

                height = heightsFromTexture[x, y];
            }

            // Apply road flattening logic
            worldX = (float)x / (terrainData.heightmapResolution - 1) * terrainData.size.x;
            worldY = (float)y / (terrainData.heightmapResolution - 1) * terrainData.size.z;

            if (regionManager != null)
            {
                var roadInfluence = regionManager.IsInsideRoad(new Vector2(worldX, worldY));
                var pocketInfluence = regionManager.IsInsidePocket(new Vector2(worldX, worldY));
                // Lerp from the calculated height to the original height.
                return Mathf.Lerp(height * heightMultiplier, normalhight, pocketInfluence + roadInfluence);
                ;
            }

            return height * heightMultiplier;
        }
    }
}