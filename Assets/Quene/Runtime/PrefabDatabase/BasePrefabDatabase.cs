namespace Quene.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// PrefabEntry<T> のリストを持ち、GetPrefab(key) で取得できる汎用データベース。
    /// T は enum やシリアライズ可能な struct を想定。
    /// </summary>
    /// <typeparam name="T">キーの型</typeparam>
    public abstract class BasePrefabDatabase<T> : ScriptableObject
        where T : IEquatable<T>
    {
        protected const string MenuNamePrefix = "Queen/PrefabDatabase/";

        [field: SerializeField]
        public List<PrefabEntry<T>> Entries { get; private set; }

        /// <summary>
        /// key に対応する Prefab を返します。見つからなければ null。
        /// </summary>
        public GameObject GetPrefab(T key)
        {
            for (var i = 0; i < this.Entries.Count; i++)
            {
                Debug.Log(this.Entries[i].Key.ToString() + ":" + key.ToString());
                if (this.Entries[i].Key.Equals(key))
                {
                    return this.Entries[i].Prefab;
                }
            }

            return null;
        }

        protected virtual void Validate()
        {
        }

        // Inspector 上で“entries”が変更されたときに呼び出したい場合
        protected virtual void OnValidate()
        {
            this.Validate();
        }
    }

    /// <summary>
    /// T をキーとして、対応する GameObject Prefab を格納する汎用エントリ
    /// </summary>
    [Serializable]
    public struct PrefabEntry<T>
    {
        [Tooltip("マッピングのキー (Enum や struct など)")]
        public T Key;

        [Tooltip("キーに対応して生成したいPrefab")]
        public GameObject Prefab;
    }
}