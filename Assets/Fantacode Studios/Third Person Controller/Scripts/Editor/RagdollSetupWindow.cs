using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using Type = System.Type;

namespace FS_Core
{
    public class RagdollSetupWindow : EditorWindow
    {
        [Tooltip("Animator that uses a humanoid rig.")]
        private Animator targetAnimator;

        private Vector2 scroll;

        private List<ColliderBackup> savedColliders = new List<ColliderBackup>();
        private List<RigidbodyBackup> savedRigidbodies = new List<RigidbodyBackup>();
        private List<GameObject> duplicatedObjects = new List<GameObject>();

        [MenuItem("Tools/FS System/FS Tools/Ragdoll Setup")]
        public static void ShowWindow()
        {
            var win = GetWindow<RagdollSetupWindow>("Ragdoll Setup");
            win.minSize = new Vector2(420, 520);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Select Humanoid Model", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);

                targetAnimator = (Animator)EditorGUILayout.ObjectField(
                    new GUIContent("Target Animator", "Assign a humanoid Animator to begin setup"),
                    targetAnimator,
                    typeof(Animator),
                    true
                );

                if (targetAnimator == null)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox("No Animator assigned. Please assign a valid Animator component from a humanoid model.", MessageType.Warning);
                    EditorGUILayout.EndScrollView();
                    return;
                }
                else if (!targetAnimator.isHuman)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox("Invalid rig type. The assigned Animator must use a Humanoid rig for bone mapping to work correctly.", MessageType.Error);
                    EditorGUILayout.EndScrollView();
                    return;
                }
                else
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.HelpBox("Valid humanoid model detected. Ready to proceed with setup.", MessageType.Info);
                }
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Ragdoll Setup", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Click 'Create Ragdoll' to automatically set up physics bones and joint configurations for the assigned humanoid model.", MessageType.Info);
                EditorGUILayout.Space(6);

                if (GUILayout.Button("Create Ragdoll", GUILayout.Height(28)))
                {
                    Type ragdollType = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        ragdollType = assembly.GetType("UnityEditor.RagdollBuilder");
                        if (ragdollType != null)
                            break;
                    }

                    var wizard = Activator.CreateInstance(ragdollType);
                    SaveExistingPhysicsComponents(targetAnimator.transform);
                    //Type ragdollType = typeof(Editor).Assembly.GetType("UnityEditor.RagdollBuilder");
                    //var wizard = ScriptableWizard.CreateInstance(ragdollType) as ScriptableWizard;

                    SetBone(wizard, ragdollType, "pelvis", targetAnimator.GetBoneTransform(HumanBodyBones.Hips));
                    SetBone(wizard, ragdollType, "leftHips", targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg));
                    SetBone(wizard, ragdollType, "leftKnee", targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
                    SetBone(wizard, ragdollType, "leftFoot", targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot));
                    SetBone(wizard, ragdollType, "rightHips", targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg));
                    SetBone(wizard, ragdollType, "rightKnee", targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg));
                    SetBone(wizard, ragdollType, "rightFoot", targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot));
                    SetBone(wizard, ragdollType, "middleSpine", targetAnimator.GetBoneTransform(HumanBodyBones.Chest));
                    SetBone(wizard, ragdollType, "head", targetAnimator.GetBoneTransform(HumanBodyBones.Head));
                    SetBone(wizard, ragdollType, "leftArm", targetAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
                    SetBone(wizard, ragdollType, "leftElbow", targetAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
                    SetBone(wizard, ragdollType, "rightArm", targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm));
                    SetBone(wizard, ragdollType, "rightElbow", targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm));

                    MethodInfo updateMethod = ragdollType.GetMethod("OnWizardUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                    updateMethod?.Invoke(wizard, null);

                    MethodInfo createMethod = ragdollType?.GetMethod("OnWizardCreate", BindingFlags.Instance | BindingFlags.NonPublic);
                    createMethod?.Invoke(wizard, null);

                    RestoreSavedPhysicsComponents();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Uses reflection to assign bones into Unity's internal Ragdoll Wizard fields.
        /// </summary>
        static void SetBone(object wizard, Type ragdollType, string fieldName, Transform bone)
        {
            FieldInfo field = ragdollType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(wizard, bone);
                bone.gameObject.layer = LayerMask.NameToLayer("RagdollBone");
            }
        }

        private void SaveExistingPhysicsComponents(Transform root)
        {
            savedColliders.Clear();
            savedRigidbodies.Clear();
            duplicatedObjects.Clear();

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (col == null) continue;

                var colliderData = new ColliderBackup
                {
                    target = col.gameObject,
                    colliderType = col.GetType(),
                    originalCollider = UnityEngine.Object.Instantiate(col)
                };
                savedColliders.Add(colliderData);

                if (!duplicatedObjects.Contains(colliderData.originalCollider.gameObject))
                    duplicatedObjects.Add(colliderData.originalCollider.gameObject);
            }

            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null) continue;

                var rigidBodyData = new RigidbodyBackup
                {
                    target = rb.gameObject,
                    originalRigidbody = UnityEngine.Object.Instantiate(rb)
                };
                savedRigidbodies.Add(rigidBodyData);

                if (!duplicatedObjects.Contains(rigidBodyData.originalRigidbody.gameObject))
                    duplicatedObjects.Add(rigidBodyData.originalRigidbody.gameObject);
            }
        }

        private void RestoreSavedPhysicsComponents()
        {
            foreach (var backup in savedColliders)
            {
                if (backup.target == null) continue;
                if (backup.target.GetComponent(backup.colliderType) == null)
                {
                    var newCol = backup.target.AddComponent(backup.colliderType) as Collider;
                    if (newCol != null)
                        EditorUtility.CopySerialized(backup.originalCollider, newCol);
                }
            }

            foreach (var backup in savedRigidbodies)
            {
                if (backup.target == null) continue;
                if (backup.target.GetComponent<Rigidbody>() == null)
                {
                    var newRB = backup.target.AddComponent<Rigidbody>();
                    if (newRB != null)
                        EditorUtility.CopySerialized(backup.originalRigidbody, newRB);
                }
            }

            foreach (var obj in duplicatedObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
        }
    }

    [Serializable]
    public class ColliderBackup
    {
        public GameObject target;
        public Type colliderType;
        public Collider originalCollider;
    }

    [Serializable]
    public class RigidbodyBackup
    {
        public GameObject target;
        public Rigidbody originalRigidbody;
    }
}
