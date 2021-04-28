using UnityEngine;
using System.Collections.Generic;

public class TerrainGenerator : MonoBehaviour
{
    public GameObject windAreaPrefab;
    public GameObject speedAreaPrefab;

    const float viewerMoveThresholdForChunkUpdate = 25f;

    const float sqrViewerMoveThresholdForChunkUpdate =
        viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    public SettingsConfig settings;

    public int colliderLODIndex;
    public LODInfo[] detailLevels;

    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureSettings;

    public Transform viewer;
    public Material mapMaterial;

    private Vector2 viewerPosition;
    private Vector2 viewerPositionOld;

    private float meshWorldSize;
    private int chunksVisibleInViewDst;

    public SoundManager soundManager;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> visibleTerrainChunks = new List<TerrainChunk>();

    public GliderController player;

    void Start()
    {
        heightMapSettings.noiseSettings.seed = settings.seed;
        textureSettings.ApplyToMaterial(mapMaterial);
        textureSettings.UpdateMeshHeights(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        meshWorldSize = meshSettings.meshWorldSize;
        chunksVisibleInViewDst = Mathf.RoundToInt(settings.renderDistance / meshWorldSize);

        UpdateVisibleChunks();
    }

    private void Update()
    {
        for (var i = 0; i < detailLevels.Length; i++)
        {
            var denominator = detailLevels.Length - i;
            detailLevels[i].visibleDstThreshold = settings.renderDistance / denominator;
            detailLevels[i].lod = Mathf.Clamp(settings.mapQuality - denominator + 1, 0, 4);
        }

        var position = viewer.position;
        viewerPosition = new Vector2(position.x, position.z);

        if (viewerPosition != viewerPositionOld)
        {
            foreach (TerrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollisionMesh();
            }
        }

        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        var flat = Mathf.Abs(viewedChunkCoord.y) < 4 && Mathf.Abs(viewedChunkCoord.x) < 4;

                        var newChunk = new TerrainChunk(viewedChunkCoord, heightMapSettings, meshSettings,
                            detailLevels, colliderLODIndex, transform, viewer, mapMaterial, player);
                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
                        newChunk.windPrefab = windAreaPrefab;
                        newChunk.speedPrefab = speedAreaPrefab;
                        newChunk.soundManager = soundManager;
                        newChunk.onVisibilityChanged += OnTerrainChunkVisibilityChanged;
                        newChunk.Load(flat);
                    }
                }
            }
        }
    }

    void OnTerrainChunkVisibilityChanged(TerrainChunk chunk, bool isVisible)
    {
        if (isVisible)
        {
            visibleTerrainChunks.Add(chunk);
        }
        else
        {
            visibleTerrainChunks.Remove(chunk);
        }
    }

    public void ClearAllTerrain()
    {
        Random.InitState(settings.seed);
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        heightMapSettings.noiseSettings.seed = settings.seed;
        chunksVisibleInViewDst = Mathf.RoundToInt(settings.renderDistance / meshWorldSize);
        terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
        visibleTerrainChunks = new List<TerrainChunk>();
        UpdateVisibleChunks();
    }
}

[System.Serializable]
public struct LODInfo
{
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;

    public float visibleDstThreshold;
    
    public float SqrVisibleDstThreshold => visibleDstThreshold * visibleDstThreshold;
}