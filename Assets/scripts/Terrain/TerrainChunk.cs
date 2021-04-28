﻿using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk
{
    public GameObject windPrefab;
    public GameObject speedPrefab;

    const float colliderGenerationDistanceThreshold = 5;
    public event System.Action<TerrainChunk, bool> onVisibilityChanged;
    public Vector2 coord;

    public GameObject meshObject;
    Vector2 sampleCentre;
    Bounds bounds;

    MeshRenderer meshRenderer;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    CollisionLogic collisionLogic;

    LODInfo[] detailLevels;
    LODMesh[] lodMeshes;
    int colliderLODIndex;

    public HeightMap heightMap;
    bool heightMapReceived = false;
    bool structuresGenerated = false;
    int previousLODIndex = -1;
    bool hasSetCollider;
    float maxViewDst;
    public bool flat = false;

    public SoundManager soundManager;
    public HeightMapSettings heightMapSettings;
    MeshSettings meshSettings;
    Transform viewer;

    public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings,
        LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Transform viewer, Material material,
        GliderController player)
    {
        this.coord = coord;
        this.detailLevels = detailLevels;
        this.colliderLODIndex = colliderLODIndex;
        this.heightMapSettings = heightMapSettings;
        this.meshSettings = meshSettings;
        this.viewer = viewer;

        sampleCentre = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
        Vector2 position = coord * meshSettings.meshWorldSize;
        bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

        meshObject = new GameObject("Terrain Chunk");
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshCollider = meshObject.AddComponent<MeshCollider>();
        collisionLogic = meshObject.AddComponent<CollisionLogic>();
        meshRenderer.material = material;
        collisionLogic.player = player;
        meshObject.tag = "Terrain";
        meshObject.layer = 3;

        meshObject.transform.position = new Vector3(position.x, 0, position.y);
        meshObject.transform.parent = parent;
        SetVisible(false);

        lodMeshes = new LODMesh[detailLevels.Length];
        for (int i = 0; i < detailLevels.Length; i++)
        {
            lodMeshes[i] = new LODMesh(detailLevels[i].lod);
            lodMeshes[i].updateCallback += UpdateTerrainChunk;
            if (i == colliderLODIndex)
            {
                lodMeshes[i].updateCallback += UpdateCollisionMesh;
            }
        }

        maxViewDst = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
    }

    public void Load(bool flat)
    {
        this.flat = flat;
        ThreadedDataRequester.RequestData(
            () => HeightMapGenerator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine,
                heightMapSettings, sampleCentre, flat), OnHeightMapReceived);
    }

    public void LoadStructures()
    {
        ThreadedDataRequester.RequestData(() => StructureManager.GenerateStructures(this), OnStructuresRecieved);
    }

    private void OnStructuresRecieved(object structDictObj)
    {
        System.Random rand = new System.Random(heightMapSettings.noiseSettings.seed);
        Dictionary<string, Vector2> structureDictionary = (Dictionary<string, Vector2>) structDictObj;
        int numOfStructs = structureDictionary.Count;
        for (int i = 0; i < numOfStructs; i++)
        {
            if (structureDictionary.TryGetValue($"{i}Wind", out Vector2 windCoords))
            {
                Vector2 pos = new Vector2(meshObject.transform.position.x + (windCoords.x * meshSettings.meshScale),
                    meshObject.transform.position.z + (windCoords.y * meshSettings.meshScale));
                GameObject obj = Object.Instantiate(windPrefab, new Vector3(pos.x, -1, pos.y), Quaternion.identity);
                obj.transform.parent = meshObject.transform;
                WindArea windArea = obj.GetComponent<WindArea>();
                windArea.soundManager = soundManager;
            }
            else if (structureDictionary.TryGetValue($"{i}Speed", out Vector2 boostCoords))
            {
                Vector2 pos = new Vector2(meshObject.transform.position.x + (boostCoords.x * meshSettings.meshScale),
                    meshObject.transform.position.z + (boostCoords.y * meshSettings.meshScale));
                GameObject obj = Object.Instantiate(speedPrefab, new Vector3(pos.x, heightMap.maxValue + 20, pos.y),
                    Quaternion.Euler(0, (float) rand.Next(), 0));
                obj.transform.parent = meshObject.transform;
            }
        }
    }

    void OnHeightMapReceived(object heightMapObject)
    {
        this.heightMap = (HeightMap) heightMapObject;
        heightMapReceived = true;

        LoadStructures();

        UpdateTerrainChunk();
    }

    Vector2 viewerPosition
    {
        get { return new Vector2(viewer.position.x, viewer.position.z); }
    }


    public void UpdateTerrainChunk()
    {
        if (heightMapReceived)
        {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));

            bool wasVisible = IsVisible();
            bool visible = viewerDstFromNearestEdge <= maxViewDst;

            if (visible)
            {
                int lodIndex = 0;

                for (int i = 0; i < detailLevels.Length - 1; i++)
                {
                    if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
                    {
                        lodIndex = i + 1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex)
                {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh)
                    {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(heightMap, meshSettings);
                    }
                }
            }

            if (wasVisible != visible)
            {
                SetVisible(visible);
                if (onVisibilityChanged != null)
                {
                    onVisibilityChanged(this, visible);
                }
            }
        }
    }

    public void UpdateCollisionMesh()
    {
        if (!hasSetCollider)
        {
            float sqrDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);

            if (sqrDstFromViewerToEdge < detailLevels[colliderLODIndex].sqrVisibleDstThreshold)
            {
                if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
                {
                    lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
                }
            }

            if (sqrDstFromViewerToEdge < colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold)
            {
                if (lodMeshes[colliderLODIndex].hasMesh)
                {
                    meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                    hasSetCollider = true;
                }
            }
        }
    }

    public void SetVisible(bool visible)
    {
        meshObject.SetActive(visible);
    }

    public bool IsVisible()
    {
        return meshObject.activeSelf;
    }
}

class LODMesh
{
    public Mesh mesh;
    public bool hasRequestedMesh;
    public bool hasMesh;
    int lod;
    public event System.Action updateCallback;

    public LODMesh(int lod)
    {
        this.lod = lod;
    }

    void OnMeshDataReceived(object meshDataObject)
    {
        mesh = ((MeshData) meshDataObject).CreateMesh();
        hasMesh = true;

        updateCallback();
    }

    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
    {
        hasRequestedMesh = true;
        ThreadedDataRequester.RequestData(() => MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, lod),
            OnMeshDataReceived);
    }
}