// Assets/Scripts/VoxelizeStage.cs
using UnityEngine;

[ExecuteAlways]
public class VoxelizeStage : MonoBehaviour
{
    public Vector3 worldOrigin;        // ステージ最小コーナー
    public Vector3 worldSize;          // ステージ全体サイズ
    public int gridX=64, gridY=32, gridZ=64;
    public RenderTexture occupancyRT;  // 3D R8_UNorm

    void Start()
    {
        
    }
}