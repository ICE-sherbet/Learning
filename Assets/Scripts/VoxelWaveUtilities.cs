using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public struct WaveParams {
    public float3 center;      // 波源のワールド位置
    public float  startTime;   // 発生時刻
    public float  speed;       // 伝播速度
    public float  wavelength;  // 波長
    public float  amplitude;   // 振幅
    public float  damping;     // 減衰係数
    public float  hideDelay;   // 非表示までの余裕時間
    public bool   isAlpha;     // true=α波, false=β波
}
public struct VoxelData {
    public bool    isSolid;      // 空気ブロックかどうか
    public float   slopeAngle;   // 0＝フラット、それ以外は degrees
    public float2  slopeDir;     // XZ 平面上の単位ベクトル (nx, nz)
}

[BurstCompile]
public static class VoxelWaveUtility
{
    /// <summary>
    /// セル内ローカル座標 lx,lz [0,1] に対して、スロープ角度と方向を
    /// 用いた高さオフセットを返します。
    /// </summary>
    static float ShapeOffset(
        VoxelData v,      // .isSolid, .slopeAngle (deg), .slopeDir (normalized float2)
        float lx, float lz,
        float cellSize
    ) {
        // フラットならオフセット 0
        if (v.slopeAngle <= 0f) return 0f;

        // セル原点 (x*cellSize, z*cellSize) から見たローカル XY
        // worldPos から計算していた lx,lz を使う前提
        // 傾斜方向への射影距離 (0..cellSize)
        float proj = math.clamp(lx * v.slopeDir.x + lz * v.slopeDir.y, 0f, 1f) * cellSize;

        // 高さ差 = proj * tan(slopeAngle)
        float tanA = math.tan(math.radians(v.slopeAngle));
        return proj * tanA;
    }
    public static float SampleHeight(
        float3 worldPos,
        [ReadOnly] NativeArray<VoxelData>  voxels,
        [ReadOnly] NativeArray<WaveParams> waves,
        int W, int H, int D,
        float cellSize,
        float now
    ) {
        // XZ→セル座標
        int x = (int)math.clamp(math.floor(worldPos.x / cellSize), 0, W - 1);
        int z = (int)math.clamp(math.floor(worldPos.z / cellSize), 0, D - 1);

        float bestY = float.NegativeInfinity;

        for (int y = 0; y < H; y++) {
            int idx = y * (W * D) + z * W + x;
            var v = voxels[idx];
            if (!v.isSolid) continue;

            // ベースY＋スロープ高さ
            float baseY = y * cellSize;
            float lx = (worldPos.x / cellSize) - x;
            float lz = (worldPos.z / cellSize) - z;
            float hShape = ShapeOffset(v, lx, lz, cellSize);

            // α波／β波別に寄与を集計
            bool hasA = false, hasB = false;
            float sumA = 0f, sumB = 0f;

            for (int wi = 0; wi < waves.Length; wi++) {
                var w = waves[wi];
                float dt = now - w.startTime;
                if (dt < 0f) continue;
                float r = dt * w.speed;
                float d3 = math.distance(worldPos, w.center);
                if (d3 > r) continue;

                float phase   = (r - d3) / w.wavelength * (2f * math.PI);
                float contrib = w.amplitude * math.sin(phase) * math.exp(-w.damping * dt);

                if (w.isAlpha) { hasA = true; sumA += contrib; }
                else           { hasB = true; sumB += contrib; }
            }

            // αとβが両方あれば波高ゼロ、それ以外は通常どおり
            float hWave = (hasA && hasB) ? 0f : (sumA + sumB);

            float finalY = baseY + hShape + hWave;
            bestY = math.max(bestY, finalY);
        }

        return bestY;
    }
}
