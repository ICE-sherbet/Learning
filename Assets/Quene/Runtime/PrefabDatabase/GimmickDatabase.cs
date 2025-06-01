using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quene.Runtime
{
    [CreateAssetMenu(fileName = "GimmickDatabase", menuName = MenuName)]
    public class GimmickDatabase : BasePrefabDatabase<GimmickDefinition>
    {
        private const string MenuName = MenuNamePrefix + "Floor";

        /// <summary>
        /// キー一覧を返す。UI で「どのギミック定義を使えるか」を列挙したいときに使う。
        /// </summary>
        /// <returns>キー一覧</returns>
        public IEnumerable<GimmickDefinition> GetAllDefinitions()
        {
            return from e in this.Entries where e.Key != null select e.Key;
        }
    }
}