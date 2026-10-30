using UnityEngine;

namespace FS_Util
{
    public class LayersUtil
    {
        public static bool LayerMaskContainsLayer(LayerMask layerMask, int layer)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }
    }
}
