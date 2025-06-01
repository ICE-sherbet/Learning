using System;
using UnityEngine;

namespace Quene.Runtime
{
    [CreateAssetMenu(fileName = "SlopeDatabase", menuName = MenuName, order = 0)]
    public class SlopeDatabase : BasePrefabDatabase<FloorKey>
    {
        private const string MenuName = MenuNamePrefix + "Slope";
    }

    [Serializable]
    public struct SlopeKey : IEquatable<SlopeKey>
    {
        public float Height;
        public FloorNeighbor Mask;

        public static bool operator !=(SlopeKey a, SlopeKey b) => !a.Equals(b);

        public static bool operator ==(SlopeKey a, SlopeKey b) => a.Equals(b);

        /// <inheritdoc/>
        public bool Equals(SlopeKey other)
        {
            return Mathf.Approximately(this.Height, other.Height) &&
                   this.Mask == other.Mask;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is SlopeKey other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Height, (int)this.Mask);
        }
    }
}