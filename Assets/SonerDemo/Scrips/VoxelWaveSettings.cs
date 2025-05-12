using UnityEngine;

[CreateAssetMenu(menuName = "Wave/Settings", fileName = "VoxelWaveSettings")]
public class VoxelWaveSettings : ScriptableObject
{
    [Header("World Grid")]
    public Vector3 worldOrigin;
    public Vector3 worldSize = new Vector3(64, 32, 64);
    public Vector3Int gridSize = new Vector3Int(64, 32, 64);
    [Header("Stage Geometry")]
    public LayerMask stageMask;

    [Header("Compute Shaders")]
    public ComputeShader waveBfsCS;
    public int bfsPasses = 128;

    [Header("Shader Globals")]
    public string globalOccupancyTex = "_OccupancyTex";
    public string globalDistanceTex  = "_DistanceTex";
    public string globalWorldOrigin  = "_WorldOrigin";
    public string globalWorldSize    = "_WorldSize";
    public string globalCellSize     = "_CellSize";
}