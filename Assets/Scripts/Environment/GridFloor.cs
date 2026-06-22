using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public sealed class GridFloor : MonoBehaviour
{
    private const int TextureSize = 128;

    [SerializeField] private float size = 960f;
    [SerializeField] private float tileSize = 8f;
    [SerializeField] private int lineWidth = 3;
    [SerializeField] private Color baseColor = new Color(0.82f, 0.25f, 0.09f, 1f);
    [SerializeField] private Color gridColor = new Color(1f, 0.86f, 0.72f, 1f);

    private Material materialInstance;
    private Mesh meshInstance;
    private Texture2D gridTexture;

    public void Configure(float floorSize, float gridTileSize)
    {
        size = Mathf.Max(1f, floorSize);
        tileSize = Mathf.Max(0.1f, gridTileSize);
        Apply();
    }

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        size = Mathf.Max(1f, size);
        tileSize = Mathf.Max(0.1f, tileSize);
        lineWidth = Mathf.Clamp(lineWidth, 1, TextureSize / 2);

        if (isActiveAndEnabled)
        {
            Apply();
        }
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Destroy(materialInstance);
        Destroy(meshInstance);
        Destroy(gridTexture);
    }

    private void Apply()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        meshFilter.sharedMesh = GetOrCreatePlaneMesh();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = true;

        Material gridMaterial = GetOrCreateMaterial();
        if (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.shader == null)
        {
            meshRenderer.sharedMaterial = gridMaterial;
        }
        else if (meshRenderer.sharedMaterial != gridMaterial && meshRenderer.sharedMaterial.name.Contains("Grid Floor"))
        {
            meshRenderer.sharedMaterial = gridMaterial;
        }

        transform.localScale = new Vector3(size, 1f, size);
        boxCollider.center = new Vector3(0f, -0.05f, 0f);
        boxCollider.size = new Vector3(1f, 0.1f, 1f);
        boxCollider.isTrigger = false;
    }

    private Mesh GetOrCreatePlaneMesh()
    {
        if (meshInstance == null)
        {
            meshInstance = new Mesh
            {
                name = "Grid Floor Mesh",
                hideFlags = Application.isPlaying ? HideFlags.DontSave : HideFlags.None
            };
        }
        else
        {
            meshInstance.Clear();
        }

        meshInstance.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f)
        };

        meshInstance.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(size / tileSize, 0f),
            new Vector2(0f, size / tileSize),
            new Vector2(size / tileSize, size / tileSize)
        };

        meshInstance.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        meshInstance.RecalculateNormals();
        meshInstance.RecalculateBounds();
        return meshInstance;
    }

    private Material GetOrCreateMaterial()
    {
        if (materialInstance == null || materialInstance.shader == null)
        {
            materialInstance = new Material(GetGridShader())
            {
                name = "Grid Floor Material",
                hideFlags = Application.isPlaying ? HideFlags.DontSave : HideFlags.None
            };
        }

        if (gridTexture == null)
        {
            gridTexture = CreateGridTexture();
        }

        SetMaterialTexture(materialInstance, gridTexture);
        return materialInstance;
    }

    private static Shader GetGridShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        return shader;
    }

    private static void SetMaterialTexture(Material material, Texture2D texture)
    {
        material.mainTexture = texture;
        material.mainTextureScale = Vector2.one;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
    }

    private Texture2D CreateGridTexture()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime Grid Floor Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = Application.isPlaying ? HideFlags.DontSave : HideFlags.None
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                bool isLine = x < lineWidth || y < lineWidth;
                pixels[y * TextureSize + x] = isLine ? gridColor : baseColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
