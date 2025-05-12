using UnityEngine;
using UnityEngine.Rendering;

public class VoxelWaveController : MonoBehaviour
{
    [SerializeField] VoxelWaveSettings settings;
    [Header("Wave Motion")]
    public float waveSpeed = 4f;      // 波が進む速さ（world 単位／秒）
    public float thickness = 1f;      // 波の幅（world 単位）

    float lastEmitTime;
    Texture3D        occupancyTex;
    RenderTexture    distanceRT;
    RenderTexture    distancePrevRT;
    int              initK, propK;
    Vector3          lastEmitPos;

    void OnEnable()
    {
        var gs = settings.gridSize;

        // 1) Create occupancy Texture3D
        occupancyTex = new Texture3D(gs.x, gs.y, gs.z, TextureFormat.R8, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        // 2) Create distance RenderTexture3D buffers
        distanceRT = new RenderTexture(gs.x, gs.y, 0, RenderTextureFormat.RFloat)
        {
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = gs.z,
            enableRandomWrite = true
        };
        distanceRT.Create();

        distancePrevRT = new RenderTexture(gs.x, gs.y, 0, RenderTextureFormat.RFloat)
        {
            dimension         = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth       = gs.z,
            enableRandomWrite = true
        };
        distancePrevRT.Create();
        
        distanceRT.filterMode      = FilterMode.Bilinear;
        distancePrevRT.filterMode  = FilterMode.Bilinear;
        distanceRT.wrapMode        = TextureWrapMode.Clamp;
        distancePrevRT.wrapMode    = TextureWrapMode.Clamp;
        // 3) Get compute kernels
        initK = settings.waveBfsCS.FindKernel("Init");
        propK = settings.waveBfsCS.FindKernel("Propagate");

        // 4) Voxelize stage geometry into occupancyTex
        VoxelizeStage();

        // 5) Bind globals
        Shader.SetGlobalTexture(settings.globalOccupancyTex, occupancyTex);
        Shader.SetGlobalTexture(settings.globalDistanceTex,  distanceRT);
        Shader.SetGlobalVector(settings.globalWorldOrigin, settings.worldOrigin);
        Shader.SetGlobalVector(settings.globalWorldSize,   settings.worldSize);
        float cell = Mathf.Max(
            settings.worldSize.x / gs.x,
            settings.worldSize.y / gs.y,
            settings.worldSize.z / gs.z
        );
        Shader.SetGlobalFloat(settings.globalCellSize, cell);
    }

    void VoxelizeStage()
    {
        var gs = settings.gridSize;
        Vector3 cellSize = new Vector3(
            settings.worldSize.x / gs.x,
            settings.worldSize.y / gs.y,
            settings.worldSize.z / gs.z
        );
        Vector3 halfExt = cellSize * 0.5f;
        var cols = new Color32[gs.x * gs.y * gs.z];
        int idx = 0;
        for (int z = 0; z < gs.z; z++)
        {
            for (int y = 0; y < gs.y; y++)
            {
                for (int x = 0; x < gs.x; x++)
                {
                    Vector3 center = settings.worldOrigin +
                        new Vector3((x + 0.5f) * cellSize.x,
                                    (y + 0.5f) * cellSize.y,
                                    (z + 0.5f) * cellSize.z);
                    bool hit = Physics.OverlapBox(
                        center, halfExt, Quaternion.identity, settings.stageMask
                    ).Length > 0;
                    cols[idx++] = hit
                        ? new Color32(255, 0, 0, 0)
                        : new Color32(0,   0, 0, 0);
                }
            }
        }
        occupancyTex.SetPixels32(cols);
        occupancyTex.Apply();
    }

    public void Emit(Vector3 worldPos)
    {
        Ray ray = new Ray(worldPos + Vector3.up * 1f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f, settings.stageMask))
        {
            worldPos = hit.point;
        }
        else
        {
            // もし拾えなければ元の worldPos を使うか早期リターン
            Debug.LogWarning("Emit: stage surface not found under " + worldPos);
        }

        lastEmitPos = worldPos;
        lastEmitTime = Time.time;

        // 2) worldPos→ボクセルインデックス
        Vector3 rel = worldPos - settings.worldOrigin;
        Vector3 idxF = new Vector3(
            rel.x / settings.worldSize.x * settings.gridSize.x,
            rel.y / settings.worldSize.y * settings.gridSize.y,
            rel.z / settings.worldSize.z * settings.gridSize.z
        );
        // 切り捨てして範囲内にClamp
        int ix = Mathf.Clamp(Mathf.FloorToInt(idxF.x), 0, settings.gridSize.x - 1);
        int iy = Mathf.Clamp(Mathf.FloorToInt(idxF.y), 0, settings.gridSize.y - 1);
        int iz = Mathf.Clamp(Mathf.FloorToInt(idxF.z), 0, settings.gridSize.z - 1);

        // 3) ComputeShader に渡す
        settings.waveBfsCS.SetInts("_EmitVoxel", ix, iy, iz);
    }
    
