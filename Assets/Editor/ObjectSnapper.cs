using System;
using UnityEditor;
using UnityEngine;

namespace QBuild.StageEditor
{
    [InitializeOnLoad]
    public class ObjectSnapper
    {
        private const float SnapDistance = 1.0f;

        public static float GetSnapDistance()
        {
            return SnapDistance;
        }

        private static float _snapOffset = 0.0f;
        public static Vector3Int stageArea;

        private static bool _isEnable;

        public static bool IsEnable
        {
            get => _isEnable;
            set
            {
                _isEnable = value;
                EditorPrefs.SetBool("ObjectSnapper.isEnable", value);
                Debug.Log(value);
            }
        }

        public static bool isEnableArea = true;

        private static bool _isBlockOnly;

        public static bool IsBlockOnly
        {
            get => _isBlockOnly;
            set
            {
                _isBlockOnly = value;
                EditorPrefs.SetBool("ObjectSnapper.isBlockOnly", value);
                Debug.Log(value);
            }
        }


        private static Vector3 _prevPos;


        static ObjectSnapper()
        {
            //var path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:SnapBehaviorObject")[0]);
            _isEnable = EditorPrefs.GetBool("ObjectSnapper.isEnable", true);
            _isBlockOnly = EditorPrefs.GetBool("ObjectSnapper.isBlockOnly", true);
            Debug.Log(EditorPrefs.GetBool("ObjectSnapper.isEnable", true));
        }

        private void OnEnable()
        {
   
        }

        public static void SnapToGrid(Transform transform)
        {
            var pos = transform.position;

            var snapPos = pos;

            if (_isEnable)
            {
                //1mごとにスナップ
                snapPos = new Vector3(
                    Mathf.Round(pos.x / SnapDistance - _snapOffset) * SnapDistance + _snapOffset,
                    Mathf.Round(pos.y / SnapDistance) * SnapDistance,
                    Mathf.Round(pos.z / SnapDistance - _snapOffset) * SnapDistance + _snapOffset
                );
            }

            if (isEnableArea)
            {
                snapPos.x = Mathf.Clamp(
                    snapPos.x,
                    -stageArea.x / 2.0f + _snapOffset,
                    stageArea.x / 2.0f - _snapOffset
                );
                snapPos.y = Mathf.Clamp(snapPos.y, 0, stageArea.y);
                snapPos.z = Mathf.Clamp(
                    snapPos.z,
                    -stageArea.z / 2.0f + _snapOffset,
                    stageArea.z / 2.0f - _snapOffset
                );
            }

            transform.position = snapPos;
            _prevPos = snapPos;
        }
    }
}