using FS_Core;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    [CustomEditor(typeof(Item))]
    public class ItemDataEditor : AnimationPreviewHandler
    {
        Item item;

        private new SerializedProperty name;
        private SerializedProperty description;
        private SerializedProperty icon;
        private SerializedProperty modelPrefab;



        public SerializedProperty equipmentSlotIndex;
        public SerializedProperty attachOnBody;
        public SerializedProperty usePhysicsForAttachedItem;
        public SerializedProperty overridePhysicsPrefab;
        public SerializedProperty physicsPrefab;
        public SerializedProperty useCustomAttachmentModel;
        public SerializedProperty modelToAttach;
        public SerializedProperty isSkinnedMesh;
        public SerializedProperty hasHolder;
        public SerializedProperty holderPrefab;
        public SerializedProperty attachBone;

        public SerializedProperty defaultAttachReference;
        public SerializedProperty attachReferences;

        public SerializedProperty holderLocalPosition;
        public SerializedProperty holderLocalRotation;
        public SerializedProperty holderAttachReferences;
        public SerializedProperty itemLocalPosition;
        public SerializedProperty itemLocalRotation;
        public SerializedProperty itemAttachReferences;

        static bool generalSettingsFoldout = true;
        static bool attachmentSettingsFoldout = true;
        static bool attributesFoldout = true;
        static bool holderAttachReferenceFoldout;

        public override void OnEnable()
        {
            item = (Item)target;

            name = serializedObject.FindProperty("name");
            description = serializedObject.FindProperty("description");
            icon = serializedObject.FindProperty("icon");
            modelPrefab = serializedObject.FindProperty("modelPrefab");

            equipmentSlotIndex = serializedObject.FindProperty("equipmentSlotIndex");
            attachOnBody = serializedObject.FindProperty("attachOnBody");
            usePhysicsForAttachedItem = serializedObject.FindProperty("usePhysicsForAttachedItem");
            overridePhysicsPrefab = serializedObject.FindProperty("overridePhysicsPrefab");
            physicsPrefab = serializedObject.FindProperty("physicsPrefab");
            useCustomAttachmentModel = serializedObject.FindProperty("useCustomAttachmentModel");
            modelToAttach = serializedObject.FindProperty("modelToAttach");
            isSkinnedMesh = serializedObject.FindProperty("isSkinnedMesh");
            hasHolder = serializedObject.FindProperty("hasHolder");
            holderPrefab = serializedObject.FindProperty("holderPrefab");
            attachBone = serializedObject.FindProperty("attachBone");
            holderLocalPosition = serializedObject.FindProperty("holderLocalPosition");
            holderLocalRotation = serializedObject.FindProperty("holderLocalRotation");
            holderAttachReferences = serializedObject.FindProperty("holderAttachReferences");
            itemLocalPosition = serializedObject.FindProperty("itemLocalPosition");
            itemLocalRotation = serializedObject.FindProperty("itemLocalRotation");
            itemAttachReferences = serializedObject.FindProperty("itemAttachReferences");

            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawFoldout(ref generalSettingsFoldout, "General Settings", () =>
            {
                EditorGUILayout.PropertyField(name);
                EditorGUILayout.PropertyField(description, GUILayout.Height(45));
                icon.objectReferenceValue = (Sprite)EditorGUILayout.ObjectField("Icon", icon.objectReferenceValue, typeof(Sprite), false);

                EditorGUILayout.PropertyField(modelPrefab);
                // Attributes Section
                if (item.category != null)
                {
                    GUILayout.Space(5);
                    if (item.Attributes.Count > 0 || item.category.Attributes.Count > 0)
                    {
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {

                            if (item.Attributes.Count != item.category.Attributes.Count)
                                item.category.UpdateItemAttributeBasedOnCategory(item);
                            attributesFoldout = EditorGUILayout.Foldout(attributesFoldout, "Attribute Values", true);
                            if (attributesFoldout)
                            {
                                EditorGUI.indentLevel++;
                                foreach (var attribute in item.Attributes)
                                {
                                    switch (attribute.attributeType)
                                    {
                                        case ItemAttributeType.Integer:
                                            attribute.intValue = EditorGUILayout.IntField(attribute.attributeName, attribute.intValue);
                                            break;
                                        case ItemAttributeType.Decimal:
                                            attribute.floatValue = EditorGUILayout.FloatField(attribute.attributeName, attribute.floatValue);
                                            break;
                                        case ItemAttributeType.Text:
                                            attribute.stringValue = EditorGUILayout.TextField(attribute.attributeName, attribute.stringValue);
                                            break;
                                    }
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                    }
                }
            });

            DrawFoldout(ref attachmentSettingsFoldout, "Attachment", () =>
            {
                EditorGUILayout.PropertyField(attachOnBody, new GUIContent("Attach On Body"));
                if (attachOnBody.boolValue)
                {
                    EditorGUILayout.PropertyField(isSkinnedMesh);

                    EditorGUILayout.PropertyField(useCustomAttachmentModel);
                    if (useCustomAttachmentModel.boolValue)
                        EditorGUILayout.PropertyField(modelToAttach);

                    if (!isSkinnedMesh.boolValue)
                    {
                        EditorGUILayout.PropertyField(hasHolder);
                        if (hasHolder.boolValue)
                        {
                            DrawFoldout(ref holderAttachReferenceFoldout, "Holder Configuration", () =>
                            {
                                EditorGUILayout.PropertyField(holderPrefab);
                                DrawTransformSection("Default", holderLocalPosition, holderLocalRotation);
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                EditorGUILayout.PropertyField(holderAttachReferences, new GUIContent("Per Character Reference"));
                                EditorGUILayout.EndVertical();
                            });
                        }
                        EditorGUILayout.PropertyField(usePhysicsForAttachedItem);
                        if (usePhysicsForAttachedItem.boolValue)
                            EditorGUILayout.PropertyField(overridePhysicsPrefab);
                        if (overridePhysicsPrefab.boolValue)
                            EditorGUILayout.PropertyField(physicsPrefab);
                        EditorGUILayout.PropertyField(attachBone);

                        DrawTransformSection("Default", itemLocalPosition, itemLocalRotation);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(itemAttachReferences, new GUIContent("Per Character Reference"));
                        EditorGUILayout.EndVertical();
                    }

                }

            });
            serializedObject.ApplyModifiedProperties();
        }

        public void DrawTransformSection(string label, SerializedProperty position, SerializedProperty rotation)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(position, new GUIContent("Position"));
            EditorGUILayout.PropertyField(rotation, new GUIContent("Rotation"));
            EditorGUILayout.EndVertical();
        }

        public void DrawFoldout(ref bool toggle, string label, System.Action drawer)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Start HelpBox
            EditorGUI.indentLevel++;
            toggle = EditorGUILayout.Foldout(toggle, label, true);
            if (toggle)
            {
                EditorGUI.indentLevel++;
                drawer();
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical(); // End HelpBox

        }
    }
}