    void DebugLogDistanceSample()
    {
        // 1) ワールド位置 → ボクセル座標
        Vector3 rel = lastEmitPos - settings.worldOrigin;
        int x = Mathf.FloorToInt(rel.x / settings.worldSize.x * (settings.gridSize.x - 1));
        int y = Mathf.FloorToInt(rel.y / settings.worldSize.y * (settings.gridSize.y - 1));
        int slice = Mathf.FloorToInt(rel.z / settings.worldSize.z * (settings.gridSize.z - 1));
        x = Mathf.Clamp(x, 0, settings.gridSize.x - 1);
        y = Mathf.Clamp(y, 0, settings.gridSize.y - 1);
        slice = Mathf.Clamp(slice, 0, settings.gridSize.z - 1);

        // 2) 1×1×1 の範囲を Readback
        AsyncGPUReadback.Request(
            distancePrevRT,    // Texture
            0,                 // mipIndex
            x, 1,              // x, width
            y, 1,              // y, height
            slice, 1,          // z, depth
            request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback error");
                    return;
                }
                var data = request.GetData<float>();
                if (data.Length > 0)
                {
                    Debug.Log($"[DistanceField] Sample({x},{y},{slice}) = {data[0]}");
                }
                else
                {
                    Debug.LogError($"Readback returned no data (len={data.Length})");
                }
            }
        );
    }
    void Update()
    {
        var cs = settings.waveBfsCS;
        var gs = settings.gridSize;
        float cell = Mathf.Max(
            settings.worldSize.x / gs.x,
            settings.worldSize.y / gs.y,
            settings.worldSize.z / gs.z
        );

        cs.SetInts("_GridSize", gs.x, gs.y, gs.z);
        cs.SetFloat("_CellSize", cell);

        // Init pass
        cs.SetTexture(initK, "Occupancy", occupancyTex);
        cs.SetTexture(initK, "D",         distanceRT);
        cs.Dispatch(initK,
            Mathf.CeilToInt(gs.x / 8f),
            Mathf.CeilToInt(gs.y / 8f),
            Mathf.CeilToInt(gs.z / 8f)
        );

        // Copy to prev
        Graphics.CopyTexture(distanceRT, distancePrevRT);

        // Propagate passes
        for (int i = 0; i < settings.bfsPasses; i++)
        {
            cs.SetTexture(propK, "Occupancy", occupancyTex);
            cs.SetTexture(propK, "D_prev",    distancePrevRT);
            cs.SetTexture(propK, "D",         distanceRT);
            cs.Dispatch(propK,
                Mathf.CeilToInt(gs.x / 8f),
                Mathf.CeilToInt(gs.y / 8f),
                Mathf.CeilToInt(gs.z / 8f)
            );
            Graphics.CopyTexture(distanceRT, distancePrevRT);
        }
        
        
        float radius = (Time.time - lastEmitTime) * waveSpeed;
        Shader.SetGlobalFloat("_Radius", radius);
        Shader.SetGlobalFloat("_Thickness", thickness);
        
        DebugLogDistanceSample();
    }

    void OnDisable()
    {
        if (occupancyTex    != null) DestroyImmediate(occupancyTex);
        if (distanceRT      != null) distanceRT.Release();
        if (distancePrevRT  != null) distancePrevRT.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            settings.worldOrigin + settings.worldSize * 0.5f,
            settings.worldSize
        );
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(lastEmitPos, 0.2f);
    }
}