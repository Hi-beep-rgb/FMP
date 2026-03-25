using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CodyDreams.Solutions.TerraCanvas
{
    [ExecuteInEditMode]
    public class ProceduralTerrainPainterJobified : MonoBehaviour
    {
        [Serializable]
        public struct SplatRule
        {
            public int terrainLayerIndex;

            [Header("Height Rules (in meters)")] public float minHeight;
            public float maxHeight;
            public float heightBuffer;

            [Header("Slope Rules (degrees)")] public float minSlopeDeg;
            public float maxSlopeDeg;
            public float slopeBufferDeg;

            [Header("Normal Rule (Slope Direction)")]
            public Vector3 normalDirection;

            [Range(0, 1)] public float normalInfluence;

            [Header("CNoise Rule")] [Range(0, 1)] public float noiseInfluence;
            public float noiseScale;
            public Vector2 noiseOffset;
        }

        public Terrain terrain;
        public TerrainRegionManager RegionManager;
        public SplatRule[] rules;

        [Header("Real-time Update")] public bool realTimeUpdate = true;
        public float updateInterval = 1.0f;

        [Header("Base & Sampling")] public bool baseLayerAsDefault = true;
        [Range(1, 4)] public int supersample = 1;

        private float _lastUpdateTime = 0;
        private bool _isUpdating = false;

        void OnEnable()
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (Application.isEditor && realTimeUpdate) StartCoroutine(UpdateTerrainCoroutine());
        }

        void OnValidate()
        {
            if (Application.isEditor && realTimeUpdate && !_isUpdating)
            {
                StartCoroutine(AssignSplatMap());
                realTimeUpdate = false;
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
        }

        private System.Collections.IEnumerator UpdateTerrainCoroutine()
        {
            _isUpdating = true;
            while (realTimeUpdate)
            {
                if (Time.time > _lastUpdateTime + updateInterval)
                {
                    StartCoroutine(AssignSplatMap());
                    _lastUpdateTime = Time.time;
                }

                yield return null;
            }

            _isUpdating = false;
        }

        // Blittable rule for jobs
        public struct RuleData
        {
            public int terrainLayerIndex;

            public float minHeight;
            public float maxHeight;
            public float heightBuffer;

            public float minSlopeDeg;
            public float maxSlopeDeg;
            public float slopeBufferDeg;

            public float3 normalDirection;
            public float normalInfluence;

            public float noiseInfluence;
            public float noiseScale;
            public float2 noiseOffset;
        }

        // Parallel job: one iteration per pixel (x,y)
        [BurstCompile(CompileSynchronously = true)]
        struct SplatParallelJob : IJobParallelFor
        {
            public int alphamapWidth;
            public int alphamapHeight;
            public int numLayers;

            public int hmRes;
            public float terrainSizeX;
            public float terrainSizeY;
            public float terrainSizeZ;

            public int samples; // supersample
            public int totalSamples;

            [ReadOnly] public NativeArray<float> heights; // flattened [y*hmRes + x]
            [ReadOnly] public NativeArray<RuleData> rules; // rulesCount length
            [ReadOnly] public int rulesCount;
            [ReadOnly] public int applyBase; // 1 or 0

            // Output: flattened per-pixel contiguous block: [ (y*width + x) * numLayers + layer ]
            // We intentionally write a block of elements per job (numLayers). Mark to disable ParallelFor safety check.
            [NativeDisableParallelForRestriction] public NativeArray<float> outSplat;

            // helpers
            [BurstCompile]
            private static float InverseLerpClamped(float a, float b, float v)
            {
                if (b == a) return 0f;
                return math.clamp((v - a) / (b - a), 0f, 1f);
            }

            [BurstCompile]
            private float SampleHeightNormalized(float sampleX, float sampleY)
            {
                float fx = sampleX * (hmRes - 1);
                float fy = sampleY * (hmRes - 1);
                int x0 = (int)math.floor(fx);
                int y0 = (int)math.floor(fy);
                int x1 = x0 + 1;
                int y1 = y0 + 1;
                x0 = math.clamp(x0, 0, hmRes - 1);
                y0 = math.clamp(y0, 0, hmRes - 1);
                x1 = math.clamp(x1, 0, hmRes - 1);
                y1 = math.clamp(y1, 0, hmRes - 1);
                float tx = fx - (float)math.floor(fx);
                float ty = fy - (float)math.floor(fy);

                float h00 = heights[y0 * hmRes + x0];
                float h10 = heights[y0 * hmRes + x1];
                float h01 = heights[y1 * hmRes + x0];
                float h11 = heights[y1 * hmRes + x1];

                float h0 = math.lerp(h00, h10, tx);
                float h1 = math.lerp(h01, h11, tx);
                return math.lerp(h0, h1, ty);
            }

            [BurstCompile]
            private float3 ComputeNormalAtSample(float sampleX, float sampleY)
            {
                // central differences in world-space meters
                float fx = sampleX * (hmRes - 1);
                float fy = sampleY * (hmRes - 1);
                int cx = (int)math.round(fx);
                int cy = (int)math.round(fy);

                int xL = math.clamp(cx - 1, 0, hmRes - 1);
                int xR = math.clamp(cx + 1, 0, hmRes - 1);
                int zD = math.clamp(cy - 1, 0, hmRes - 1);
                int zU = math.clamp(cy + 1, 0, hmRes - 1);

                float hL = heights[cy * hmRes + xL] * terrainSizeY;
                float hR = heights[cy * hmRes + xR] * terrainSizeY;
                float hD = heights[zD * hmRes + cx] * terrainSizeY;
                float hU = heights[zU * hmRes + cx] * terrainSizeY;

                float dx = terrainSizeX / (hmRes - 1);
                float dz = terrainSizeZ / (hmRes - 1);

                float dhdx = (hR - hL) / (2f * dx);
                float dhdz = (hU - hD) / (2f * dz);

                float3 n = new float3(-dhdx, 1f, -dhdz);
                return math.normalize(n);
            }

            [BurstCompile]
            public void Execute(int index)
            {
                int totalPixels = alphamapWidth * alphamapHeight;
                if (index < 0 || index >= totalPixels) return;

                int y = index / alphamapWidth;
                int x = index - y * alphamapWidth;

                float normalizedX = (float)x / math.max(1, alphamapWidth - 1);
                float normalizedY = (float)y / math.max(1, alphamapHeight - 1);

                int baseIndex = index * numLayers;

                // initialize per-pixel layers
                if (applyBase == 1 && numLayers > 0)
                {
                    for (int li = 0; li < numLayers; li++) outSplat[baseIndex + li] = 0f;
                    outSplat[baseIndex + 0] = 1f;
                }
                else
                {
                    for (int li = 0; li < numLayers; li++) outSplat[baseIndex + li] = 0f;
                }

                float texelSizeX = 1f / math.max(1, alphamapWidth - 1);
                float texelSizeY = 1f / math.max(1, alphamapHeight - 1);

                for (int r = 0; r < rulesCount; r++)
                {
                    RuleData rule = rules[r];
                    float accumAlpha = 0f;

                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float offsetX = ((sx + 0.5f) / (float)samples - 0.5f) * texelSizeX;
                            float offsetY = ((sy + 0.5f) / (float)samples - 0.5f) * texelSizeY;
                            float sampleX = math.clamp(normalizedX + offsetX, 0f, 1f);
                            float sampleY = math.clamp(normalizedY + offsetY, 0f, 1f);

                            float heightNormalized = SampleHeightNormalized(sampleX, sampleY);
                            float heightMeters = heightNormalized * terrainSizeY;

                            float3 normal = ComputeNormalAtSample(sampleX, sampleY);
                            float slopeDeg = math.degrees((float)math.acos(math.clamp(normal.y, -1f, 1f)));

                            // height weight
                            float heightWeight = 0f;
                            float minH = rule.minHeight;
                            float maxH = rule.maxHeight;
                            float hb = math.max(0f, rule.heightBuffer);

                            if (heightMeters >= minH && heightMeters <= maxH) heightWeight = 1f;
                            else if (heightMeters < minH && hb > 0f && heightMeters >= (minH - hb))
                                heightWeight = InverseLerpClamped(minH - hb, minH, heightMeters);
                            else if (heightMeters > maxH && hb > 0f && heightMeters <= (maxH + hb))
                                heightWeight = InverseLerpClamped(maxH + hb, maxH, heightMeters);

                            // slope weight
                            float slopeWeight = 0f;
                            float minS = math.clamp(rule.minSlopeDeg, 0f, 90f);
                            float maxS = math.clamp(rule.maxSlopeDeg, 0f, 90f);
                            float sb = math.max(0f, rule.slopeBufferDeg);

                            if (slopeDeg >= minS && slopeDeg <= maxS) slopeWeight = 1f;
                            else if (slopeDeg < minS && sb > 0f && slopeDeg >= (minS - sb))
                                slopeWeight = InverseLerpClamped(minS - sb, minS, slopeDeg);
                            else if (slopeDeg > maxS && sb > 0f && slopeDeg <= (maxS + sb))
                                slopeWeight = InverseLerpClamped(maxS + sb, maxS, slopeDeg);

                            // Noise weight
                            float noiseWeight = 1f;
                            if (rule.noiseInfluence > 0f && rule.noiseScale > 0f)
                            {
                                float2 noisePos = new float2(sampleX, sampleY) * rule.noiseScale + rule.noiseOffset;
                                float noiseVal = noise.cnoise(noisePos);
                                // Cnoise returns -1 to 1, so we remap it to 0-1
                                float normalizedNoise = (noiseVal + 1f) * 0.5f;
                                noiseWeight = math.lerp(1f, normalizedNoise, rule.noiseInfluence);
                            }

                            // Normal weight
                            float normalWeight = 0f;
                            if (rule.normalInfluence > 0f)
                            {
                                float3 dir = math.normalize(rule.normalDirection);
                                normalWeight = math.clamp(math.dot(normal, dir), 0f, 1f);
                            }

                            float baseWeight = heightWeight * slopeWeight * noiseWeight;
                            float finalWeight = math.clamp
                            (baseWeight * (1f - rule.normalInfluence) + baseWeight * normalWeight * rule.normalInfluence,
                                0f, 1f);

                            accumAlpha += finalWeight;
                        }
                    } // samples

                    float avgAlpha = accumAlpha / math.max(1, totalSamples);

                    if (avgAlpha > 0.001f)
                    {
                        float alpha = math.clamp(avgAlpha, 0f, 1f);
                        for (int li = 0; li < numLayers; li++)
                        {
                            outSplat[baseIndex + li] = outSplat[baseIndex + li] * (1f - alpha);
                        }

                        int layerIndex = math.clamp(rule.terrainLayerIndex, 0, numLayers - 1);
                        outSplat[baseIndex + layerIndex] += alpha;
                    }
                } // rules

                // normalize
                float totalWeight = 0f;
                for (int li = 0; li < numLayers; li++) totalWeight += outSplat[baseIndex + li];

                if (totalWeight <= 0f && numLayers > 0)
                {
                    outSplat[baseIndex + 0] = 1f;
                    totalWeight = 1f;
                }

                if (totalWeight > 0f)
                {
                    float inv = 1f / totalWeight;
                    for (int li = 0; li < numLayers; li++) outSplat[baseIndex + li] *= inv;
                }
            } // Execute
        } // SplatParallelJob

        // ------------------------------
        // Main AssignSplatMap method
        // ------------------------------
        public System.Collections.IEnumerator AssignSplatMap()
        {
            float currenttime = Time.realtimeSinceStartup;

            if (terrain == null || rules == null || rules.Length == 0)
            {
                Debug.LogWarning("Terrain or rules are not set. Cannot assign splatmap.");
                yield break;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                Debug.LogWarning("TerrainData is null.");
                yield break;
            }

            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            int numLayers = terrainData.alphamapLayers;
            yield return null;
            // clamp rule indices
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].terrainLayerIndex < 0 || rules[i].terrainLayerIndex >= numLayers)
                {
                    rules[i].terrainLayerIndex = Mathf.Clamp(rules[i].terrainLayerIndex, 0, numLayers - 1);
                }
            }

            yield return null;
            // heights -> NativeArray (flattened)
            int hmRes = terrainData.heightmapResolution;
            float[,] heights2D = terrainData.GetHeights(0, 0, hmRes, hmRes); // heights[y,x]
            NativeArray<float> heightsNative = new NativeArray<float>(hmRes * hmRes, Allocator.Persistent);
            for (int yy = 0; yy < hmRes; yy++)
            for (int xx = 0; xx < hmRes; xx++)
                heightsNative[yy * hmRes + xx] = heights2D[yy, xx];
            yield return null;
            // rules -> NativeArray<RuleData>
            int rulesCount = rules.Length;
            NativeArray<RuleData> rulesNative = new NativeArray<RuleData>(rulesCount, Allocator.Persistent);
            for (int i = 0; i < rulesCount; i++)
            {
                RuleData rd = new RuleData
                {
                    terrainLayerIndex = rules[i].terrainLayerIndex,
                    minHeight = rules[i].minHeight,
                    maxHeight = rules[i].maxHeight,
                    heightBuffer = math.max(0f, rules[i].heightBuffer),
                    minSlopeDeg = math.clamp(rules[i].minSlopeDeg, 0f, 90f),
                    maxSlopeDeg = math.clamp(rules[i].maxSlopeDeg, 0f, 90f),
                    slopeBufferDeg = math.max(0f, rules[i].slopeBufferDeg),
                    normalDirection = new float3(rules[i].normalDirection.x, rules[i].normalDirection.y,
                        rules[i].normalDirection.z),
                    normalInfluence = math.clamp(rules[i].normalInfluence, 0f, 1f),
                    noiseInfluence = math.clamp(rules[i].noiseInfluence, 0f, 1f),
                    noiseScale = math.max(0f, rules[i].noiseScale),
                    noiseOffset = new float2(rules[i].noiseOffset.x, rules[i].noiseOffset.y)
                };
                if (math.lengthsq(rd.normalDirection) == 0f) rd.normalDirection = new float3(0f, 1f, 0f);
                else rd.normalDirection = math.normalize(rd.normalDirection);
                rulesNative[i] = rd;
            }

            yield return null;
            int samples = math.max(1, supersample);
            int totalSamples = samples * samples;

            int totalPixels = alphamapWidth * alphamapHeight;
            NativeArray<float> outSplat = new NativeArray<float>(totalPixels * numLayers, Allocator.Persistent);

            // schedule parallel job
            SplatParallelJob job = new SplatParallelJob
            {
                alphamapWidth = alphamapWidth,
                alphamapHeight = alphamapHeight,
                numLayers = numLayers,
                hmRes = hmRes,
                terrainSizeX = terrainData.size.x,
                terrainSizeY = terrainData.size.y,
                terrainSizeZ = terrainData.size.z,
                samples = samples,
                totalSamples = totalSamples,
                heights = heightsNative,
                rules = rulesNative,
                rulesCount = rulesCount,
                applyBase = (baseLayerAsDefault && numLayers > 0) ? 1 : 0,
                outSplat = outSplat
            };

            int batchSize = math.max(1, alphamapWidth / 2);
            JobHandle handle = job.Schedule(totalPixels, batchSize, default);
            yield return new WaitWhile(() => handle.IsCompleted);
            handle.Complete();

            // convert to Unity float[,,] [x,y,layer]
            // *** NOTE: you asked to invert mapping: put x into y and y into x.
            // So we assign splatmapData[y, x, layer] = outSplat[ baseIndex + layer ]
            float[,,] splatmapData = new float[alphamapWidth, alphamapHeight, numLayers];
            float[,,] originalSplatmap = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            for (int y = 0; y < alphamapHeight; y++)
            {
                for (int x = 0; x < alphamapWidth; x++)
                {
                    int baseIndex = (y * alphamapWidth + x) * numLayers;
                    float sum = 0f;

                    Vector2 worldPosition = new Vector2(
                        (float)x / alphamapWidth * terrainData.size.x,
                        (float)y / alphamapHeight * terrainData.size.z
                    );
                    if (RegionManager)
                    {
                        worldPosition += new Vector2(terrain.transform.position.x, terrain.transform.position.z);
                        float roadInfluence = RegionManager.IsInsideRoad(worldPosition);
                        float pocketInfluence = RegionManager.IsInsidePocket(worldPosition);
                        for (int li = 0; li < numLayers; li++)
                        {
                            float v = outSplat[baseIndex + li];
                            v = math.clamp(v, 0f, 1f);
                            // <-- swapped indices here per your request
                            if (pocketInfluence + roadInfluence == 0f)
                            {
                                splatmapData[y, x, li] = v;
                                sum += v;
                            }
                            else
                            {
                                float blendedvalue = Mathf.Lerp(v, originalSplatmap[y, x, li],
                                    (roadInfluence + pocketInfluence) *
                                    (roadInfluence + pocketInfluence));
                                splatmapData[y, x, li] = blendedvalue;
                                sum += blendedvalue;
                            }
                        }

                        if (sum > 0f)
                        {
                            float inv = 1f / sum;
                            for (int li = 0; li < numLayers; li++) splatmapData[y, x, li] *= inv;
                        }
                        else if (numLayers > 0)
                        {
                            splatmapData[y, x, 0] = 1f;
                        }
                    }
                    else
                    {
                        for (int li = 0; li < numLayers; li++)
                        {
                            float v = outSplat[baseIndex + li];
                            v = math.clamp(v, 0f, 1f);
                            splatmapData[y, x, li] = v;
                            sum += v;

                        }

                        if (sum > 0f)
                        {
                            float inv = 1f / sum;
                            for (int li = 0; li < numLayers; li++) splatmapData[y, x, li] *= inv;
                        }
                        else if (numLayers > 0)
                        {
                            splatmapData[y, x, 0] = 1f;
                        }
                    }


                }
            }

            // apply
            terrainData.SetAlphamaps(0, 0, splatmapData);

            // dispose
            heightsNative.Dispose();
            rulesNative.Dispose();
            outSplat.Dispose();

            Debug.Log($"Splat assign time: {Time.realtimeSinceStartup - currenttime:0.###}s");
        }
    }
}