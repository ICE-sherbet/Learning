using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;  // MeshData API
using Unity.Collections;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class ComplexMeshXZSubdivider : MonoBehaviour
{
    [Tooltip("X方向の分割数（グリッド線の数は xDivisions-1 になります）")]
    public int xDivisions = 10;
    [Tooltip("Z方向の分割数（グリッド線の数は zDivisions-1 になります）")]
    public int zDivisions = 10;

    void Start()
    {
        var mf  = GetComponent<MeshFilter>();
        var src = mf.sharedMesh;
        mf.mesh = SubdivideMeshOnGrid(src, xDivisions, zDivisions);
    }

    Mesh SubdivideMeshOnGrid(Mesh srcMesh, int xSeg, int zSeg)
    {
        // 1) 元メッシュの頂点＆UVとインデックスを読み込んで、Triリストに変換
        var srcVerts = srcMesh.vertices;
        var srcUVs   = srcMesh.uv;
        var srcTris  = srcMesh.triangles;
        var tris = new List<Tri>();
        for (int i = 0; i < srcTris.Length; i += 3)
        {
            tris.Add(new Tri(
                srcVerts[srcTris[i+0]], srcUVs[srcTris[i+0]],
                srcVerts[srcTris[i+1]], srcUVs[srcTris[i+1]],
                srcVerts[srcTris[i+2]], srcUVs[srcTris[i+2]]
            ));
        }

        // 2) 境界からグリッド平面位置を作成
        var bounds = srcMesh.bounds;
        float minX = bounds.min.x, maxX = bounds.max.x;
        float minZ = bounds.min.z, maxZ = bounds.max.z;
        var xPlanes = new List<float>(xSeg - 1);
        var zPlanes = new List<float>(zSeg - 1);
        for (int i = 1; i < xSeg; i++)
            xPlanes.Add(Mathf.Lerp(minX, maxX, (float)i / xSeg));
        for (int i = 1; i < zSeg; i++)
            zPlanes.Add(Mathf.Lerp(minZ, maxZ, (float)i / zSeg));

        // 3) X方向の各平面で切り分け
        foreach (float px in xPlanes)
        {
            var next = new List<Tri>();
            foreach (var t in tris)
                next.AddRange(SplitTriangle(t, Axis.X, px));
            tris = next;
        }
        // 4) Z方向の各平面で切り分け
        foreach (float pz in zPlanes)
        {
            var next = new List<Tri>();
            foreach (var t in tris)
                next.AddRange(SplitTriangle(t, Axis.Z, pz));
            tris = next;
        }

        // 5) 分割後の三角形リストを頂点・UV配列＋インデックスに変換（重複頂点はまとめる）
        var uniqueVerts = new Dictionary<VertexKey, int>();
        var outVerts    = new List<Vector3>();
        var outUVs      = new List<Vector2>();
        var outIdx      = new List<int>();

        foreach (var t in tris)
        {
            for (int i = 0; i < 3; i++)
            {
                var key = new VertexKey(t.pos[i], t.uv[i]);
                if (!uniqueVerts.TryGetValue(key, out var idx))
                {
                    idx = outVerts.Count;
                    uniqueVerts[key] = idx;
                    outVerts.Add(key.pos);
                    outUVs.Add(key.uv);
                }
                outIdx.Add(idx);
            }
        }

        int vCount = outVerts.Count;
        int iCount = outIdx.Count;

        // 6) NativeArray にコピーして MeshData に流し込む
        var naVerts = new NativeArray<Vector3>(outVerts.ToArray(), Allocator.Temp);
        var naUVs   = new NativeArray<Vector2>(outUVs.ToArray(),   Allocator.Temp);
        var naIdx   = new NativeArray<int>(outIdx.ToArray(),        Allocator.Temp);

        var meshDataArr = Mesh.AllocateWritableMeshData(1);
        var md = meshDataArr[0];

        md.SetVertexBufferParams(
            vCount,
            new VertexAttributeDescriptor(VertexAttribute.Position,  VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        );
        md.SetIndexBufferParams(iCount, IndexFormat.UInt32);

        md.GetVertexData<Vector3>(0).CopyFrom(naVerts);
        md.GetVertexData<Vector2>(1).CopyFrom(naUVs);
        md.GetIndexData<int>().CopyFrom(naIdx);

        md.subMeshCount = 1;
        var desc = new SubMeshDescriptor(0, iCount, MeshTopology.Triangles);
        md.SetSubMesh(0, desc, MeshUpdateFlags.DontRecalculateBounds);

        var dst = new Mesh
        {
            name        = srcMesh.name + "_SubdivXZ",
            indexFormat = IndexFormat.UInt32
        };
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArr, dst);

        dst.RecalculateNormals();
        dst.RecalculateBounds();

        naVerts.Dispose();
        naUVs.Dispose();
        naIdx.Dispose();

        return dst;
    }

    // --- 補助型定義 ---
    enum Axis { X, Z }

    struct Tri
    {
        public Vector3[] pos;
        public Vector2[] uv;
        public Tri(Vector3 p0, Vector2 u0, Vector3 p1, Vector2 u1, Vector3 p2, Vector2 u2)
        {
            pos = new[] { p0, p1, p2 };
            uv  = new[] { u0, u1, u2 };
        }
    }

    struct VertexKey : IEquatable<VertexKey>
    {
        public Vector3 pos;
        public Vector2 uv;
        public VertexKey(Vector3 p, Vector2 u) { pos = p; uv = u; }
        public bool Equals(VertexKey o)
            => pos.Equals(o.pos) && uv.Equals(o.uv);
        public override int GetHashCode()
            => pos.GetHashCode() ^ (uv.GetHashCode() << 1);
    }

    // 三角形を axis 軸の planePos で切り分け、必ず平面をまたがない三角形群として返す
    static IEnumerable<Tri> SplitTriangle(Tri tri, Axis axis, float planePos)
    {
        // 頂点ごとの分類
        float[] vals = {
            axis==Axis.X ? tri.pos[0].x : tri.pos[0].z,
            axis==Axis.X ? tri.pos[1].x : tri.pos[1].z,
            axis==Axis.X ? tri.pos[2].x : tri.pos[2].z
        };
        bool[] side = { vals[0] <= planePos, vals[1] <= planePos, vals[2] <= planePos };

        int insideCount = side[0]?1:0;
        insideCount    += side[1]?1:0;
        insideCount    += side[2]?1:0;
        if (insideCount == 0 || insideCount == 3)
        {
            // 全部同じ側：分割不要
            yield return tri;
            yield break;
        }

        // 頂点リストを巡回しながら交点を計算し、2つのポリゴンに分割
        var polyA = new List<Vertex>();
        var polyB = new List<Vertex>();
        for (int i = 0; i < 3; i++)
        {
            int j = (i + 1) % 3;
            var vi = new Vertex(tri.pos[i], tri.uv[i], side[i]);
            var vj = new Vertex(tri.pos[j], tri.uv[j], side[j]);
            // 常に今の頂点を所属ポリゴンに追加
            (side[i] ? polyA : polyB).Add(vi);

            // 辺がまたがるなら交点を計算して両方のポリゴンに追加
            if (side[i] != side[j])
            {
                float t = (planePos - (axis==Axis.X ? tri.pos[i].x : tri.pos[i].z))
                        / ((axis==Axis.X ? tri.pos[j].x : tri.pos[j].z) - (axis==Axis.X ? tri.pos[i].x : tri.pos[i].z));
                var p = Vector3.Lerp(tri.pos[i], tri.pos[j], t);
                var u = Vector2.Lerp(tri.uv[i],  tri.uv[j],  t);
                var viCut = new Vertex(p, u, true);
                var vjCut = new Vertex(p, u, false);
                polyA.Add(viCut);
                polyB.Add(vjCut);
            }
        }

        // ポリゴンを三角形扇形に分解して返却
        foreach (var poly in new[] { polyA, polyB })
        {
            for (int k = 1; k < poly.Count - 1; k++)
            {
                yield return new Tri(
                    poly[0].pos, poly[0].uv,
                    poly[k].pos, poly[k].uv,
                    poly[k+1].pos, poly[k+1].uv
                );
            }
        }
    }

    struct Vertex
    {
        public Vector3 pos;
        public Vector2 uv;
        public bool isA;
        public Vertex(Vector3 p, Vector2 u, bool a) { pos = p; uv = u; isA = a; }
    }
}
