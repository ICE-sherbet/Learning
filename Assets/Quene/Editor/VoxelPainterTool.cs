using System.Collections.Generic;
using Quene.Runtime;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

[EditorTool("Voxel Painter", typeof(VoxelGrid))]
public class VoxelPainterTool : EditorTool
{
    enum Mode
    {
        Floor,
        SlopeUp,
        SlopeDown,
        Gimmick
    }

    Mode currentMode = Mode.Floor;
    int activeY = 0;
    bool isPainting = false;

    VoxelGrid targetGrid => (VoxelGrid)target;

    // Gimmick 定義のリスト
    List<GimmickDefinition> gimmickDefs = new List<GimmickDefinition>();
    int selectedGimmickIndex = 0;

    // 床・スロープ用に現在選択中の FloorType
    FloorType selectedFloorType = FloorType.Black;

    void OnEnable()
    {
        // GimmickDatabase からすべての定義を取得（ただしエディタ実行時のみ）
        if (targetGrid.gimmickDatabase != null)
        {
            gimmickDefs = new List<GimmickDefinition>(targetGrid.gimmickDatabase.GetAllDefinitions());
        }
    }

    public override void OnToolGUI(EditorWindow window)
    {
        var e = Event.current;

        // ─────────────────────────────────────────────────────────────
        // (1) モード切替＋階層スライダー＋Gimmick選択UI
        // ─────────────────────────────────────────────────────────────
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 240, 120));
        currentMode = (Mode)GUILayout.Toolbar((int)currentMode, new[] { "Floor", "UpSlope", "DownSlope", "Gimmick" });

        GUILayout.Space(4);
        GUILayout.Label($"Editing Floor Y = {activeY}");
        activeY = (int)GUILayout.HorizontalSlider(activeY, 0, targetGrid.gridSize.y - 1);
        GUILayout.Space(8);

        if (currentMode == Mode.Floor)
        {
            // 床色選択ドロップダウン
            selectedFloorType = (FloorType)EditorGUILayout.EnumPopup("FloorType", selectedFloorType);
        }
        else if (currentMode == Mode.Gimmick)
        {
            if (gimmickDefs.Count == 0)
            {
                GUILayout.Label("No Gimmick Definitions");
            }
            else
            {
                string[] names = new string[gimmickDefs.Count];
                for (int i = 0; i < names.Length; i++)
                    names[i] = gimmickDefs[i].DisplayName;
                selectedGimmickIndex = EditorGUILayout.Popup("Gimmick", selectedGimmickIndex, names);
            }
        }

        GUILayout.EndArea();
        Handles.EndGUI();

        // ─────────────────────────────────────────────────────────────
        // (2) Undo の開始／終了
        // ─────────────────────────────────────────────────────────────
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Undo.RegisterCompleteObjectUndo(targetGrid, "Paint Voxel");
            isPainting = true;
            e.Use();
        }

        if (isPainting && e.type == EventType.MouseUp && e.button == 0)
        {
            isPainting = false;
            e.Use();
        }

        // ─────────────────────────────────────────────────────────────
        // (3) ペイント処理 (ドラッグ中のセル塗り／削り)
        // ─────────────────────────────────────────────────────────────
        if (isPainting && e.type == EventType.MouseDrag)
        {
            Debug.Log("Paint Voxel");
            if (GetCellUnderMouse(out Vector3Int cellPos))
            {

                Debug.Log($"{cellPos.x}, {activeY}, {cellPos.z}");
                int x = cellPos.x, y = activeY, z = cellPos.z;
                if (!targetGrid.InRange(x, y, z)) return;

                switch (currentMode)
                {
                    case Mode.Floor:
                        PaintFloorCell(x, y, z);
                        break;
                }
            }

            e.Use();
        }

        // ─────────────────────────────────────────────────────────────
        // (4) リアルタイムプレビュー (オプション)
        // ─────────────────────────────────────────────────────────────
        if (!isPainting && e.type == EventType.MouseMove)
        {
            Debug.Log("Paint Preview");
            if (GetCellUnderMouse(out Vector3Int previewCell))
                DrawPreview(previewCell);
        }
    }

    
    private void PaintFloorCell(int x, int y, int z)
    {
        targetGrid.SetFloorType(x, y, z, selectedFloorType);

        EditorUtility.SetDirty(targetGrid);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetGrid.gameObject.scene);
    }

    void PaintSlopeUp(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, 100f, 1 << LayerMask.NameToLayer("FloorPlane")))
            return;

        Vector3Int cellPos = WorldToCell(hit.point);
        int x = cellPos.x, y = activeY, z = cellPos.z;
        if (!targetGrid.InRange(x, y, z)) return;

        // 下階に床がない or 既存床とつながらない場合は置けない

        EditorUtility.SetDirty(targetGrid);
        EditorSceneManager.MarkSceneDirty(targetGrid.gameObject.scene);
    }

    void PaintSlopeDown(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, 100f, 1 << LayerMask.NameToLayer("FloorPlane")))
            return;

        Vector3Int cellPos = WorldToCell(hit.point);
        int x = cellPos.x, y = activeY, z = cellPos.z;
        if (!targetGrid.InRange(x, y, z)) return;

        EditorUtility.SetDirty(targetGrid);
        EditorSceneManager.MarkSceneDirty(targetGrid.gameObject.scene);
    }

    void PaintGimmick(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit, 100f, 1 << LayerMask.NameToLayer("FloorPlane")))
            return;

        Vector3Int cellPos = WorldToCell(hit.point);
        int x = cellPos.x, y = activeY, z = cellPos.z;
        if (!targetGrid.InRange(x, y, z)) return;

        // その位置に床がないとギミックは置けない (例)
        var ft = targetGrid.GetFloorType(x, y, z);
        if (ft == FloorType.None) return;

        if (gimmickDefs.Count == 0) return;
        var def = gimmickDefs[selectedGimmickIndex];

        var newCell = new GimmickCell
        {
            definition = def,
            rotation = Quaternion.identity,
        };
        targetGrid.SetGimmickCell(x, y, z, newCell);

        EditorUtility.SetDirty(targetGrid);
        EditorSceneManager.MarkSceneDirty(targetGrid.gameObject.scene);
    }

    void DrawPreview(Vector3Int cellPos)
    {

        Vector3Int pos = cellPos;
        int x = pos.x, y = activeY, z = pos.z;
        if (!targetGrid.InRange(x, y, z)) return;

        if (currentMode == Mode.Floor)
        {
            FloorType selType = selectedFloorType;
            FloorNeighbor mask = (FloorNeighbor)255; // 1111 (全方向に床があると仮定)
            FloorKey key = new FloorKey(selType, mask);
            GameObject prefab = targetGrid.floorDatabase.GetPrefab(key);
            Debug.Log("Draw Preview: " + key.FloorType + " " + key.Mask + " " + mask);
            if (prefab != null)
            {
                Debug.Log("Draw Preview: true:" + prefab.name);
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                {
                    Vector3 wpos = targetGrid.CellToWorld(pos);
                    Handles.color = new Color(1, 1, 1, 0.5f);
                    Matrix4x4 tm = Matrix4x4.TRS(wpos, Quaternion.identity,
                        Vector3.one * targetGrid.voxelSize);
                    Handles.color = Color.white;
                }
            }
        }
    }

    Vector3Int WorldToCell(Vector3 wp)
    {
        Vector3 local = (wp - targetGrid.transform.position) / targetGrid.voxelSize;
        return new Vector3Int(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y),
            Mathf.FloorToInt(local.z)
        );
    }
    /// <summary>
    /// SceneView 上でマウスが指している位置を、activeY の高さの水平面との交点にプロジェクションし、
    /// グリッドセル座標 (x,y,z) を返します。Collider には依存しません。
    /// </summary>
    private bool GetCellUnderMouse(out Vector3Int cell)
    {
        cell = Vector3Int.zero;

        // SceneView のカメラから MousePosition → Ray を生成
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        // activeY のワールド座標（水平平面）を定義
        float planeY = targetGrid.transform.position.y + activeY * targetGrid.voxelSize;
        Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldHit = ray.GetPoint(enter);
            // グリッドのローカル空間に変換
            Vector3 localPos = (worldHit - targetGrid.transform.position) / targetGrid.voxelSize;
            int x = Mathf.FloorToInt(localPos.x);
            int y = activeY;
            int z = Mathf.FloorToInt(localPos.z);

            cell = new Vector3Int(x, y, z);
            return true;
        }
        return false;
    }
    // ≒ VoxelGrid 内の ComputeFloorMask, ComputeSlopeUpMask, InferUnderlyingFloorForSlopeUp などを呼び出す…
}