using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public GameObject tilePrefab;
    public int width = 10;
    public int height = 10;

    [Header("Checkerboard Colors")]
    public Color colorA = new Color(0.53f, 0.77f, 0.40f); // light green
    public Color colorB = new Color(0.30f, 0.55f, 0.20f); // dark green

    private Material matA;
    private Material matB;

    void Start()
    {
        // Create two materials from the prefab's existing material as base
        Material baseMat = tilePrefab.GetComponent<Renderer>()?.sharedMaterial;
        matA = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
        matB = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
        matA.color = colorA;
        matB.color = colorB;

        GenerateGrid();
    }

    void GenerateGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 pos = new Vector3(x, 0, z);
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

                Renderer r = tile.GetComponent<Renderer>();
                if (r != null)
                    r.material = (x + z) % 2 == 0 ? matA : matB;
            }
        }
    }
}
