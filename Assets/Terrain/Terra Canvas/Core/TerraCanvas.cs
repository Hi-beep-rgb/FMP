using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CodyDreams.Solutions.TerraCanvas
{
    public class TerraCanvas : MonoBehaviour
    {
        [Header("Configuration")] [Tooltip("The asset containing the ordered list of generation passes.")]
        public TerraCanvasPasses terrainPipeline;

        [Header("References")] public Terrain terrain;
        public TerrainRegionManager terrainRegionManager;
        public RenderTextureTerrainGenerator TerrainGenerator;
        public ProceduralTerrainPainterJobified TerrainPainter;
        public SimpleTreeGeneratorFixed TreeGenerator;
        public ProceduralGrassPainter DetailPainter;

#if UNITY_EDITOR
        public HeightmapGenerator BackupGenerator;
        [Tooltip("Save a backup of your handcrafted world, we recommend turning this off once you have a " +
                 "procedural world because having the original hand-crafted world is more useful.")]
        public bool SaveBackup;
#endif

        void Awake()
        {
            // Find references if not set.
            if (terrain == null)
                terrain = GetComponent<Terrain>();

            if (TerrainGenerator == null)
                TerrainGenerator = GetComponent<RenderTextureTerrainGenerator>();

            if (TerrainPainter == null)
                TerrainPainter = GetComponent<ProceduralTerrainPainterJobified>();

            if (TreeGenerator == null)
                TreeGenerator = GetComponent<SimpleTreeGeneratorFixed>();

            if (terrainRegionManager == null)
                terrainRegionManager = GetComponent<TerrainRegionManager>();

            if (DetailPainter == null)
                DetailPainter = GetComponent<ProceduralGrassPainter>();

            // Disable components and assign terrain reference.
            if (TerrainGenerator != null)
            {
                TerrainGenerator.enabled = false;
                if (TerrainGenerator.terrain == null) TerrainGenerator.terrain = terrain;
                if (terrainRegionManager && !TerrainGenerator.regionManager)
                    TerrainGenerator.regionManager = terrainRegionManager;
            }

            if (TerrainPainter != null)
            {
                TerrainPainter.enabled = false;
                if (TerrainPainter.terrain == null) TerrainPainter.terrain = terrain;
            }

            if (TreeGenerator != null)
            {
                TreeGenerator.enabled = false;
                if (TreeGenerator.terrain == null) TreeGenerator.terrain = terrain;
            }

            if (DetailPainter != null)
            {
                DetailPainter.enabled = false;
                if (DetailPainter.terrain == null) DetailPainter.terrain = terrain;
            }
#if UNITY_EDITOR
            if (BackupGenerator == null)
                BackupGenerator = GetComponent<HeightmapGenerator>();
            if (BackupGenerator != null)
            {
                if (BackupGenerator.terrain == null) BackupGenerator.terrain = terrain;
                if (BackupGenerator.heightmapTexture == null) SaveBackup = false;
            }
#endif
        }

        void Start()
        {
            // Check for the main terrain reference.
            if (terrain == null)
            {
                Debug.LogError("Terra Canvas: Terrain reference is missing. Cannot proceed with generation.");
                return;
            }

            if (terrainPipeline == null)
            {
                Debug.LogError(
                    "Terra Canvas: Terrain pipeline asset is missing. Please assign a TerraCanvasPasses ScriptableObject.");
                return;
            }

            // Start the main coroutine chain.
            StartCoroutine(ExecuteTerrainPipeline());
        }

        private IEnumerator ExecuteTerrainPipeline()
        {
            Debug.Log("Terra Canvas: Starting terrain generation sequence...");
            float startTime = Time.realtimeSinceStartup;
            foreach (var pass in terrainPipeline.generationPasses)
            {
                // these are actions each pass would run , add anything you want in here.
                switch (pass)
                {
                    case GenerationStep.GenerateHeightmap:
                        if (TerrainGenerator != null)
                        {
                            Debug.Log("Terra Canvas: Generating terrain heightmap...");
                            yield return StartCoroutine(TerrainGenerator.GenerateTerrainFromTextureCoroutine());
                        }

                        break;

                    case GenerationStep.PaintTerrain:
                        if (TerrainPainter != null)
                        {
                            Debug.Log("Terra Canvas: Painting terrain splatmap...");
                            yield return StartCoroutine(TerrainPainter.AssignSplatMap());
                        }

                        break;

                    case GenerationStep.GenerateTrees:
                        if (TreeGenerator != null)
                        {
                            Debug.Log("Terra Canvas: Generating trees...");
                            yield return StartCoroutine(TreeGenerator.GenerateTreesCoroutine());
                        }

                        break;

#if UNITY_EDITOR
                    case GenerationStep.SaveBackup:
                        if (BackupGenerator != null)
                        {
                            Debug.Log("Terra Canvas: Saving terrain heightmap backup...");
                            BackupGenerator.SaveHeightmap();
                        }

                        break;
                    case GenerationStep.LoadBackup:
                        if (BackupGenerator != null)
                        {
                            Debug.Log("Terra Canvas: Loading terrain heightmap backup...");
                            BackupGenerator.LoadHeightmap();
                        }

                        break;
#endif
                    case GenerationStep.None:
                        Debug.Log("Terra Canvas: Skipping a 'None' step.");
                        break;
                    case GenerationStep.GenerateDetail:
                        if (DetailPainter != null)
                        {
                            Debug.Log("Terra Canvas: Generating detail ...");
                            yield return StartCoroutine(DetailPainter.GenerateGrassRoutine());
                        }

                        break;
                    case GenerationStep.Randomize:
                        TerraCanvasRandomizer.Instance.RegisterAndRandomizeTerrain(terrain);
                        break;
                    default:
                        Debug.LogWarning($"Terra Canvas: Unhandled generation step: {pass}.");
                        break;
                }
            }

            Debug.Log("Terra Canvas: All generation steps are complete! and took " +
                      (Time.realtimeSinceStartup - startTime) + "" +
                      "seconds.");
        }


    }
}