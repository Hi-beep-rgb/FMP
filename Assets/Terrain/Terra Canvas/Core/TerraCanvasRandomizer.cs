using UnityEngine;

namespace CodyDreams.Solutions.TerraCanvas
{
    public class TerraCanvasRandomizer : MonoBehaviour
    {
        // Singleton instance
        public static TerraCanvasRandomizer Instance { get; private set; }

        [Tooltip("The shader keyword for the random offset, e.g., '_RandomOffset'.")] [SerializeField]
        private string _shaderRandomOffsetKeyword = "_RandomOffset";

        public Material material;

        [Tooltip("The seed for the terrain generation. Set to -1 for a random seed.")] [SerializeField]
        private int _seed = -1;

        // Use a private const for the seed range to avoid modification
        private const int SEED_RANGE_LIMIT = 3141; // fun fact , find where this arbitrary value came from
        private bool _isInitialized = false;
        public Vector2 CurrentOffset;

        private void Awake()
        {
            // Enforce the singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                // Optionally, make it persist across scene loads
                Initialize();
            }
        }

        private void Initialize()
        {
            // Ensure initialization only happens once
            if (_isInitialized) return;

            if (_seed == -1)
            {
                // Use system time or another truly random source for the initial seed
                _seed = Random.Range(-SEED_RANGE_LIMIT, SEED_RANGE_LIMIT);
            }

            // Initialize the global random state for the entire game
            Random.InitState(_seed);
            _isInitialized = true;
            CurrentOffset = new Vector2(Random.Range(-SEED_RANGE_LIMIT, SEED_RANGE_LIMIT),
                Random.Range(-SEED_RANGE_LIMIT, SEED_RANGE_LIMIT));
            Debug.Log($"TerrainRandomizer initialized with seed: {_seed}");
        }

        // Public method for a terrain to get its unique offset
        public void RegisterAndRandomizeTerrain(Terrain terrain)
        {
            if (terrain == null)
            {
                Debug.LogError("Attempted to register a null terrain.");
                return;
            }

            CurrentOffset += new Vector2(terrain.transform.position.x, terrain.transform.position.z);

            // This sets a global shader variable.
            material.SetVector(_shaderRandomOffsetKeyword, CurrentOffset);
        }
    }
}