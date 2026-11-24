using FS_Core;
using UnityEngine;

namespace FS_SwingSystem
{
    [CreateAssetMenu(fileName = "New Hook", menuName = "Swing System/Create Hook")]
    public class SwingHookItem : EquippableItem
    {
        public AnimGraphClipInfo ropeThrowingClip;
        public override bool RestoreAfterActionStow => true;

        public override void SetCategory()
        {
            category = Resources.Load<ItemCategory>("Category/Swing Hook");
        }
    }
}
