using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Quene.Runtime
{
    public class VoxelGrid : MonoBehaviour
    {
        public Vector3Int gridSize;
        public float voxelSize;

        [Header("PrefabDatabase (Floor / Gimmick をまとめて持つ)")]
        public FloorDatabase floorDatabase; // すでに定義済み (FloorKey → Prefab)

        public GimmickDatabase gimmickDatabase; // 今回新設 (GimmickDefinition → Prefab)

        public FloorType[] floorCells1D;

        /// <summary>
        /// (x,y,z) の床種別を取得。範囲外なら FloorType.None を返す。
        /// </summary>
        public FloorType GetFloorType(int x, int y, int z)
        {
            if (!this.InRange(x, y, z))
            {
                return FloorType.None;
            }

            int idx = x + y * gridSize.x + z * gridSize.x * gridSize.y;
            if (idx < 0 || idx >= floorCells1D.Length) return FloorType.None;
            return floorCells1D[idx];
        }

        /// <summary>
        /// (x,y,z) の床種別を設定。範囲外を指定された場合は無視。
        /// </summary>
        public void SetFloorType(int x, int y, int z, FloorType type)
        {
            if (!InRange(x, y, z)) return;
            int idx = x + y * gridSize.x + z * gridSize.x * gridSize.y;
            if (idx < 0 || idx >= floorCells1D.Length) return;
            Debug.Log(idx);
            floorCells1D[idx] = type;
        }

        /// <summary>
        /// gridSize の変更などで floorCells1D の長さが合わなくなったときに呼び出し、
        /// 必要に応じてサイズを resize して FloorType.None で埋める。
        /// </summary>
        private void EnsureFloorCellsSize()
        {
            int total = gridSize.x * gridSize.y * gridSize.z;
            if (total <= 0) return;
            if (floorCells1D == null)
                floorCells1D = new FloorType[] { };
            if (floorCells1D.Length != total)
            {
                var newList = new FloorType[total];
                // 既存の値をコピー（範囲が重なる分だけ）
                int copyCount = Mathf.Min(floorCells1D.Length, total);
                for (int i = 0; i < copyCount; i++)
                    newList[i] = floorCells1D[i];
                floorCells1D = newList;
            }
        }

        /// <summary>
        /// 座標がグリッド内か判定するユーティリティ
        /// </summary>
        public bool InRange(int x, int y, int z)
            => x >= 0 && x < gridSize.x
                      && y >= 0 && y < gridSize.y
                      && z >= 0 && z < gridSize.z;
        
        [Serializable]
        public struct SlopeCellEntry
        {
            public Vector3Int position;

            public SlopeKey slope;
        }
        [SerializeField]
        private List<GimmickCellEntry> SlopeCellEntries;
        
        [SerializeField]
        private List<GimmickCellEntry> gimmickCellEntries;

        private GameObject floorRoot;
        private GameObject slopeRoot;
        private GameObject gimmickRoot;

        public GimmickCell? GetGimmickCell(int x, int y, int z)
        {
            for (int i = 0; i < gimmickCellEntries.Count; i++)
            {
                if (gimmickCellEntries[i].position.x == x
                    && gimmickCellEntries[i].position.y == y
                    && gimmickCellEntries[i].position.z == z)
                {
                    return gimmickCellEntries[i].cell;
                }
            }

            return null;
        }

        public void SetGimmickCell(int x, int y, int z, GimmickCell? cellOpt)
        {
            // すでに同じ位置があれば先に削除
            for (int i = gimmickCellEntries.Count - 1; i >= 0; i--)
            {
                if (gimmickCellEntries[i].position.x == x
                    && gimmickCellEntries[i].position.y == y
                    && gimmickCellEntries[i].position.z == z)
                {
                    gimmickCellEntries.RemoveAt(i);
                    break;
                }
            }

            // 新しい cell を追加（nullなら追加せず、実質削除）
            if (cellOpt.HasValue)
            {
                var entry = new GimmickCellEntry
                {
                    position = new Vector3Int(x, y, z),
                    cell = cellOpt.Value
                };
                gimmickCellEntries.Add(entry);
            }
        }

        // ────────────────────────────────
        // 3) Awake / OnValidate でリスト初期化
        // ────────────────────────────────

        private void Reset()
        {
            // Reset は MonoBehaviour を Inspector 上で Add した直後に呼ばれるので、
            // ここで必ず floorCells1D を初期化しておく
            EnsureFloorCellsSize();
            gimmickCellEntries = new List<GimmickCellEntry>();
        }

        private void OnValidate()
        {
            // gridSize が変わったときに floorCells1D のサイズを自動調整
            EnsureFloorCellsSize();
        }

        private void Awake()
        {
            EnsureFloorCellsSize();
        }

        // ────────────────────────────────
        // 4) Build() などの利用例
        // ────────────────────────────────

        private void ClearChildren(GameObject root)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
        }

        public void Build()
        {
            // ルートとなる空の親 GameObject を用意
            this.ClearChildren(floorRoot);
            this.ClearChildren(slopeRoot);
            this.ClearChildren(gimmickRoot);

            // 1) 床セル(Black/White/SlopeUp/SlopeDown) の配置
            for (int x = 0; x < this.gridSize.x; x++)
            {
                for (int y = 0; y < this.gridSize.y; y++)
                {
                    for (int z = 0; z < this.gridSize.z; z++)
                    {
                        FloorType cellType = this.GetFloorType(x, y, z);
                        if (cellType == FloorType.None) continue;

                        FloorKey key;
                        FloorNeighbor mask = this.ComputeFloorMask(x, y, z, cellType);
                        key = new FloorKey(cellType, mask);

                        GameObject prefab = this.floorDatabase.GetPrefab(key)
                                            ?? this.GetDefaultPrefabFor(key);
                        if (prefab == null)
                        {
                            continue;
                        }

                        Vector3 wpos = this.CellToWorld(new Vector3Int(x, y, z));
                        Quaternion rot = Quaternion.identity;
                        var parent = floorRoot.transform;
                        Instantiate(prefab, wpos, rot, parent);
                    }
                }
            }

            // 2) ギミックセルの配置
            foreach (var entry in gimmickCellEntries)
            {
                Vector3Int pos = entry.position;
                GimmickCell cell = entry.cell;
                var def = cell.definition;
                if (def == null)
                {
                    Debug.LogError($"[VoxelGrid] 無効なギミック参照: セル{pos} の定義が null");
                    continue;
                }

                GameObject prefab = gimmickDatabase.GetPrefab(def);
                if (prefab == null)
                {
                    Debug.LogError($"[VoxelGrid] GimmickDatabase に登録なし: {def.name}");
                    continue;
                }

                Vector3 wpos = CellToWorld(pos) + Vector3.up * (voxelSize * 0.51f);
                GameObject inst = Instantiate(prefab, wpos, cell.rotation, gimmickRoot.transform);
                def.OnPlace(inst);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private GameObject GetDefaultPrefabFor(FloorKey key)
        {
            // floorDatabase にマッチする Prefab がなければフォールバック
            // 例: mask = AllDirections の汎用タイル、など
            return null;
        }

        public Vector3 CellToWorld(Vector3Int c)
            => transform.position + new Vector3(c.x, c.y, c.z) * voxelSize;


        public FloorNeighbor ComputeFloorMask(int x, int y, int z, FloorType type)
        {
            var mask = FloorNeighbor.None;

            // 北 (+Z)
            {
                int nx = x + 0;
                int ny = y;
                int nz = z + 1;
                if (this.InRange(nx, ny, nz) && this.GetFloorType(nx, ny, nz) == type)
                {
                    mask |= FloorNeighbor.North;
                }
            }

            // 東 (+X)
            {
                int nx = x + 1;
                int ny = y;
                int nz = z + 0;
                if (this.InRange(nx, ny, nz) && this.GetFloorType(nx, ny, nz) == type)
                {
                    mask |= FloorNeighbor.East;
                }
            }

            // 南 (-Z)
            {
                int nx = x + 0;
                int ny = y;
                int nz = z - 1;
                if (this.InRange(nx, ny, nz) && this.GetFloorType(nx, ny, nz) == type)
                {
                    mask |= FloorNeighbor.South;
                }
            }

            // 西 (-X)
            {
                int nx = x - 1;
                int ny = y;
                int nz = z + 0;
                if (this.InRange(nx, ny, nz) && this.GetFloorType(nx, ny, nz) == type)
                {
                    mask |= FloorNeighbor.West;
                }
            }

            return mask;
        }
    }
}