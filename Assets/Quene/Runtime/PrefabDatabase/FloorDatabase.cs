using System;
using UnityEngine;

namespace Quene.Runtime
{
    [CreateAssetMenu(fileName = "FloorDatabase", menuName = MenuName, order = 0)]
    public class FloorDatabase : BasePrefabDatabase<FloorKey>
    {
        private const string MenuName = MenuNamePrefix + "Floor";
    }


    /// <summary>
    /// FloorDatabaseで利用されるPrefabと一緒に登録するデータ
    /// </summary>
    [Serializable]
    public struct FloorKey : IEquatable<FloorKey>
    {
        public FloorType FloorType;

        public FloorNeighbor Mask;

        public FloorKey(FloorType floorType, FloorNeighbor neighborMask)
        {
            this.FloorType = floorType;
            this.Mask = neighborMask;
        }

        public static bool operator !=(FloorKey a, FloorKey b) => !a.Equals(b);

        public static bool operator ==(FloorKey a, FloorKey b) => a.Equals(b);

        /// <inheritdoc/>
        public bool Equals(FloorKey other)
        {
            return this.FloorType == other.FloorType &&
                   this.Mask == other.Mask;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is FloorKey other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => ((int)this.FloorType * 397) ^ (int)this.Mask;

        public override string ToString()
        {
            return $"{this.FloorType}-{this.Mask}";
        }
    }

    /// <summary>
    /// 床の種類を表す enum 。
    /// </summary>
    public enum FloorType
    {
        None,
        Black,
        White,
    }

    /// <summary>
    /// 隣接するかどうかのマスク
    /// </summary>
    [Flags]
    public enum FloorNeighbor : byte
    {
        None = 0,
        North = 1 << 0, // 0001b
        East = 1 << 1, // 0010b
        South = 1 << 2, // 0100b
        West = 1 << 3, // 1000b
    }
}