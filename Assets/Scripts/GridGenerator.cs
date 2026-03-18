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

    void GenerateMap()
    {
        float offsetX = seed * 0.1f;
        float offsetZ = seed * 0.1f;
        bool firstTileSpawned = false;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                float noise = Mathf.PerlinNoise(x * noiseScale + offsetX, z * noiseScale + offsetZ);
                if (noise < noiseThreshold)
                    continue;

                Vector3 pos = new Vector3(x, 0f, z);
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

                Renderer r = tile.GetComponent<Renderer>();
                if (r != null)
                    r.material = (x + z) % 2 == 0 ? matA : matB;

                WalkableTiles.Add(new Vector2Int(x, z));

                if (!firstTileSpawned)
                {
                    firstTileSpawned = true;
                    SpawnPlayerOnTile(tile);
                }
            }
        }
    }

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
