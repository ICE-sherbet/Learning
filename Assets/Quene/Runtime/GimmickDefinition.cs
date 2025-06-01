using System;
using UnityEngine;

namespace Quene.Runtime
{
    /// <summary>
    /// ギミック定義の親クラス。共通メタ情報や共通メソッドをここにまとめる
    /// </summary>
    public abstract class GimmickDefinition : ScriptableObject, IEquatable<GimmickDefinition>
    {
        protected const string MenuNamePrefix = "Queen/GimmickDefinition/";

        [Header("基底メタ情報")]
        [Tooltip("インスペクタやツールで使う、表示用の名前")]
        public string DisplayName;

        [Tooltip("ツール上で使うアイコン")]
        public Texture2D Icon;

        [Tooltip("シーン上にInstantiateするPrefab")]
        public GameObject Prefab;

        /// <summary>
        /// ギミック配置時に GameObject をセットアップするための抽象メソッド。
        /// 具体的な子クラスでオーバーライドし、インスタンス化直後に呼び出す。
        /// </summary>
        /// <param name="inst">Instantiate された GameObject</param>
        public abstract void OnPlace(GameObject inst);

        /// <inheritdoc/>
        public bool Equals(GimmickDefinition other)
        {
            return base.Equals(other);
        }
    }
}