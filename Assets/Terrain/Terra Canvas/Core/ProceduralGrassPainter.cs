using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CodyDreams.Solutions.TerraCanvas
{
    [ExecuteInEditMode]
    public class ProceduralGrassPainter : MonoBehaviour
    {
        [System.Serializable]
        public class GrassRule
        {
            [Tooltip("Index of detail prototype in Terrain settings")]
            public int[] detailLayerIndexs = { 0 };

            public int[] ExclusionRuleindexes = { };
            [Range(0, 1)] public float density = 0.5f; // Grass density (0–16 typical range)
            [Range(0, 100)] public int spawnChance = 100; // % chance to spawn

            [Header("Height Filter")] public float minHeight = 0f; // World height range
            public float maxHeight = 1000f;

            [Header("Slope Filter")] public float minSlope = 0f; // Slope range
            public float maxSlope = 90f;

            [Header("Noise Filter")] public bool useNoise = true;
            [Range(0f, 1f)] public float noiseThreshold = 0.3f;
            public float noiseScale = 10f;
        }

        [Header("References")] public Terrain terrain;

        [Header("Rules")] public List<GrassRule> grassRules = new List<GrassRule>();

        [Header("Generation Options")] public bool generateOnStart = false;
        [Header("Debug Preview")] public List<int> previewRuleIndices = new List<int>(); // which rules to visualize

        public Vector3 cubeSize; // size of preview cubes
        [Range(1, 16)] public int gizmoStep = 4; // how many terrain cells to skip
        public bool drawGizmos = true; // master toggle

        /// <summary>
        /// Checks if a given GrassRule passes all its conditions based on terrain data.
        /// </summary>
        private bool CheckRuleConditions(GrassRule rule, float worldHeight, float slope, float nx, float nz,
            bool IgnoreNoise = false)
        {
            // Height Filter Check
            bool heightOK = worldHeight >= rule.minHeight && worldHeight <= rule.maxHeight;
            if (!heightOK) return false;

            // Slope Filter Check
            bool slopeOK = slope >= rule.minSlope && slope <= rule.maxSlope;
            if (!slopeOK) return false;

            // Noise Filter Check
            if (rule.useNoise)
            {
                float noiseValue = Mathf.PerlinNoise(nx * rule.noiseScale, nz * rule.noiseScale);
                if (noiseValue <= rule.noiseThreshold) return false;
            }

            if (IgnoreNoise) return true;
            // Spawn Chance Check
            if (Random.value * 100f > rule.spawnChance)
            {
                return false;
            }

            return true;
        }

        [ContextMenu("Generate Grass Now")]
        public void GenerateGrassNow()
        {
            if (terrain == null)
            {
                terrain = GetComponent<Terrain>();
            }

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[GrassGen] Terrain reference missing.");
                return;
            }

            TerrainData td = terrain.terrainData;
            int w = td.detailWidth;
            int h = td.detailHeight;

            int detailPrototypeCount = td.detailPrototypes.Length;
            int[][,] detailMaps = new int[detailPrototypeCount][,];
            for (int i = 0; i < detailPrototypeCount; i++)
            {
                detailMaps[i] = new int[h, w];
            }

            Vector3 terrainPos = terrain.transform.position;

            for (int y = 0; y < h; y++)
            {
                float nz = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;

                    float worldHeight = td.GetInterpolatedHeight(nx, nz) + terrainPos.y;
                    float slope = td.GetSteepness(nx, nz);

                    foreach (var rule in grassRules)
                    {
                        if (CheckRuleConditions(rule, worldHeight, slope, nx, nz))
                        {
                            foreach (var detail in rule.detailLayerIndexs)
                            {
                                if (detail < 0 || detail >= detailPrototypeCount)
                                    continue;

                                int current = detailMaps[detail][y, x];
                                if (terrain.terrainData.detailScatterMode == DetailScatterMode.InstanceCountMode)
                                    detailMaps[detail][y, x] = Mathf.RoundToInt(Mathf.Max(current, rule.density) * 16f);
                                else
                                    detailMaps[detail][y, x] =
                                        Mathf.RoundToInt(Mathf.Max(current, rule.density) * 255f);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < detailPrototypeCount; i++)
            {
                td.SetDetailLayer(0, 0, i, detailMaps[i]);
            }

            Debug.Log($"[GrassGen] Applied {grassRules.Count} rules across {detailPrototypeCount} detail layers.");
        }

        [ContextMenu("Generate Grass Coroutine")]
        public void GenerateGrassCoroutine()
        {
            if (terrain == null)
                terrain = GetComponent<Terrain>();

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[GrassGen] Terrain reference missing.");
                return;
            }

            StartCoroutine(GenerateGrassRoutine());
        }

        public IEnumerator GenerateGrassRoutine()
        {
            TerrainData td = terrain.terrainData;
            int w = td.detailWidth;
            int h = td.detailHeight;

            int detailPrototypeCount = td.detailPrototypes.Length;
            int[][,] detailMaps = new int[detailPrototypeCount][,];
            for (int i = 0; i < detailPrototypeCount; i++)
                detailMaps[i] = new int[h, w];

            Vector3 terrainPos = terrain.transform.position;

            for (int y = 0; y < h; y++)
            {
                float nz = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;

                    float worldHeight = td.GetInterpolatedHeight(nx, nz) + terrainPos.y;
                    float slope = td.GetSteepness(nx, nz);

                    foreach (var rule in grassRules)
                    {
                        if (CheckRuleConditions(rule, worldHeight, slope, nx, nz))
                        {
                            foreach (var detail in rule.detailLayerIndexs)
                            {
                                if (detail < 0 || detail >= detailPrototypeCount)
                                    continue;

                                int current = detailMaps[detail][y, x];
                                bool isexcluded = false;
                                foreach (var remover in rule.ExclusionRuleindexes)
                                {
                                    isexcluded = CheckRuleConditions(grassRules[remover], worldHeight, slope, nx, nz,
                                        true);
                                    if (isexcluded) break;
                                }

                                if (!isexcluded)
                                    if (terrain.terrainData.detailScatterMode == DetailScatterMode.InstanceCountMode)
                                        detailMaps[detail][y, x] =
                                            Mathf.RoundToInt(Mathf.Max(current, rule.density) * 16f);
                                    else
                                        detailMaps[detail][y, x] =
                                            Mathf.RoundToInt(Mathf.Max(current, rule.density) * 255f);
                            }
                        }
                    }
                }

                if (y % 8 == 0)
                    yield return null;
            }

            for (int i = 0; i < detailPrototypeCount; i++)
                td.SetDetailLayer(0, 0, i, detailMaps[i]);

            Debug.Log($"[GrassGen] Coroutine applied {grassRules.Count} rules across {detailPrototypeCount} layers.");
        }

        private void Start()
        {
            if (generateOnStart) GenerateGrassCoroutine();
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || terrain == null || terrain.terrainData == null || grassRules.Count == 0)
                return;

            TerrainData td = terrain.terrainData;
            int w = td.detailWidth;
            int h = td.detailHeight;

            Vector3 terrainSize = td.size;
            Vector3 terrainPos = terrain.transform.position;

            float cellWidth = terrainSize.x / w;
            float cellHeight = terrainSize.z / h;

            Dictionary<int, Color> ruleColors = new Dictionary<int, Color>();
            foreach (int idx in previewRuleIndices)
            {
                if (idx < 0 || idx >= grassRules.Count) continue;
                Random.InitState(idx * 7919);
                ruleColors[idx] = new Color(Random.value, Random.value, Random.value, 1f);
            }

            for (int y = 0; y < h; y += gizmoStep)
            {
                float nz = (float)y / h;
                for (int x = 0; x < w; x += gizmoStep)
                {
                    float nx = (float)x / w;
                    float worldHeight = td.GetInterpolatedHeight(nx, nz) + terrainPos.y;
                    float slope = td.GetSteepness(nx, nz);
                    foreach (int idx in previewRuleIndices)
                    {
                        if (idx < 0 || idx >= grassRules.Count) continue;
                        var rule = grassRules[idx];

                        // Use the new reusable method
                        if (CheckRuleConditions(rule, worldHeight, slope, nx, nz, true))
                        {
                            Vector3 worldPos = new Vector3(
                                terrainPos.x + x * cellWidth,
                                worldHeight + 0.2f,
                                terrainPos.z + y * cellHeight
                            );

                            Gizmos.color = ruleColors[idx];
                            Gizmos.DrawCube(worldPos, cubeSize);
                        }
                    }
                }
            }
        }
#endif
    }
}