using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mesh generator for the Marching Cubes method
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCube : MonoBehaviour
{
    public const int Capacity = VoxelCount * 16;
    public const float CubeSize = 2.0f;
    public const float CubeExtent = CubeSize / 2.0f;

    // odd number is recommended
    public const int PointCountCommon = 21;
    public const int PointCountX = 31;
    public const int PointCountY = PointCountCommon;
    public const int PointCountZ = PointCountCommon;

    public const int PointCountPlaneYZ = PointCountY * PointCountZ;

    public const int PointCountExtentX = (PointCountX - 1) / 2;
    public const int PointCountExtentY = (PointCountY - 1) / 2;
    public const int PointCountExtentZ = (PointCountZ - 1) / 2;

    public const int CubeCountX = PointCountX - 1;
    public const int CubeCountY = PointCountY - 1;
    public const int CubeCountZ = PointCountZ - 1;

    public const int VoxelCount = PointCountX * PointCountY * PointCountZ;
    public readonly float[] voxel = new float[VoxelCount];

    public const float VolumeSizeX = CubeCountX * CubeSize;
    public const float VolumeSizeY = CubeCountY * CubeSize;
    public const float VolumeSizeZ = CubeCountZ * CubeSize;
    public readonly Vector3 VolumeSize = new Vector3(VolumeSizeX, VolumeSizeY, VolumeSizeZ);

    public const float VolumeExtentX = CubeCountX * CubeExtent;
    public const float VolumeExtentY = CubeCountY * CubeExtent;
    public const float VolumeExtentZ = CubeCountZ * CubeExtent;

    public readonly Vector3[] vertices = new Vector3[Capacity];
    //public readonly Vector3[] normals = new Vector3[Capacity];
    public readonly int[] triangles = new int[Capacity];

    public readonly Vector3 firstCubePosition = new Vector3(
        (PointCountExtentX * -CubeSize) + CubeExtent,
        (PointCountExtentY * -CubeSize) + CubeExtent,
        (PointCountExtentZ * -CubeSize) + CubeExtent
    );

    public readonly Vector3[] cubeCornerPoints = new Vector3[CubeCornerCount]
    {
        new Vector3(-CubeExtent, +CubeExtent, -CubeExtent),
        new Vector3(+CubeExtent, +CubeExtent, -CubeExtent),
        new Vector3(+CubeExtent, -CubeExtent, -CubeExtent),
        new Vector3(-CubeExtent, -CubeExtent, -CubeExtent),

        new Vector3(-CubeExtent, +CubeExtent, +CubeExtent),
        new Vector3(+CubeExtent, +CubeExtent, +CubeExtent),
        new Vector3(+CubeExtent, -CubeExtent, +CubeExtent),
        new Vector3(-CubeExtent, -CubeExtent, +CubeExtent),
    };

    public readonly Vector3[] cubeEdgePoints = new Vector3[CubeEdgeCount]
    {
        new Vector3(0, +CubeExtent, -CubeExtent),
        new Vector3(+CubeExtent, 0, -CubeExtent),
        new Vector3(0, -CubeExtent, -CubeExtent),
        new Vector3(-CubeExtent, 0, -CubeExtent),

        new Vector3(0, +CubeExtent, +CubeExtent),
        new Vector3(+CubeExtent, 0, +CubeExtent),
        new Vector3(0, -CubeExtent, +CubeExtent),
        new Vector3(-CubeExtent, 0, +CubeExtent),

        new Vector3(-CubeExtent, +CubeExtent, 0),
        new Vector3(+CubeExtent, +CubeExtent, 0),
        new Vector3(+CubeExtent, -CubeExtent, 0),
        new Vector3(-CubeExtent, -CubeExtent, 0),
    };

    public readonly int[] cubeCornerToVolumeIndexOffset = new int[CubeCornerCount]
    {
        (0 * PointCountPlaneYZ) + (1 * PointCountZ) + 0,
        (1 * PointCountPlaneYZ) + (1 * PointCountZ) + 0,
        (1 * PointCountPlaneYZ) + (0 * PointCountZ) + 0,
        (0 * PointCountPlaneYZ) + (0 * PointCountZ) + 0,
        (0 * PointCountPlaneYZ) + (1 * PointCountZ) + 1,
        (1 * PointCountPlaneYZ) + (1 * PointCountZ) + 1,
        (1 * PointCountPlaneYZ) + (0 * PointCountZ) + 1,
        (0 * PointCountPlaneYZ) + (0 * PointCountZ) + 1,
    };

    public readonly Vector3[] voxelVolumeCornerPoints = new Vector3[CubeCornerCount]
    {
        new Vector3(-VolumeExtentX, +VolumeExtentY, -VolumeExtentZ),
        new Vector3(+VolumeExtentX, +VolumeExtentY, -VolumeExtentZ),
        new Vector3(+VolumeExtentX, -VolumeExtentY, -VolumeExtentZ),
        new Vector3(-VolumeExtentX, -VolumeExtentY, -VolumeExtentZ),

        new Vector3(-VolumeExtentX, +VolumeExtentY, +VolumeExtentZ),
        new Vector3(+VolumeExtentX, +VolumeExtentY, +VolumeExtentZ),
        new Vector3(+VolumeExtentX, -VolumeExtentY, +VolumeExtentZ),
        new Vector3(-VolumeExtentX, -VolumeExtentY, +VolumeExtentZ),
    };

    public Vector3 noiseSpeed = Vector3.right * 0.1f;
    public Vector3 noiseScale = Vector3.one;
    public Vector3 noiseOffset;
    public float fpsLimit = 30.0f;

    private Mesh sharedMesh;
    private MeshFilter meshFilter;
    private float lastUpdateTime = 0.0f;

    public void SetVoxelValue(int xi, int yi, int zi, float value)
    {
        var vi =
            xi * PointCountPlaneYZ +
            yi * PointCountZ +
            zi;

        voxel[vi] = value;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, VolumeSize);
    }

    private void Awake()
    {
        sharedMesh = new Mesh();

        // Up to 4294967296 vertices, not guaranteed on all platforms.
        sharedMesh.indexFormat = IndexFormat.UInt32;

        meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = sharedMesh;
    }

    private void Update()
    {
        float interval = fpsLimit > 0.0f ? 1.0f / fpsLimit : 0.0f;
        if (Time.time - lastUpdateTime > interval)
        {
            UpdateVolume();
            UpdateMesh();

            lastUpdateTime = Time.time;
        }
    }

    private void UpdateVolume()
    {
        Vector3 offset = noiseSpeed * Time.time;

        for (int xi = 0; xi < PointCountX; xi++)
        {
            float xnoise = (float)xi / (PointCountX - 1) * noiseScale.x;

            for (int yi = 0; yi < PointCountY; yi++)
            {
                float ynoise = (float)yi / (PointCountY - 1) * noiseScale.y;

                for (int zi = 0; zi < PointCountZ; zi++)
                {
                    float znoise = (float)zi / (PointCountZ - 1) * noiseScale.z;

                    var v = Mathf.Clamp01(Perlin3D(
                        xnoise - noiseOffset.x + offset.x,
                        ynoise - noiseOffset.y + offset.y,
                        znoise - noiseOffset.z + offset.z));

                    SetVoxelValue(xi, yi, zi, v);
                }
            }
        }
    }

    private void UpdateMesh()
    {
        int vCount = 0;
        //int nCount = 0;
        int tCount = 0;

        for (int cxi = 0; cxi < CubeCountX; cxi++)
        {
            var voxelBaseX = cxi * PointCountPlaneYZ;

            for (int cyi = 0; cyi < CubeCountY; cyi++)
            {
                var voxelBaseXY = voxelBaseX + cyi * PointCountZ;

                for (int czi = 0; czi < CubeCountZ; czi++)
                {
                    var cubeCenterOffset = firstCubePosition + new Vector3(
                        cxi * CubeSize,
                        cyi * CubeSize,
                        czi * CubeSize
                    );

                    // Get voxel index
                    var vi = voxelBaseXY + czi;

                    byte cornerOccupiedFlags = 0b0000_0000;
                    for (int cci = 0; cci < CubeCornerCount; cci++)
                    {
                        const float OccupiedThresh = 0.5f;
                        var v = voxel[vi + cubeCornerToVolumeIndexOffset[cci]];
                        if (v > OccupiedThresh)
                        {
                            cornerOccupiedFlags += (byte)(1 << cci);
                        }
                    }

                    const int Triangle = 3;
                    var edegSeqs = PatternToEdgeSequence[cornerOccupiedFlags];
                    for (int esi = 0; esi < edegSeqs.Length; esi += Triangle)
                    {
                        for (int ti = 0; ti < Triangle; ti++)
                        {
                            var edge = edegSeqs[esi + ti];
                            var edgePoint = cubeEdgePoints[edge];

                            var pos = edgePoint + cubeCenterOffset;
                            vertices[vCount++] = pos;
                            //normals[nCount++] = normal;
                            triangles[tCount] = tCount;
                            tCount++;
                        }
                    }
                }
            }
        }

        // Set mesh data
        sharedMesh.Clear();
        sharedMesh.SetVertices(vertices, 0, vCount);
        //sharedMesh.SetNormals(normals, 0, nCount);
        sharedMesh.SetTriangles(triangles, 0, tCount, 0);
        sharedMesh.RecalculateNormals();
    }

    public static float Perlin3D(float x, float y, float z)
    {
        float AB = Mathf.PerlinNoise(x, y);
        float BC = Mathf.PerlinNoise(y, z);
        float AC = Mathf.PerlinNoise(x, z);

        float BA = Mathf.PerlinNoise(y, x);
        float CB = Mathf.PerlinNoise(z, y);
        float CA = Mathf.PerlinNoise(z, x);

        float ABC = AB + BC + AC + BA + CB + CA;
        return ABC / 6.0f;
    }

    public const int CubeCornerCount = 8;
    public const int CubeEdgeCount = 12;

    public static readonly int[][] cornerToEdges = new int[CubeCornerCount][]
    {
        new int[]{ 0, 3, 8 }, // corner[0] -> triangle(edge[0], edge[3], edge[8])
        new int[]{ 0, 1, 9 },
        new int[]{ 1, 2, 10 },
        new int[]{ 2, 3, 11 },

        new int[]{ 4, 7, 8 },
        new int[]{ 4, 5, 9 },
        new int[]{ 5, 6, 10 },
        new int[]{ 6, 7, 11 },
    };

    // All 256 pattern of edges for triangle generation.
    public static readonly int[][] PatternToEdgeSequence = new int[256][]
    {
        new int[] { },                 // pattern 00000000: empty
        new int[] { 0, 8, 3},          // pattern 00000001: point uses edge 0-8-3 (1 triangle)
        new int[] { 0, 1, 9},
        new int[] { 1, 8, 3, 9, 8, 1}, // pattern 00000011: point uses edge 1-8-3 and 9-8-1 (2 triangles)
        new int[] { 1, 2, 10},
        new int[] { 0, 8, 3, 1, 2, 10},
        new int[] { 9, 2, 10, 0, 2, 9},
        new int[] { 2, 8, 3, 2, 10, 8, 10, 9, 8},
        new int[] { 3, 11, 2},
        new int[] { 0, 11, 2, 8, 11, 0},
        new int[] { 1, 9, 0, 2, 3, 11},
        new int[] { 1, 11, 2, 1, 9, 11, 9, 8, 11},
        new int[] { 3, 10, 1, 11, 10, 3},
        new int[] { 0, 10, 1, 0, 8, 10, 8, 11, 10},
        new int[] { 3, 9, 0, 3, 11, 9, 11, 10, 9},
        new int[] { 9, 8, 10, 10, 8, 11},
        new int[] { 4, 7, 8},
        new int[] { 4, 3, 0, 7, 3, 4},
        new int[] { 0, 1, 9, 8, 4, 7},
        new int[] { 4, 1, 9, 4, 7, 1, 7, 3, 1},
        new int[] { 1, 2, 10, 8, 4, 7},
        new int[] { 3, 4, 7, 3, 0, 4, 1, 2, 10},
        new int[] { 9, 2, 10, 9, 0, 2, 8, 4, 7},
        new int[] { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4},
        new int[] { 8, 4, 7, 3, 11, 2},
        new int[] { 11, 4, 7, 11, 2, 4, 2, 0, 4},
        new int[] { 9, 0, 1, 8, 4, 7, 2, 3, 11},
        new int[] { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1},
        new int[] { 3, 10, 1, 3, 11, 10, 7, 8, 4},
        new int[] { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4},
        new int[] { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3},
        new int[] { 4, 7, 11, 4, 11, 9, 9, 11, 10},
        new int[] { 9, 5, 4},
        new int[] { 9, 5, 4, 0, 8, 3},
        new int[] { 0, 5, 4, 1, 5, 0},
        new int[] { 8, 5, 4, 8, 3, 5, 3, 1, 5},
        new int[] { 1, 2, 10, 9, 5, 4},
        new int[] { 3, 0, 8, 1, 2, 10, 4, 9, 5},
        new int[] { 5, 2, 10, 5, 4, 2, 4, 0, 2},
        new int[] { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8},
        new int[] { 9, 5, 4, 2, 3, 11},
        new int[] { 0, 11, 2, 0, 8, 11, 4, 9, 5},
        new int[] { 0, 5, 4, 0, 1, 5, 2, 3, 11},
        new int[] { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5},
        new int[] { 10, 3, 11, 10, 1, 3, 9, 5, 4},
        new int[] { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10},
        new int[] { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3},
        new int[] { 5, 4, 8, 5, 8, 10, 10, 8, 11},
        new int[] { 9, 7, 8, 5, 7, 9},
        new int[] { 9, 3, 0, 9, 5, 3, 5, 7, 3},
        new int[] { 0, 7, 8, 0, 1, 7, 1, 5, 7},
        new int[] { 1, 5, 3, 3, 5, 7},
        new int[] { 9, 7, 8, 9, 5, 7, 10, 1, 2},
        new int[] { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3},
        new int[] { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2},
        new int[] { 2, 10, 5, 2, 5, 3, 3, 5, 7},
        new int[] { 7, 9, 5, 7, 8, 9, 3, 11, 2},
        new int[] { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11},
        new int[] { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7},
        new int[] { 11, 2, 1, 11, 1, 7, 7, 1, 5},
        new int[] { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11},
        new int[] { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0},
        new int[] { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0},
        new int[] { 11, 10, 5, 7, 11, 5},
        new int[] { 10, 6, 5},
        new int[] { 0, 8, 3, 5, 10, 6},
        new int[] { 9, 0, 1, 5, 10, 6},
        new int[] { 1, 8, 3, 1, 9, 8, 5, 10, 6},
        new int[] { 1, 6, 5, 2, 6, 1},
        new int[] { 1, 6, 5, 1, 2, 6, 3, 0, 8},
        new int[] { 9, 6, 5, 9, 0, 6, 0, 2, 6},
        new int[] { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8},
        new int[] { 2, 3, 11, 10, 6, 5},
        new int[] { 11, 0, 8, 11, 2, 0, 10, 6, 5},
        new int[] { 0, 1, 9, 2, 3, 11, 5, 10, 6},
        new int[] { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11},
        new int[] { 6, 3, 11, 6, 5, 3, 5, 1, 3},
        new int[] { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6},
        new int[] { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9},
        new int[] { 6, 5, 9, 6, 9, 11, 11, 9, 8},
        new int[] { 5, 10, 6, 4, 7, 8},
        new int[] { 4, 3, 0, 4, 7, 3, 6, 5, 10},
        new int[] { 1, 9, 0, 5, 10, 6, 8, 4, 7},
        new int[] { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4},
        new int[] { 6, 1, 2, 6, 5, 1, 4, 7, 8},
        new int[] { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7},
        new int[] { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6},
        new int[] { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9},
        new int[] { 3, 11, 2, 7, 8, 4, 10, 6, 5},
        new int[] { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11},
        new int[] { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6},
        new int[] { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6},
        new int[] { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6},
        new int[] { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11},
        new int[] { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7},
        new int[] { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9},
        new int[] { 10, 4, 9, 6, 4, 10},
        new int[] { 4, 10, 6, 4, 9, 10, 0, 8, 3},
        new int[] { 10, 0, 1, 10, 6, 0, 6, 4, 0},
        new int[] { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10},
        new int[] { 1, 4, 9, 1, 2, 4, 2, 6, 4},
        new int[] { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4},
        new int[] { 0, 2, 4, 4, 2, 6},
        new int[] { 8, 3, 2, 8, 2, 4, 4, 2, 6},
        new int[] { 10, 4, 9, 10, 6, 4, 11, 2, 3},
        new int[] { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6},
        new int[] { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10},
        new int[] { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1},
        new int[] { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3},
        new int[] { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1},
        new int[] { 3, 11, 6, 3, 6, 0, 0, 6, 4},
        new int[] { 6, 4, 8, 11, 6, 8},
        new int[] { 7, 10, 6, 7, 8, 10, 8, 9, 10},
        new int[] { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10},
        new int[] { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0},
        new int[] { 10, 6, 7, 10, 7, 1, 1, 7, 3},
        new int[] { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7},
        new int[] { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9},
        new int[] { 7, 8, 0, 7, 0, 6, 6, 0, 2},
        new int[] { 7, 3, 2, 6, 7, 2},
        new int[] { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7},
        new int[] { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7},
        new int[] { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11},
        new int[] { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1},
        new int[] { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6},
        new int[] { 0, 9, 1, 11, 6, 7},
        new int[] { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0},
        new int[] { 7, 11, 6},
        new int[] { 7, 6, 11},
        new int[] { 3, 0, 8, 11, 7, 6},
        new int[] { 0, 1, 9, 11, 7, 6},
        new int[] { 8, 1, 9, 8, 3, 1, 11, 7, 6},
        new int[] { 10, 1, 2, 6, 11, 7},
        new int[] { 1, 2, 10, 3, 0, 8, 6, 11, 7},
        new int[] { 2, 9, 0, 2, 10, 9, 6, 11, 7},
        new int[] { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8},
        new int[] { 7, 2, 3, 6, 2, 7},
        new int[] { 7, 0, 8, 7, 6, 0, 6, 2, 0},
        new int[] { 2, 7, 6, 2, 3, 7, 0, 1, 9},
        new int[] { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6},
        new int[] { 10, 7, 6, 10, 1, 7, 1, 3, 7},
        new int[] { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8},
        new int[] { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7},
        new int[] { 7, 6, 10, 7, 10, 8, 8, 10, 9},
        new int[] { 6, 8, 4, 11, 8, 6},
        new int[] { 3, 6, 11, 3, 0, 6, 0, 4, 6},
        new int[] { 8, 6, 11, 8, 4, 6, 9, 0, 1},
        new int[] { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6},
        new int[] { 6, 8, 4, 6, 11, 8, 2, 10, 1},
        new int[] { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6},
        new int[] { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9},
        new int[] { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3},
        new int[] { 8, 2, 3, 8, 4, 2, 4, 6, 2},
        new int[] { 0, 4, 2, 4, 6, 2},
        new int[] { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8},
        new int[] { 1, 9, 4, 1, 4, 2, 2, 4, 6},
        new int[] { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1},
        new int[] { 10, 1, 0, 10, 0, 6, 6, 0, 4},
        new int[] { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3},
        new int[] { 10, 9, 4, 6, 10, 4},
        new int[] { 4, 9, 5, 7, 6, 11},
        new int[] { 0, 8, 3, 4, 9, 5, 11, 7, 6},
        new int[] { 5, 0, 1, 5, 4, 0, 7, 6, 11},
        new int[] { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5},
        new int[] { 9, 5, 4, 10, 1, 2, 7, 6, 11},
        new int[] { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5},
        new int[] { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2},
        new int[] { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6},
        new int[] { 7, 2, 3, 7, 6, 2, 5, 4, 9},
        new int[] { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7},
        new int[] { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0},
        new int[] { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8},
        new int[] { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7},
        new int[] { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4},
        new int[] { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10},
        new int[] { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10},
        new int[] { 6, 9, 5, 6, 11, 9, 11, 8, 9},
        new int[] { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5},
        new int[] { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11},
        new int[] { 6, 11, 3, 6, 3, 5, 5, 3, 1},
        new int[] { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6},
        new int[] { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10},
        new int[] { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5},
        new int[] { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3},
        new int[] { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2},
        new int[] { 9, 5, 6, 9, 6, 0, 0, 6, 2},
        new int[] { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8},
        new int[] { 1, 5, 6, 2, 1, 6},
        new int[] { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6},
        new int[] { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0},
        new int[] { 0, 3, 8, 5, 6, 10},
        new int[] { 10, 5, 6},
        new int[] { 11, 5, 10, 7, 5, 11},
        new int[] { 11, 5, 10, 11, 7, 5, 8, 3, 0},
        new int[] { 5, 11, 7, 5, 10, 11, 1, 9, 0},
        new int[] { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1},
        new int[] { 11, 1, 2, 11, 7, 1, 7, 5, 1},
        new int[] { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11},
        new int[] { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7},
        new int[] { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2},
        new int[] { 2, 5, 10, 2, 3, 5, 3, 7, 5},
        new int[] { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5},
        new int[] { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2},
        new int[] { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2},
        new int[] { 1, 3, 5, 3, 7, 5},
        new int[] { 0, 8, 7, 0, 7, 1, 1, 7, 5},
        new int[] { 9, 0, 3, 9, 3, 5, 5, 3, 7},
        new int[] { 9, 8, 7, 5, 9, 7},
        new int[] { 5, 8, 4, 5, 10, 8, 10, 11, 8},
        new int[] { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0},
        new int[] { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5},
        new int[] { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4},
        new int[] { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8},
        new int[] { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11},
        new int[] { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5},
        new int[] { 9, 4, 5, 2, 11, 3},
        new int[] { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4},
        new int[] { 5, 10, 2, 5, 2, 4, 4, 2, 0},
        new int[] { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9},
        new int[] { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2},
        new int[] { 8, 4, 5, 8, 5, 3, 3, 5, 1},
        new int[] { 0, 4, 5, 1, 0, 5},
        new int[] { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5},
        new int[] { 9, 4, 5},
        new int[] { 4, 11, 7, 4, 9, 11, 9, 10, 11},
        new int[] { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11},
        new int[] { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11},
        new int[] { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4},
        new int[] { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2},
        new int[] { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3},
        new int[] { 11, 7, 4, 11, 4, 2, 2, 4, 0},
        new int[] { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4},
        new int[] { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9},
        new int[] { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7},
        new int[] { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10},
        new int[] { 1, 10, 2, 8, 7, 4},
        new int[] { 4, 9, 1, 4, 1, 7, 7, 1, 3},
        new int[] { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1},
        new int[] { 4, 0, 3, 7, 4, 3},
        new int[] { 4, 8, 7},
        new int[] { 9, 10, 8, 10, 11, 8},
        new int[] { 3, 0, 9, 3, 9, 11, 11, 9, 10},
        new int[] { 0, 1, 10, 0, 10, 8, 8, 10, 11},
        new int[] { 3, 1, 10, 11, 3, 10},
        new int[] { 1, 2, 11, 1, 11, 9, 9, 11, 8},
        new int[] { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9},
        new int[] { 0, 2, 11, 8, 0, 11},
        new int[] { 3, 2, 11},
        new int[] { 2, 3, 8, 2, 8, 10, 10, 8, 9},
        new int[] { 9, 10, 2, 0, 9, 2},
        new int[] { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8},
        new int[] { 1, 10, 2},
        new int[] { 1, 3, 8, 9, 1, 8},
        new int[] { 0, 9, 1},
        new int[] { 0, 3, 8},
        new int[] { } // pattern 11111111: empty (no intersections)
    };
}
