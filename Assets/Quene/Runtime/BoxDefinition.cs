using UnityEngine;

namespace Quene.Runtime
{
    [CreateAssetMenu(fileName = "BoxDefinition", menuName = MenuNamePrefix + "Box")]
    public class BoxDefinition : GimmickDefinition
    {
        public override void OnPlace(GameObject inst)
        {
            throw new System.NotImplementedException();
        }
    }
}