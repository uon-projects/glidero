using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StructureManager
{
    static float windSpawnChance = 5e-2f;
    static float speedSpawnChance = 5e-2f;

    static System.Random randomNumberGenerator = null;

    public static Dictionary<string, Vector2> GenerateStructures(TerrainChunk chunk)
    {
        randomNumberGenerator ??= new System.Random(chunk.heightMapSettings.noiseSettings.seed);
        float rand = (float) randomNumberGenerator.NextDouble();
        Dictionary<string, Vector2> structures = new Dictionary<string, Vector2>();
        float[,] hm = chunk.heightMap.values;
        for (int i = 0; i < hm.GetLength(0); i++)
        {
            for (int j = 0; j < hm.GetLength(1); j++)
            {
                Vector2 coords = new Vector2(i > hm.GetLength(0) / 2 ? i - hm.GetLength(0) : i,
                    j > hm.GetLength(0) / 2 ? j - hm.GetLength(0) : j);
                if (hm[i, j] == chunk.heightMap.minValue && rand < windSpawnChance && !chunk.flat)
                {
                    structures.Add($"{structures.Count}Wind", coords);
                }

                if (hm[i, j] == chunk.heightMap.maxValue && rand < speedSpawnChance && !chunk.flat)
                {
                    structures.Add($"{structures.Count}Speed", coords);
                }
            }
        }

        return structures;
    }
}