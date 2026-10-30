#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace FS_Core
{


    public class WayPointsNetwork : MonoBehaviour
    {
        public bool drawPathCount = true;
        public bool onlyDrawWhenSelected = false;

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if (!onlyDrawWhenSelected)
                DrawWayPoints();
        }

        public void OnDrawGizmosSelected()
        {
            if (onlyDrawWhenSelected)
                DrawWayPoints();
        }

        void DrawWayPoints()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var pos = transform.GetChild(i).position;
                if (drawPathCount)
                    Handles.Label(pos, (i + 1).ToString(), new GUIStyle() { fontSize = 25 });
                Gizmos.DrawSphere(pos, 0.2f);
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild((i + 1) % transform.childCount).position);
            }
        }
#endif
    }
}
