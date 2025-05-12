using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(VoxelWaveController))]
public class DistanceFieldVisualizer : MonoBehaviour
{
    public Material debugMat;
    VoxelWaveController controller;
    [SerializeField] VoxelWaveSettings settings;

    void OnEnable()
    {
        controller = GetComponent<VoxelWaveController>();
    }

    void OnDisable()
    {
        controller = null;
    }

    void OnDrawGizmos()
    {
        if (controller == null || debugMat == null) return;
        // マテリアルにグローバルテクスチャ＆パラメータがセットされている前提
        // ボリュームのワールド空間版ボックスを描画
        Vector3 origin = settings.worldOrigin;
        Vector3 size   = settings.worldSize;

        GameObject tempPrimitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = tempPrimitive.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(tempPrimitive);
        Matrix4x4 mat = Matrix4x4.TRS(
            origin + size * 0.5f,
            Quaternion.identity,
            size
        );
        Graphics.DrawMesh(
            cube,
            mat,
            debugMat,
            0,
            null,
            0,
            null,
            castShadows:ShadowCastingMode.Off,
            receiveShadows:false,
            null,
            LightProbeUsage.Off
        );
    }
}