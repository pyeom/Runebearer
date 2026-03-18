using System.Collections.Generic;
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public static GridGenerator Instance { get; private set; }
    public HashSet<Vector2Int> WalkableTiles { get; private set; } = new HashSet<Vector2Int>();

    [Header("Tile Settings")]
    public GameObject tilePrefab;
    public int width = 10;
    public int height = 10;

    [Header("Procedural Generation")]
    [Tooltip("Scale of the Perlin noise — smaller = larger landmasses")]
    public float noiseScale = 0.35f;
    [Range(0f, 1f)]
    [Tooltip("Noise values above this threshold place a tile. Lower = more tiles.")]
    public float noiseThreshold = 0.35f;
    [Tooltip("Fixed seed for reproducible maps. Ignored when randomSeed is true.")]
    public int seed = 0;
    public bool randomSeed = true;

    [Header("Checkerboard Colors")]
    public Color colorA = new Color(0.53f, 0.77f, 0.40f); // light green
    public Color colorB = new Color(0.30f, 0.55f, 0.20f); // dark green

    [Header("Player Spawn")]
    [Tooltip("The player GameObject to place on the first generated tile.")]
    public GameObject player;
    [Tooltip("Height offset above the tile surface to place the player.")]
    public float playerHeightOffset = 0.7f;

    [HideInInspector] public int previewSeed;

    private Material matA;
    private Material matB;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Material baseMat = tilePrefab.GetComponent<Renderer>()?.sharedMaterial;
        matA = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
        matB = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
        matA.color = colorA;
        matB.color = colorB;

        if (randomSeed)
            seed = Random.Range(0, 999999);

        GenerateMap();
    }

    // Pure data: returns (kept tiles, discarded tiles) — no GameObjects touched.
    // Safe to call from editor code without entering Play mode.
    public (HashSet<Vector2Int> kept, HashSet<Vector2Int> discarded) ComputeLayout(int useSeed)
    {
        float offsetX = useSeed * 0.1f;
        float offsetZ = useSeed * 0.1f;

        var candidates = new HashSet<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
            {
                float noise = Mathf.PerlinNoise(x * noiseScale + offsetX, z * noiseScale + offsetZ);
                if (noise >= noiseThreshold)
                    candidates.Add(new Vector2Int(x, z));
            }

        var visited = new HashSet<Vector2Int>();
        var largest = new List<Vector2Int>();
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var start in candidates)
        {
            if (visited.Contains(start)) continue;

            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var dir in dirs)
                {
                    var neighbor = current + dir;
                    if (candidates.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (component.Count > largest.Count)
                largest = component;
        }

        var kept = new HashSet<Vector2Int>(largest);
        var discarded = new HashSet<Vector2Int>(candidates);
        discarded.ExceptWith(kept);
        return (kept, discarded);
    }

    void GenerateMap()
    {
        var (largest, _) = ComputeLayout(seed);

        bool firstTileSpawned = false;
        foreach (var cell in largest)
        {
            Vector3 pos = new Vector3(cell.x, 0f, cell.y);
            GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            Renderer r = tile.GetComponent<Renderer>();
            if (r != null)
                r.material = (cell.x + cell.y) % 2 == 0 ? matA : matB;

            WalkableTiles.Add(cell);

            if (!firstTileSpawned)
            {
                firstTileSpawned = true;
                SpawnPlayerOnTile(tile);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        int drawSeed = randomSeed ? previewSeed : seed;
        var (kept, discarded) = ComputeLayout(drawSeed);
        Vector3 origin = transform.position;

        // Discarded islands — red
        Gizmos.color = new Color(0.85f, 0.25f, 0.25f, 0.55f);
        foreach (var cell in discarded)
            Gizmos.DrawCube(origin + new Vector3(cell.x, 0f, cell.y), new Vector3(0.9f, 0.05f, 0.9f));

        // Kept tiles — green
        Gizmos.color = new Color(0.35f, 0.85f, 0.35f, 0.75f);
        foreach (var cell in kept)
            Gizmos.DrawCube(origin + new Vector3(cell.x, 0f, cell.y), new Vector3(0.9f, 0.05f, 0.9f));
    }
#endif

    void SpawnPlayerOnTile(GameObject tile)
    {
        if (player == null) return;

        Renderer r = tile.GetComponent<Renderer>();
        float tileTop = r != null ? r.bounds.max.y : tile.transform.position.y;

        player.transform.position = new Vector3(
            tile.transform.position.x,
            tileTop + playerHeightOffset,
            tile.transform.position.z
        );
    }
}
