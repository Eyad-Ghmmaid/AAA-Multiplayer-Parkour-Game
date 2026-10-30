using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    public class PersistentGizmosDrawer : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            PersistentGizmos.DrawGizmos();
        }

        private void OnDestroy()
        {
            PersistentGizmos.Clear();
        }
    }
}
