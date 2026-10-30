#if UNITY_EDITOR
using FS_Util;
using UnityEditor;
using UnityEngine;

namespace FS_ParkourSystem
{
    [CustomEditor(typeof(ParkourAction))]
    public class ParkourActionEditor : Editor
    {
        SerializedProperty rootMotionCurves;
        SerializedProperty rootCurveX, rootCurveY, rootCurveZ;
        SerializedProperty boneRootOffset;
        SerializedProperty retargetedWarpDispProp;

        // Editor-only — not stored on the asset. Static persists across inspector redraws.
        static Animator referenceAnimator;

        void OnEnable()
        {
            rootMotionCurves = serializedObject.FindProperty("rootMotionCurves");
            rootCurveX = rootMotionCurves.FindPropertyRelative("CurveX");
            rootCurveY = rootMotionCurves.FindPropertyRelative("CurveY");
            rootCurveZ = rootMotionCurves.FindPropertyRelative("CurveZ");
            boneRootOffset = serializedObject.FindProperty("boneRootOffset");
            retargetedWarpDispProp = serializedObject.FindProperty("retargetedWarpDisplacement");

            if (referenceAnimator == null)
            {
                var defaultPrefab = Resources.Load<GameObject>("Default Character");
                if (defaultPrefab != null)
                {
                    Debug.Log("Animator assigned");
                    referenceAnimator = defaultPrefab.GetComponent<Animator>();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Curve Baking", EditorStyles.boldLabel);

            referenceAnimator = (Animator)EditorGUILayout.ObjectField(
                "Reference Animator", referenceAnimator, typeof(Animator), allowSceneObjects: false);

            EditorGUILayout.HelpBox(
                "Drag in your character prefab's Animator (prefab only — scene objects are not supported). " +
                "Required to bake root motion curves and bone-root offset.",
                MessageType.Info);

            if (GUILayout.Button("Bake Curves"))
                BakeCurves();

            serializedObject.ApplyModifiedProperties();
        }

        void BakeCurves()
        {
            var action = (ParkourAction)target;
            var clip   = action.ClipInfo?.clip;
            if (clip == null)
            {
                Debug.LogError("[ParkourActionEditor] No clip assigned on ClipInfo.");
                return;
            }

            if (referenceAnimator == null)
            {
                Debug.LogError("[ParkourActionEditor] No reference animator provided. Please assign a prefab with animator and tr again.");
                return;
            }

            serializedObject.Update();

            var matchBone = AvatarTargetToHumanBodyBones(action.MatchBodyPart);

            // Sample via ClipSampler
            var result = ClipSampler.Sample(referenceAnimator.gameObject, clip, matchBone);

            // ── 1. Root motion curves ─────────────────────────────────────────────────
            // Replace raw AnimationUtility RootT curves with properly retargeted curves
            // produced by PlayableGraph through the character's humanoid avatar.
            rootCurveX.animationCurveValue = result.rootPosX;
            rootCurveY.animationCurveValue = result.rootPosY;
            rootCurveZ.animationCurveValue = result.rootPosZ;

            // Bone root offset
            if (matchBone == HumanBodyBones.LastBone)
            {
                boneRootOffset.vector3Value = Vector3.zero;
            }
            else
            {
                float tEnd   = action.MatchTargetTime * clip.length;
                var boneAtEnd = result.EvaluateBonePosition(tEnd);
                boneRootOffset.vector3Value = -boneAtEnd;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ParkourActionEditor] Baked '{action.name}'. " +
                      $"TotalDisp={result.TotalDisplacement:F3}  BoneRootOffset={boneRootOffset.vector3Value:F3}");
        }

        static HumanBodyBones AvatarTargetToHumanBodyBones(AvatarTarget target) => target switch
        {
            AvatarTarget.LeftFoot  => HumanBodyBones.LeftFoot,
            AvatarTarget.RightFoot => HumanBodyBones.RightFoot,
            AvatarTarget.LeftHand  => HumanBodyBones.LeftHand,
            AvatarTarget.RightHand => HumanBodyBones.RightHand,
            _                      => HumanBodyBones.LastBone
        };
    }
}
#endif
