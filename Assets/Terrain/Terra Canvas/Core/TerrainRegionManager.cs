using UnityEngine;
using UnityEngine.Serialization;

namespace CodyDreams.Solutions.TerraCanvas
{
    public class TerrainRegionManager : MonoBehaviour
    {
        [SerializeField] private Terrain currentTerrain;

        public ManualPocket[] manualPockets;

        // Each Vector3: x = worldX, y = worldZ (horizontal), z = width at that point (full width)
        public Road[] roads;
        [SerializeField] private bool drawRoads;
        [SerializeField] private bool drawPockets;

        private void OnValidate()
        {
            if (currentTerrain == null)
                currentTerrain = GetComponent<Terrain>();

            if (currentTerrain == null)
            {
                Debug.LogWarning("Terrain component not found. Disabling TerrainGenerationGizmos.");
                enabled = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (currentTerrain == null || !enabled)
                return;

            // Draw pockets in terrain local space
            if (drawPockets && manualPockets != null)
            {
                Gizmos.matrix = currentTerrain.transform.localToWorldMatrix;
                foreach (ManualPocket pocket in manualPockets)
                    DrawPocket(pocket);
                Gizmos.matrix = Matrix4x4.identity;
            }

            // Draw roads (world space)
            if (drawRoads && roads != null)
            {
                foreach (Road road in roads)
                    DrawRoad(road);
            }

            Gizmos.color = Color.white;
        }

        private void DrawPocket(ManualPocket pocket)
        {
            // Height is now defined by a float, not from Vector2.y
            float pocketHeight = pocket.Height;
            float pocketCenterY = pocket.WorldPosition.y + (pocketHeight / 2.0f);

            // **CHANGE:** pocket.Size.y now maps to the Z-axis
            Vector3 center = new Vector3(pocket.WorldPosition.x, pocketCenterY, pocket.WorldPosition.z);
            Vector3 size = new Vector3(pocket.Size.x, pocketHeight, pocket.Size.y);

            // **CHANGE:** pocket.BufferSize.y now maps to the Z-axis
            Vector3 bufferSize = new Vector3(pocket.BufferSize.x, 0f, pocket.BufferSize.y);
            Vector3 totalSize = size + bufferSize;

            Gizmos.color = new Color(1f, 0f, 0f, .5f);
            Gizmos.DrawCube(center, totalSize);
            Gizmos.DrawWireCube(center, totalSize);

            Gizmos.color = new Color(.5f, 1f, 1f, .5f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawRoad(Road road)
        {
            if (road.points == null || road.points.Length == 0)
                return;

            // Draw a simple polyline showing the path, at the road height
            Gizmos.color = Color.white;
            for (int i = 0; i < road.points.Length - 1; i++)
            {
                Gizmos.DrawLine(FlatVec3(road.points[i], road.highetFloat),
                    FlatVec3(road.points[i + 1], road.highetFloat));
            }

            Matrix4x4 originalMatrix = Gizmos.matrix;

            for (int i = 0; i < road.points.Length - 1; i++)
            {
                Vector3 pA = road.points[i];
                Vector3 pB = road.points[i + 1];

                Vector3 startPoint = new Vector3(pA.x, road.highetFloat, pA.y);
                Vector3 endPoint = new Vector3(pB.x, road.highetFloat, pB.y);

                Vector3 segmentDirection = endPoint - startPoint;
                float segmentLength = segmentDirection.magnitude;

                if (segmentLength <= Mathf.Epsilon)
                    continue;

                Vector3 segmentCenter = (startPoint + endPoint) * 0.5f;
                Quaternion segmentRotation = Quaternion.LookRotation(segmentDirection.normalized, Vector3.up);

                float blendedWidth = Mathf.Lerp(pA.z, pB.z, 0.5f);

                Gizmos.matrix = Matrix4x4.TRS(segmentCenter, segmentRotation, Vector3.one);

                Gizmos.color = new Color(1f, 0f, 0f, .5f); // buffer
                Gizmos.DrawCube(Vector3.zero,
                    new Vector3(blendedWidth + road.BufferWidth, road.DebugHight, segmentLength));

                Gizmos.color = new Color(0f, 1f, 0f, .5f); // core
                Gizmos.DrawCube(Vector3.zero, new Vector3(blendedWidth, road.DebugHight, segmentLength));
            }

            Gizmos.matrix = originalMatrix;
            for (int i = 0; i < road.points.Length; i++)
            {
                Vector3 p = road.points[i];
                Vector3 worldPos = FlatVec3(p, road.highetFloat);

                float coreRadius = p.z / 2f;
                float bufferRadius = coreRadius + road.BufferWidth;

                Gizmos.color = new Color(1f, 0f, 0f, .5f);
                Gizmos.DrawSphere(worldPos, bufferRadius);

                Gizmos.color = new Color(0f, 1f, 0f, .5f);
                Gizmos.DrawSphere(worldPos, coreRadius);
            }

            Gizmos.matrix = originalMatrix;
        }

        private Vector3 FlatVec3(Vector3 storedPoint, float y = 0f)
        {
            return new Vector3(storedPoint.x, y, storedPoint.y);
        }

        // --- Road membership test ---
        public float IsInsideRoad(Vector2 position)
        {
            float maxRoadValue = 0f;

            if (roads == null) return 0f;

            foreach (Road road in roads)
            {
                if (road.points == null || road.points.Length == 0)
                    continue;

                // 1) Spheres at points
                for (int i = 0; i < road.points.Length; i++)
                {
                    Vector3 p = road.points[i];
                    Vector2 point2D = new Vector2(p.x, p.y);
                    float distance = Vector2.Distance(position, point2D);

                    float coreRadius = p.z / 2f;
                    float bufferRadius = coreRadius + road.BufferWidth;

                    if (distance <= coreRadius)
                        return 1f;

                    if (distance <= bufferRadius)
                    {
                        float value = 1f - Mathf.InverseLerp(coreRadius, bufferRadius, distance);
                        maxRoadValue = Mathf.Max(maxRoadValue, value);
                    }
                }

                // 2) Segments between points
                for (int i = 0; i < road.points.Length - 1; i++)
                {
                    Vector3 a = road.points[i];
                    Vector3 b = road.points[i + 1];
                    Vector2 start = new Vector2(a.x, a.y);
                    Vector2 end = new Vector2(b.x, b.y);

                    Vector2 seg = end - start;
                    float segLen = seg.magnitude;
                    if (segLen <= Mathf.Epsilon)
                        continue;

                    Vector2 dir = seg / segLen;
                    float dot = Vector2.Dot(position - start, dir);
                    float clamped = Mathf.Clamp(dot, 0f, segLen);
                    Vector2 projection = start + dir * clamped;

                    float distanceToSegment = Vector2.Distance(position, projection);
                    float t = (segLen <= Mathf.Epsilon) ? 0f : (clamped / segLen);
                    float blendedWidth = Mathf.Lerp(a.z, b.z, t);
                    float coreHalf = blendedWidth / 2f;
                    float bufferHalf = coreHalf + road.BufferWidth;

                    if (distanceToSegment <= coreHalf)
                        return 1f;

                    if (distanceToSegment <= bufferHalf)
                    {
                        float value = 1f - Mathf.InverseLerp(coreHalf, bufferHalf, distanceToSegment);
                        maxRoadValue = Mathf.Max(maxRoadValue, value);
                    }
                }
            }

            return maxRoadValue;
        }

        // --- Pocket membership test using axis-aligned rectangles (X, Z) ---
        public float IsInsidePocket(Vector2 position)
        {
            if (manualPockets == null || manualPockets.Length == 0)
                return 0f;

            float maxValue = 0f;

            foreach (ManualPocket pocket in manualPockets)
            {
                // **CHANGE:** Pocket position now uses Vector3 for x, y (height), z coordinates.
                Vector2 center = new Vector2(pocket.WorldPosition.x, pocket.WorldPosition.z);
                Vector2 halfSize = new Vector2(pocket.Size.x, pocket.Size.y) / 2f;

                // **CHANGE:** Buffer size now uses Vector2 for x, z.
                Vector2 bufferThickness = pocket.BufferSize / 2f;
                Vector2 outerHalfSize = halfSize + bufferThickness;

                // Convert world space position to terrain local space for accurate check
                Vector2 localPosition = currentTerrain.transform
                    .InverseTransformPoint(new Vector3(position.x, 0f, position.y)).xz();
                Vector2 delta = localPosition - center;
                Vector2 absDelta = new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));

                if (absDelta.x > outerHalfSize.x || absDelta.y > outerHalfSize.y)
                    continue;

                Vector2 excess = new Vector2(
                    Mathf.Max(absDelta.x - halfSize.x, 0f),
                    Mathf.Max(absDelta.y - halfSize.y, 0f)
                );

                float normX = (bufferThickness.x > 0f)
                    ? excess.x / bufferThickness.x
                    : (excess.x > 0f ? float.PositiveInfinity : 0f);
                float normY = (bufferThickness.y > 0f)
                    ? excess.y / bufferThickness.y
                    : (excess.y > 0f ? float.PositiveInfinity : 0f);

                float maxNorm = Mathf.Max(normX, normY);

                float val = (maxNorm >= 1f) ? 0f : 1f - maxNorm;

                maxValue = Mathf.Max(maxValue, val);
                if (val >= 1f)
                    return 1f;
            }

            return maxValue;
        }
    }

// Custom Extension to get XZ from Vector3
    public static class Vector3Extensions
    {
        public static Vector2 xz(this Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }
    }

    [System.Serializable]
    public struct ManualPocket
    {
        public Vector3 WorldPosition; // **CHANGE:** Now uses Vector3 for X, Y(Height), Z coordinates
        public Vector2 Size; // **CHANGE:** X maps to X-size, Y maps to Z-size
        public Vector2 BufferSize; // **CHANGE:** X maps to X-buffer, Y maps to Z-buffer
        public float Height; // **CHANGE:** Height is now a separate float
    }

    [System.Serializable]
    public struct Road
    {
        // Vector3 array: x=worldX, y=worldZ (horizontal), z = width at that point
        public Vector3[] points;
        public float BufferWidth;
        public float DebugHight;
        public float highetFloat;
    }
}