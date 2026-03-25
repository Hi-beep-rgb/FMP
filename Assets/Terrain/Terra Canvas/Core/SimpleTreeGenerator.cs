using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CodyDreams.Solutions.TerraCanvas
{
    [ExecuteInEditMode]
    public class SimpleTreeGeneratorFixed : MonoBehaviour
    {
        [System.Serializable]
        public struct TreeRule
        {
            public GameObject treePrefab; // Prefab to use as prototype
            [Range(0, 100)] public int spawnChance; // chance out of 100
            public float minHeight; // world-space meters
            public float maxHeight;
            public float minSlope; // degrees
            public float maxSlope;
            public float noiseThreshold; // 0..1
            public bool useNoise;
        }

        [Header("References")] public Terrain terrain;

        [Header("Rules")] public List<TreeRule> treeRules = new List<TreeRule>();

        [Header("Generation Options")] [Tooltip("Generate automatically when the scene starts (Play Mode).")]
        public bool generateOnStart = false;

        [Tooltip("If true, a coroutine will be used in Play Mode to spread work across frames.")]
        public bool useCoroutineInPlay = true;

        [Tooltip("Number of terrain sample points processed per frame when using coroutine.")] [Range(1, 50000)]
        public int pointsPerFrame = 2000;

        [Tooltip("Spacing (meters) between sample points.")] [Range(0.5f, 50f)]
        public float resolution = 2f;

        // -----------------------
        // Gizmo preview settings
        // -----------------------
#if UNITY_EDITOR
        [Header("Preview (Gizmos)")] [Tooltip("Toggle the preview gizmos on/off.")]
        public bool previewInScene = true;

        [Tooltip("If true, preview honors spawnChance using a stable hash.")]
        public bool previewRespectChance = false;

        [Tooltip("Seed used for deterministic preview when 'previewRespectChance' is enabled.")]
        public int previewSeed = 12345;

        [Tooltip("Cluster division X (how many clusters across the terrain). Fewer clusters = fewer cubes.")] [Min(1)]
        public int previewClusterCountX = 5;

        [Tooltip("Cluster division Z (how many clusters along terrain Z).")] [Min(1)]
        public int previewClusterCountZ = 5;

        [Tooltip("Number of deterministic sample cubes drawn in addition to clusters.")] [Range(0, 50)]
        public int previewSampleCount = 5;

        [Tooltip("Cube size in meters for both cluster cubes and sample cubes.")] [Range(0.05f, 50f)]
        public float previewGizmoSize = 1f;

        // Per-rule toggle: show gizmos for this rule when true (inspector editable)
        [Tooltip("Per-rule toggles: enable to draw gizmos for that specific rule.")]
        public List<bool> previewRuleEnabled = new List<bool>();

        // color palette
        private static readonly Color[] kRuleColors =
        {
            new Color(0.25f, 1f, 0.25f, 0.5f), // green
            new Color(0.25f, 1f, 1f, 0.5f), // cyan
            new Color(1f, 1f, 0.25f, 0.5f), // yellow
            new Color(1f, 0.6f, 0.2f, 0.5f), // orange
            new Color(1f, 0.5f, 1f, 0.5f), // magenta
            new Color(0.6f, 0.6f, 1f, 0.5f), // blue-ish
        };
#endif

        // Internal
        private Coroutine generationCoroutine = null;

        [ContextMenu("Generate Trees Now")]
        public void GenerateNow()
        {
            if (terrain == null)
            {
                Debug.LogError("[TreeGen] Terrain reference is null.");
                return;
            }

            if (Application.isPlaying)
            {
                if (useCoroutineInPlay)
                {
                    if (generationCoroutine != null) StopCoroutine(generationCoroutine);
                    generationCoroutine = StartCoroutine(GenerateTreesCoroutine());
                }
                else
                {
                    GenerateTreesImmediate();
                }
            }
            else
            {
                GenerateTreesImmediate();
            }
        }

        private void Start()
        {
            if (generateOnStart)
            {
                if (useCoroutineInPlay)
                {
                    generationCoroutine = StartCoroutine(GenerateTreesCoroutine());
                }
                else
                {
                    GenerateTreesImmediate();
                }
            }
        }

        public IEnumerator GenerateTreesCoroutine()
        {
            if (terrain == null) yield break;

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) yield break;

            Dictionary<GameObject, int> protoMap;
            TreePrototype[] prototypes = EnsurePrototypesAndGetMap(terrainData, out protoMap);

            List<TreeInstance> placed = new List<TreeInstance>();

            Vector3 terrainSize = terrainData.size;
            Vector3 terrainPos = terrain.transform.position;

            int stepsX = Mathf.Max(1, Mathf.FloorToInt(terrainSize.x / resolution));
            int stepsZ = Mathf.Max(1, Mathf.FloorToInt(terrainSize.z / resolution));

            int total = (stepsX + 1) * (stepsZ + 1);
            int processed = 0;

            for (int iz = 0; iz <= stepsZ; iz++)
            {
                float normalizedZ = (float)iz / (float)stepsZ;

                for (int ix = 0; ix <= stepsX; ix++)
                {
                    float normalizedX = (float)ix / (float)stepsX;

                    float worldHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) + terrainPos.y;
                    float slope = terrainData.GetSteepness(normalizedX, normalizedZ);

                    float noiseValue = Mathf.PerlinNoise(normalizedX * 10f, normalizedZ * 10f);

                    float rand01 = Random.value * 100f;

                    for (int r = 0; r < treeRules.Count; r++)
                    {
                        var rule = treeRules[r];
                        if (rule.treePrefab == null) continue;

                        if (rand01 < Mathf.Clamp(rule.spawnChance, 0, 100))
                        {
                            bool heightMatch = (worldHeight >= rule.minHeight && worldHeight <= rule.maxHeight);
                            bool slopeMatch = (slope >= rule.minSlope && slope <= rule.maxSlope);
                            bool noiseMatch = !rule.useNoise || (noiseValue > rule.noiseThreshold);

                            if (heightMatch && slopeMatch && noiseMatch)
                            {
                                TreeInstance ti = new TreeInstance();
                                ti.prototypeIndex = protoMap[rule.treePrefab];
                                ti.position = new Vector3(normalizedX, (worldHeight - terrainPos.y) / terrainSize.y,
                                    normalizedZ);
                                ti.widthScale = 1f;
                                ti.heightScale = 1f;
                                ti.rotation = Random.Range(0f, 360f);
                                ti.color = Color.white;
                                ti.lightmapColor = Color.white;

                                placed.Add(ti);
                                break;
                            }
                        }
                    }

                    processed++;
                    if (processed % pointsPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }

            terrainData.treeInstances = placed.ToArray();

#if UNITY_EDITOR
            EditorUtility.SetDirty(terrainData);
#endif

            Debug.Log($"[TreeGen] Completed: placed {placed.Count} trees (coroutine).");

            generationCoroutine = null;
        }

        private void GenerateTreesImmediate()
        {
            if (terrain == null) return;
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) return;

            Dictionary<GameObject, int> protoMap;
            TreePrototype[] prototypes = EnsurePrototypesAndGetMap(terrainData, out protoMap);

            List<TreeInstance> placed = new List<TreeInstance>();

            Vector3 terrainSize = terrainData.size;
            Vector3 terrainPos = terrain.transform.position;

            int stepsX = Mathf.Max(1, Mathf.FloorToInt(terrainSize.x / resolution));
            int stepsZ = Mathf.Max(1, Mathf.FloorToInt(terrainSize.z / resolution));

            for (int iz = 0; iz <= stepsZ; iz++)
            {
                float normalizedZ = (float)iz / (float)stepsZ;

                for (int ix = 0; ix <= stepsX; ix++)
                {
                    float normalizedX = (float)ix / (float)stepsX;

                    float worldHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) + terrainPos.y;
                    float slope = terrainData.GetSteepness(normalizedX, normalizedZ);
                    float noiseValue = Mathf.PerlinNoise(normalizedX * 10f, normalizedZ * 10f);
                    float rand01 = Random.value * 100f;

                    for (int r = 0; r < treeRules.Count; r++)
                    {
                        var rule = treeRules[r];
                        if (rule.treePrefab == null) continue;

                        if (rand01 < Mathf.Clamp(rule.spawnChance, 0, 100))
                        {
                            bool heightMatch = (worldHeight >= rule.minHeight && worldHeight <= rule.maxHeight);
                            bool slopeMatch = (slope >= rule.minSlope && slope <= rule.maxSlope);
                            bool noiseMatch = !rule.useNoise || (noiseValue > rule.noiseThreshold);

                            if (heightMatch && slopeMatch && noiseMatch)
                            {
                                TreeInstance ti = new TreeInstance();
                                ti.prototypeIndex = protoMap[rule.treePrefab];
                                ti.position = new Vector3(normalizedX, (worldHeight - terrainPos.y) / terrainSize.y,
                                    normalizedZ);
                                ti.widthScale = 1f;
                                ti.heightScale = 1f;
                                ti.rotation = Random.Range(0f, 360f);
                                ti.color = Color.white;
                                ti.lightmapColor = Color.white;

                                placed.Add(ti);
                                break;
                            }
                        }
                    }
                }
            }

            terrainData.treeInstances = placed.ToArray();

#if UNITY_EDITOR
            EditorUtility.SetDirty(terrainData);
#endif

            Debug.Log($"[TreeGen] Completed: placed {placed.Count} trees (immediate).");
        }

        private TreePrototype[] EnsurePrototypesAndGetMap(TerrainData terrainData,
            out Dictionary<GameObject, int> protoMap)
        {
            protoMap = new Dictionary<GameObject, int>();

            List<TreePrototype> protolist = new List<TreePrototype>(terrainData.treePrototypes ?? new TreePrototype[0]);

            for (int i = 0; i < protolist.Count; i++)
            {
                GameObject p = protolist[i].prefab;
                if (p != null && !protoMap.ContainsKey(p))
                    protoMap[p] = i;
            }

            foreach (var rule in treeRules)
            {
                if (rule.treePrefab == null) continue;
                if (!protoMap.ContainsKey(rule.treePrefab))
                {
                    protolist.Add(new TreePrototype { prefab = rule.treePrefab });
                    protoMap[rule.treePrefab] = protolist.Count - 1;
                    Debug.Log($"[TreeGen] Added prototype for: {rule.treePrefab.name}");
                }
            }

            TreePrototype[] final = protolist.ToArray();
            bool different = terrainData.treePrototypes == null || terrainData.treePrototypes.Length != final.Length;
            if (!different)
            {
                for (int i = 0; i < final.Length; i++)
                {
                    if (terrainData.treePrototypes[i].prefab != final[i].prefab)
                    {
                        different = true;
                        break;
                    }
                }
            }

            if (different)
            {
                terrainData.treePrototypes = final;
            }

            return final;
        }

        // ---------------------------
        // Gizmos / Editor utilities
        // ---------------------------
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!enabled) return;
            // clamp some sensible limits
            pointsPerFrame = Mathf.Max(1, pointsPerFrame);
            resolution = Mathf.Max(0.01f, resolution);
            previewClusterCountX = Mathf.Max(1, previewClusterCountX);
            previewClusterCountZ = Mathf.Max(1, previewClusterCountZ);
            previewSampleCount = Mathf.Max(0, previewSampleCount);
            previewGizmoSize = Mathf.Max(0.01f, previewGizmoSize);

            // Ensure previewRuleEnabled matches number of rules
            if (previewRuleEnabled == null) previewRuleEnabled = new List<bool>();
            if (previewRuleEnabled.Count != treeRules.Count)
            {
                // preserve existing entries where possible
                List<bool> newList = new List<bool>(treeRules.Count);
                for (int i = 0; i < treeRules.Count; i++)
                {
                    if (i < previewRuleEnabled.Count) newList.Add(previewRuleEnabled[i]);
                    else newList.Add(true); // default to visible
                }

                previewRuleEnabled = newList;
            }

            // repaint scene so gizmos update when you tweak inspector values
            if (!Application.isPlaying)
            {
                SceneView.RepaintAll();
            }
        }

        // deterministic small hash RNG: returns 0..(mod-1)
        private int HashRanged(int a, int b, int seed, int mod)
        {
            unchecked
            {
                int h = a * 73856093 ^ b * 19349663 ^ seed * 83492791;
                h = (h << 13) ^ h;
                return Mathf.Abs(h % Mathf.Max(1, mod));
            }
        }

        private bool MatchesRuleAt(
            float nX, float nZ,
            TerrainData td, Vector3 tPos, Vector3 tSize,
            TreeRule rule, bool respectChance,
            int ix, int iz)
        {
            if (rule.treePrefab == null) return false;

            float worldH = td.GetInterpolatedHeight(nX, nZ) + tPos.y;
            float slope = td.GetSteepness(nX, nZ);
            float noise = Mathf.PerlinNoise(nX * 10f, nZ * 10f);

            bool heightOK = (worldH >= rule.minHeight && worldH <= rule.maxHeight);
            bool slopeOK = (slope >= rule.minSlope && slope <= rule.maxSlope);
            bool noiseOK = !rule.useNoise || (noise > rule.noiseThreshold);
            if (!(heightOK && slopeOK && noiseOK)) return false;

            if (!respectChance) return true;

            int rnd100 = HashRanged(ix, iz, previewSeed, 100);
            return rnd100 < Mathf.Clamp(rule.spawnChance, 0, 100);
        }

        private void OnDrawGizmosSelected()
        {
            if (!previewInScene || terrain == null || treeRules == null || treeRules.Count == 0 || !enabled) return;

            var td = terrain.terrainData;
            if (td == null) return;

            Vector3 tSize = td.size;
            Vector3 tPos = terrain.transform.position;

            float safeRes = Mathf.Max(0.0001f, resolution);
            int stepsX = Mathf.Max(1, Mathf.FloorToInt(tSize.x / safeRes));
            int stepsZ = Mathf.Max(1, Mathf.FloorToInt(tSize.z / safeRes));

            // Cluster division based on previewClusterCountX/Z
            int clusterCountX = Mathf.Max(1, previewClusterCountX);
            int clusterCountZ = Mathf.Max(1, previewClusterCountZ);

            int stepX = Mathf.Max(1, Mathf.FloorToInt((float)stepsX / clusterCountX));
            int stepZ = Mathf.Max(1, Mathf.FloorToInt((float)stepsZ / clusterCountZ));

            // draw cluster cubes, but ONLY when a rule matches AND that rule has previewRuleEnabled = true
            for (int cz = 0; cz <= stepsZ; cz += stepZ)
            {
                for (int cx = 0; cx <= stepsX; cx += stepX)
                {
                    float nX = (float)cx / Mathf.Max(1, stepsX);
                    float nZ = (float)cz / Mathf.Max(1, stepsZ);

                    int matchedRule = -1;
                    for (int r = 0; r < treeRules.Count; r++)
                    {
                        if (MatchesRuleAt(nX, nZ, td, tPos, tSize, treeRules[r], previewRespectChance, cx, cz))
                        {
                            matchedRule = r;
                            break;
                        }
                    }

                    // skip drawing anything if no rule matched OR the matched rule is disabled for preview
                    if (matchedRule < 0) continue;
                    if (matchedRule < previewRuleEnabled.Count && !previewRuleEnabled[matchedRule]) continue;

                    Color c = kRuleColors[matchedRule % kRuleColors.Length];

                    float worldH = td.GetInterpolatedHeight(nX, nZ) + tPos.y;
                    Vector3 clusterCenter = new Vector3(tPos.x + nX * tSize.x, worldH, tPos.z + nZ * tSize.z);

                    Gizmos.color = c;
                    float clusterSizeX = stepX * resolution;
                    float clusterSizeZ = stepZ * resolution;
                    // Draw filled cube (centered)
                    Gizmos.DrawCube(clusterCenter, new Vector3(clusterSizeX, previewGizmoSize * 0.5f, clusterSizeZ));
                    // outline for clarity
                    //   Gizmos.color = Color.black;
                    //   Gizmos.DrawWireCube(clusterCenter, new Vector3(clusterSizeX, previewGizmoSize * 0.5f, clusterSizeZ));
                }
            }

            // deterministic sample cubes (a few) -- only draw if the sampled cell matches an enabled rule
            int sampleCount = Mathf.Clamp(previewSampleCount, 0, 50);
            for (int i = 0; i < sampleCount; i++)
            {
                int ix = HashRanged(i, previewSeed, i + 1, stepsX + 1);
                int iz = HashRanged(i + 7, previewSeed, i + 11, stepsZ + 1);

                float nX = (float)ix / Mathf.Max(1, stepsX);
                float nZ = (float)iz / Mathf.Max(1, stepsZ);

                int matchedRule = -1;
                for (int r = 0; r < treeRules.Count; r++)
                {
                    if (MatchesRuleAt(nX, nZ, td, tPos, tSize, treeRules[r], previewRespectChance, ix, iz))
                    {
                        matchedRule = r;
                        break;
                    }
                }

                // skip sample if no matched rule or that rule disabled for preview
                if (matchedRule < 0) continue;
                if (matchedRule < previewRuleEnabled.Count && !previewRuleEnabled[matchedRule]) continue;

                float worldH = td.GetInterpolatedHeight(nX, nZ) + tPos.y;
                Vector3 pos = new Vector3(tPos.x + nX * tSize.x, worldH, tPos.z + nZ * tSize.z);

                Gizmos.color = Color.black;
                Gizmos.DrawWireCube(pos, Vector3.one * previewGizmoSize);
            }
        }
#endif
    }
}