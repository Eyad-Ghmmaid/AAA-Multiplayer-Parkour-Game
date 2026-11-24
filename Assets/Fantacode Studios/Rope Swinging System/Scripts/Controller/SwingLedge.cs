using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS_SwingSystem
{

    public class SwingLedge : MonoBehaviour
    {
        public bool showHookPoint = true;
        public float gizmosRadius = .1f;
        [HideInInspector] public Vector3 hookPoint = new Vector3(0, .1f, 0);



        private void OnDrawGizmos()
        {
            if (showHookPoint)
            {
                Gizmos.color = Color.green;
                var pos = transform.TransformPoint(hookPoint);
                Gizmos.DrawSphere(pos, gizmosRadius);
            }
        }

    }

#if UNITY_EDITOR

    [CustomEditor(typeof(SwingLedge))]
    public class SwingLedgeEditor : Editor
    {
        private void OnSceneGUI()
        {
            var tar = target as SwingLedge;
            if (tar.showHookPoint)
            {
                var pos = tar.transform.TransformPoint(tar.hookPoint);

                Tool currentTool = Tools.current;

                if (currentTool == Tool.Move)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPosition = Handles.PositionHandle(pos, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(tar, "Move Transform");
                        tar.hookPoint = tar.transform.InverseTransformPoint(newPosition);
                    }
                }
            }
        }
    }

#endif
}