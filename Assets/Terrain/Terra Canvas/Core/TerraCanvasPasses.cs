using System.Collections.Generic;
using UnityEngine;

namespace CodyDreams.Solutions.TerraCanvas
{
    [CreateAssetMenu(fileName = "TerraCanvasPasses", menuName = "Terra Canvas/TerraCanvasPasses")]
    public class TerraCanvasPasses : ScriptableObject
    {
        // The list of generation steps to execute in order.
        public List<GenerationStep> generationPasses = new List<GenerationStep>();
    }

// Define new passes in here if you want
    public enum GenerationStep
    {
        None,
        GenerateHeightmap,
        PaintTerrain,
        GenerateTrees,
        GenerateDetail,
        Randomize,
        SaveBackup,
        LoadBackup
    }
}