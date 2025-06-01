using System;
using UnityEngine;

namespace Quene.Runtime
{
    [Serializable]
    public struct GimmickCell
    {
        [Tooltip("配置されたギミック定義 (null ならギミック無し)")]
        public GimmickDefinition definition;

        [Tooltip("ギミックのY軸周りの向き (0/90/180/270° など)")]
        public Quaternion rotation;
    }

    [Serializable]
    public struct GimmickCellEntry
    {
        public Vector3Int position; // (x,y,z)
        public GimmickCell cell;
    }
}