#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QBuild.StageEditor
{
    public class StageEditorWindow : EditorWindow
    {
        private StageData _editingStageData;
        private const string BlocksInventoryPath = "Assets/QBuild/InGame/Part/Prefab/";
        private const string GimmicksInventoryPath = "Assets/QBuild/InGame/Gimmick/Prefabs/";
        private const string SaveStageDataFolderPath = "Assets/QBuild/InGame/Stage/StageData";
        private const string StageEditorSceneName = "StageEditor";
        private const string BlockLayerName = "Block";
        private const string IconPath = "Assets/QBuild/Editor/StageEditor/Icons/";
        private const string StageDataScriptableObjectName = "StageData";

        private readonly List<StageData> _stageDataList = new();

        private struct BlockData
        {
            public GameObject prefab;
            public Texture2D thumbnail;
        }

        private readonly List<BlockData> _blockList = new();

        private List<CheckNormalStageData.WarningLog> _warningLogs = new();

        private static GUIContent Icon(string file, string tooltip) =>
            new(AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath + file), tooltip);

        private static readonly GUIContent NewIcon     = Icon("New.png",     "新たなステージデータを生成");
        private static readonly GUIContent SaveIcon    = Icon("Save.png",    "ステージデータを保存する");
        private static readonly GUIContent RefreshIcon = Icon("Refresh.png", "更新");
        private static readonly GUIContent MagnetIcon  = Icon("Magnet.png",  "選択中のオブジェクトをスナップさせる");
        private static readonly GUIContent BlockOnlyIcon = Icon("BlockOnly.png", "Blockレイヤーのみ");
        private static readonly GUIContent TrashIcon = Icon("Trash.png", "ステージデータを削除する");

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            if (3 < EditorApplication.timeSinceStartup)
                return;
            Debug.Log("StageEditorWindow InitializeOnLoad");
            EditorApplication.delayCall += Open;
        }

        [MenuItem(EditorConst.WindowPrePath + "ステージエディタ/ステージエディタウィンドウ")]
        private static void Open()
        {
            var window = GetWindow<StageEditorWindow>();
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorSceneManager.sceneClosing += (_, _) => Initialize();
            EditorSceneManager.sceneOpened += (_, _) => Initialize();
            EditorApplication.quitting += Initialize;
            Selection.selectionChanged += () =>
            {
                CheckWarningLogs();
                if (Selection.gameObjects.Length == 0)
                    Refresh();
            };
            Refresh();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void Initialize()
        {
            var scene = SceneManager.GetActiveScene();

            if (scene.name != StageEditorSceneName) return;

            var obj = GetAllBlocksInScene();
            foreach (var o in obj)
            {
                DestroyImmediate(o);
            }

            _editingStageData = null;

            EditorSceneManager.SaveScene(scene);
        }

        private void OnGUI()
        {
            minSize = new Vector2(300, 300);

            if (SceneManager.GetActiveScene().name != StageEditorSceneName)
            {
                OnDrawOtherSceneWarning();
                GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
                OnDrawTools();
                ObjectSnapper.isEnableArea = false;
                return;
            }

            ObjectSnapper.isEnableArea = true;

            OnDrawDataEdit();
            OnDrawTools();

            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));

            using (new EditorGUILayout.HorizontalScope())
            {
                OnDrawStageScriptableObjects();
                OnDrawStageDataValue();
            }

            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            OnDrawInventory();

            GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
            OnDrawStageStatus();
        }


        private void OnSceneGUI(SceneView sceneView)
        {
            //Render the area of the stage
            if (_editingStageData == null) return;

            var area = _editingStageData.GetStageArea();
            var center = new Vector3(0.0f, area.y / 2.0f, 0.0f);

            Handles.DrawWireCube(center, area);

            var objPos = Selection.gameObjects;
            float grid = 1.0f;
            var distance = 0.05f;

            foreach (var obj in objPos)
            {
                if (obj.layer != LayerMask.NameToLayer(BlockLayerName)) continue;

                for (int x = area.x / 2; x > -area.x / 2; x--)
                {
                    for (int z = area.z / 2; z > -area.z / 2; z--)
                    {
                        var pos = new Vector3(x - grid / 2.0f, obj.transform.position.y, z - grid / 2.0f);

                        Handles.DrawLine(pos + Vector3.left * distance, pos + Vector3.right * distance);
                        Handles.DrawLine(pos + Vector3.up * distance, pos + Vector3.down * distance);
                        Handles.DrawLine(pos + Vector3.forward * distance, pos + Vector3.back * distance);
                    }
                }
            }
        }

        //別のシーンが開かれているときに呼ばれる
        private void OnDrawOtherSceneWarning()
        {
            EditorGUILayout.HelpBox("ステージエディタシーンを開いてください。", MessageType.Warning);
            if (GUILayout.Button("ステージエディタシーンを開く"))
                OpenStageEditorScene();
        }
        private static void OpenStageEditorScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            { 
                const string FolderPath = "Assets/QBuild/Editor/StageEditor/";
                const string SceneName = "StageEditor.unity";
                EditorSceneManager.OpenScene(FolderPath + SceneName);
            }
        }
        private void OnDrawDataEdit()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox,
                       GUILayout.Height(40)))
            {
                if (GUILayout.Button(NewIcon, GUILayout.Width(40),
                        GUILayout.Height(40)))
                {
                    CreateStageData("NewStageData");
                }

                bool grayOut = _editingStageData == null;
                using (new EditorGUI.DisabledScope(grayOut))
                {
                    if (GUILayout.Button(SaveIcon, GUILayout.Height(40)))
                    {
                        SaveStageData();
                    }
                }

                if (GUILayout.Button(RefreshIcon, GUILayout.Width(80), GUILayout.Height(40)))
                {
                    Refresh();
                }
            }
        }

        private void Refresh()
        {
            AssetDatabase.Refresh();

            RefreshStageDataList();
            RefreshBlockList();
            ObjectSnapper.stageArea = _editingStageData?.GetStageArea() ?? Vector3Int.zero;

            var stageData = _stageDataList.ToArray();
            foreach (var data in stageData)
            {
                var result = CheckNormalStageData.CheckStageData(data);
                ChangeStageDataProperty(data, "_isExistWarningItem", result.Count > 0);
            }

            CheckWarningLogs();
        }

        private void CheckWarningLogs()
        {
            _warningLogs = new List<CheckNormalStageData.WarningLog>();
            _warningLogs = CheckNormalStageData.CheckStageData(_editingStageData);
        }
        private void RefreshStageDataList()
        {
            _stageDataList.Clear();
            var guids = AssetDatabase.FindAssets("t:" + StageDataScriptableObjectName);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var stageData = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stageData == null) continue;
                _stageDataList.Add(stageData);
            }
        }

        private void RefreshBlockList()
        {
            _blockList.Clear();
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { BlocksInventoryPath, GimmicksInventoryPath });
            foreach (var guid in guids)
            {
                BlockData blockData = new();
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var block = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (block == null) continue;
                blockData.prefab = block;
                blockData.thumbnail = AssetPreview.GetAssetPreview(block);
                _blockList.Add(blockData);
            }
        }

        private void OnDrawTools()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.color = ObjectSnapper.IsEnable ? Color.cyan : Color.white;
                if (GUILayout.Button(MagnetIcon, GUILayout.Height(20),
                        GUILayout.MaxWidth(60)))
                {
                    ObjectSnapper.IsEnable = !ObjectSnapper.IsEnable;
                }

                GUI.color = Color.white;

                GUI.color = ObjectSnapper.IsBlockOnly ? Color.cyan : Color.white;
                if (GUILayout.Button(BlockOnlyIcon, GUILayout.Height(20),
                        GUILayout.MaxWidth(60)))
                {
                    ObjectSnapper.IsBlockOnly = !ObjectSnapper.IsBlockOnly;
                }

                GUI.color = Color.white;
            }
        }


        private Vector2 _dataScrollPosition;

        private void OnDrawStageScriptableObjects()
        {
            using (var scroll =
                   new GUILayout.ScrollViewScope(_dataScrollPosition, EditorStyles.helpBox, GUILayout.Width(150),
                       GUILayout.Height(250)))
            {
                _dataScrollPosition = scroll.scrollPosition;


                foreach (var stageData in _stageDataList)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.color = Color.white;
                        if (GUILayout.Button(TrashIcon, GUILayout.Height(20),
                                GUILayout.Width(20))) //Delete
                        {
                            var result = EditorUtility.DisplayDialog("消去", "本当に消去しますか？",
                                "はい",
                                "いいえ"
                            );

                            if (result)
                            {
                                DeleteStageData(stageData);
                                return;
                            }
                        }

                        if (stageData.IsExistWarningItem()) GUI.color = Color.yellow;
                        if (stageData == _editingStageData) GUI.color = Color.cyan;

                        if (GUILayout.Button(stageData.GetFileName(), GUILayout.ExpandWidth(true))) //Load
                        {
                            LoadStageData(stageData);
                            return;
                        }
                    }
                }

                GUI.color = Color.white;
            }
        }

        private void OnDrawStageDataValue()
        {
            if (_editingStageData == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(250)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ファイル名（半角英数字）", GUILayout.Width(140));

                    var text = EditorGUILayout.DelayedTextField(_editingStageData.GetFileName());
                    if (text != null && text != _editingStageData.GetFileName())
                    {
                        var log = AssetDatabase.RenameAsset(
                            SaveStageDataFolderPath + "/" + _editingStageData.GetFileName(),
                            text);
                        if (log != "")
                        {
                            EditorUtility.DisplayDialog("エラー", "ファイル名が重複しています。", "OK");
                        }
                        else
                        {
                            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_editingStageData), text);
                            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_editingStageData._stage), text);

                            ChangeStageDataProperty(_editingStageData, "_fileName", text);
                        }
                    }
                }


                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ステージ名（全角可）", GUILayout.Width(140));

                    var text = EditorGUILayout.DelayedTextField(_editingStageData.GetStageName());
                    if (text != null && text != _editingStageData.GetStageName())
                    {
                        ChangeStageDataProperty(_editingStageData, "_stageName", text);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("難易度", GUILayout.Width(140));

                    //1~5 slider
                    var difficult = EditorGUILayout.IntSlider(_editingStageData.GetStageDifficult(), 1, 5);
                    if (difficult != _editingStageData.GetStageDifficult())
                    {
                        ChangeStageDataProperty(_editingStageData, "_stageDifficult", difficult);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ステージ範囲", GUILayout.Width(140));

                    EditorGUILayout.LabelField("x", GUILayout.Width(10));
                    int x = EditorGUILayout.DelayedIntField(_editingStageData.GetStageArea().x);
                    EditorGUILayout.LabelField("y", GUILayout.Width(10));
                    int y = EditorGUILayout.DelayedIntField(_editingStageData.GetStageArea().y);
                    EditorGUILayout.LabelField("z", GUILayout.Width(10));
                    int z = EditorGUILayout.DelayedIntField(_editingStageData.GetStageArea().z);

                    var area = new Vector3Int(x, y, z);

                    if (_editingStageData.GetStageArea() != area)
                    {
                        ChangeStageDataProperty(_editingStageData, "_stageArea", area);
                        ObjectSnapper.stageArea = area;

                        var obj = GetAllBlocksInScene();
                        foreach (var o in obj) ObjectSnapper.SnapToGrid(o.transform);
                    }
                }

                {
                    EditorGUILayout.LabelField("サムネイル画像");

                    var texture = EditorGUILayout.ObjectField(_editingStageData.GetStageImage(), typeof(Texture), false,
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true)) as Texture2D;

                    if (_editingStageData.GetStageImage() != texture)
                    {
                        ChangeStageDataProperty(_editingStageData, "_stageImage", texture);
                    }
                }
            }
        }

        private Vector2 _inventoryScrollPosition;

        private void OnDrawInventory()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("ブロックフォルダを開く"))
                {
                    //Projectウィンドウでブロックフォルダを開く
                    var dummy = _blockList[0].prefab;
                    if (dummy)
                    {
                        EditorGUIUtility.PingObject(dummy);
                    }
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(
                           _inventoryScrollPosition,
                           EditorStyles.helpBox,
                           GUILayout.ExpandWidth(true),
                           GUILayout.ExpandHeight(true)))
                {
                    _inventoryScrollPosition = scroll.scrollPosition;

                    float availableWidth = EditorGUIUtility.currentViewWidth - 35;
                    int buttonsPerRow = Mathf.FloorToInt(availableWidth / 100);

                    float buttonSize = (availableWidth - (buttonsPerRow - 1)) / buttonsPerRow;

                    for (int i = 0; i < _blockList.Count; i++)
                    {
                        if (i % buttonsPerRow == 0)
                            EditorGUILayout.BeginHorizontal();

                        var prefab = _blockList[i].prefab;
                        Texture2D thumbnail = _blockList[i].thumbnail;
                        if (GUILayout.Button(new GUIContent(thumbnail,prefab.name), GUILayout.Width(buttonSize), GUILayout.Height(buttonSize)))
                        {
                            InstanceObject(prefab);
                        }

                        if ((i + 1) % buttonsPerRow == 0 || i == _blockList.Count - 1)
                            EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }



        private const float _InstanceDistance = 5f;

        private void InstanceObject(GameObject prefab)
        {
            if (_editingStageData == null) return;

            var position = SceneView.lastActiveSceneView.camera.transform.position +
                           SceneView.lastActiveSceneView.camera.transform.forward * _InstanceDistance;

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.position = position;
                ObjectSnapper.CheckSnapBehaviorObject(instance);
                ObjectSnapper.SnapToGrid(instance.transform);
                Selection.activeGameObject = instance;
            }
        }

        private Vector2 _stageStatusScrollPosition;

        private void OnDrawStageStatus()
        {
            if (_editingStageData == null) return;

            using (var scroll = new EditorGUILayout.ScrollViewScope(
                       _stageStatusScrollPosition,
                       EditorStyles.helpBox,
                       GUILayout.ExpandWidth(true),
                       GUILayout.MaxHeight(100)))
            {
                _stageStatusScrollPosition = scroll.scrollPosition;

                foreach (var log in _warningLogs)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(log.text, GUILayout.ExpandWidth(true));
                        if (log.targetObject.Count > 0)
                        {
                            if (GUILayout.Button("Focus", GUILayout.Width(50)))
                            {
                                Selection.objects = log.targetObject.ToArray();
                            }
                        }
                    }
                }
            }
        }

        private void DeleteStageData(StageData stageData)
        {
            AssetDatabase.DeleteAsset(SaveStageDataFolderPath + "/" + stageData.GetFileName());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_editingStageData == stageData)
            {
                DestroyAllBlocks();
                _editingStageData = null;
            }

            Refresh();
        }

        private void SaveStageData()
        {
            if (_editingStageData == null) return;

            //ShrinkStageData(_editingStageData);
            ShrinkStageDataFromAddressable(_editingStageData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();
        }

        private void LoadStageData(StageData stageData)
        {
            GUI.FocusControl("");

            if (_editingStageData == stageData) return;

            if (_editingStageData != null)
            {
                var num = EditorUtility.DisplayDialogComplex("開く", "変更内容を保存しますか？",
                    "はい",
                    "キャンセル",
                    "保存せずに開く"
                );
                switch (num)
                {
                    case 0:
                        SaveStageData();
                        break;
                    case 1:
                        return;
                    case 2:
                        break;
                }
            }

            _editingStageData = stageData;
            ObjectSnapper.stageArea = stageData.GetStageArea();

            DestroyAllBlocks();

            //ExpandStageData(stageData);
            ExpandStageDataFromAddressable(stageData);

            EditorUtility.SetDirty(_editingStageData);

            Refresh();
        }

        private static void DestroyAllBlocks()
        {
            var obj = GetAllBlocksInScene();
            foreach (var o in obj)
            {
                DestroyImmediate(o);
            }
        }

        private void ExpandStageDataFromAddressable(StageData stageData)
        {
            var guid = stageData.GetStagePrefab().AssetGUID;
            if (guid is null or "") return;

            var key = stageData.GetStagePrefab();

            Addressables
                .LoadAssetAsync<GameObject>(key)
                .Completed += op =>
            {
                var obj = Instantiate(op.Result);

                int count = obj.transform.childCount;
                for (int i = 0; i < count; i++)
                {
                    var child = obj.transform.GetChild(0);
                    child.SetParent(null);
                }

                DestroyImmediate(obj);

                Refresh();
            };
        }

        private void ExpandStageData(StageData stageData)
        {
            if (stageData._stage == null) return;

            var obj = Instantiate(stageData._stage);

            int count = obj.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                var child = obj.transform.GetChild(0);
                child.SetParent(null);
            }

            DestroyImmediate(obj);
        }

        public static List<GameObject> GetAllBlocksInScene()
        {
            var objects = FindObjectsOfType<GameObject>();

            //objectsの中からBlockレイヤーのオブジェクトを抽出
            var blocks = new List<GameObject>();
            foreach (var obj in objects)
            {
                if (obj.layer == LayerMask.NameToLayer(BlockLayerName) &&
                    (obj.transform.parent == null ||
                     obj.transform.parent.gameObject.layer != LayerMask.NameToLayer(BlockLayerName)))
                {
                    blocks.Add(obj);
                }
            }
            
            return blocks;
        }

        private void ShrinkStageDataFromAddressable(StageData stageData)
        {
            var parent = new GameObject("Stage");
            bool isExistSaveData = false;

            var obj = GetAllBlocksInScene();
            foreach (var o in obj)
            {
                isExistSaveData = true;
                Debug.Log(o.name);
                o.transform.SetParent(parent.transform);
            }

            if (!isExistSaveData)
            {
                DestroyImmediate(parent);
                return;
            }

            var prefab =
                PrefabUtility.SaveAsPrefabAsset(parent,
                    SaveStageDataFolderPath + "/" + stageData.GetFileName() + "/" + stageData.GetFileName() +
                    ".prefab");

            //prefabのAddressableを有効にする
            var group = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
            var setting = AddressableAssetSettingsDefaultObject.Settings;

            var entry = setting.CreateOrMoveEntry(
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), group, false, true);

            entry.SetLabel("StageData", true, true);

            AssetReferenceGameObject assetReferenceGameObject = new AssetReferenceGameObject(entry.guid);

            SerializedObject so = new SerializedObject(stageData);

            var prefabProperty = so.FindProperty("_stagePrefab");
            prefabProperty.FindPropertyRelative("m_AssetGUID").stringValue = entry.guid;
            so.ApplyModifiedPropertiesWithoutUndo();


            foreach (var child in obj)
                child.transform.SetParent(null);

            DestroyImmediate(parent);
            
            Debug.Log($"セーブ完了:{AssetDatabase.GetAssetPath(prefab)}");
        }

        private void ShrinkStageData(StageData stageData)
        {
            var parent = new GameObject("Stage");
            bool isExistSaveData = false;

            var obj = GetAllBlocksInScene();
            foreach (var o in obj)
            {
                isExistSaveData = true;
                o.transform.SetParent(parent.transform);
            }

            if (!isExistSaveData)
            {
                DestroyImmediate(parent);
                return;
            }

            var prefab =
                PrefabUtility.SaveAsPrefabAsset(parent,
                    SaveStageDataFolderPath + "/" + stageData.GetFileName() + "/" + stageData.GetFileName() +
                    ".prefab");

            stageData._stage = prefab;

            foreach (var child in obj)
                child.transform.SetParent(null);

            DestroyImmediate(parent);
        }

        private void CreateStageData(string fileName)
        {
            int i = 1;

            while (AssetDatabase.IsValidFolder(SaveStageDataFolderPath + "/" + fileName + i))
                i++;

            var numberingFileName = fileName + i;
            var folderPath = SaveStageDataFolderPath + "/" + numberingFileName;

            if (!AssetDatabase.IsValidFolder(SaveStageDataFolderPath))
            {
                var path = SaveStageDataFolderPath.Replace("/StageData", "");
                AssetDatabase.CreateFolder(path, "StageData");
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(SaveStageDataFolderPath, numberingFileName);

            var fullFileName = folderPath + "/" + numberingFileName + ".asset";

            var stageData = CreateInstance<StageData>();

            AssetDatabase.CreateAsset(stageData, fullFileName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var so = new SerializedObject(stageData);
            EditorUtilities.ChangeStageDataProperty(so, "_stageName", numberingFileName);
            EditorUtilities.ChangeStageDataProperty(so, "_fileName", numberingFileName);
            EditorUtilities.ChangeStageDataProperty(so, "_stageDifficult", 1);
            EditorUtilities.ChangeStageDataProperty(so, "_stageArea", new Vector3Int(10, 10, 10));

            Refresh();
        }
    }
}