using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    [CustomEditor(typeof(ThrowableItem), editorForChildClasses: true)]
    public class ThrowableItemEditor : EquippableItemEditor
    {
        // Flight
        SerializedProperty maxThrowDistance;
        SerializedProperty maxAimRange;
        SerializedProperty maxLifetime;

        // Toss
        SerializedProperty tossDistanceThreshold;
        SerializedProperty useLowToss;
        SerializedProperty highToss;
        SerializedProperty lowToss;

        // Aim camera
        SerializedProperty useAimCamera;
        SerializedProperty aimCameraSettings;

        // Recovery
        SerializedProperty isRecoverable;

        // Hit effects
        SerializedProperty hitEffects;

        static bool _flightFoldout = true;
        static bool _tossFoldout = true;
        static bool _cameraFoldout = true;
        static bool _recoveryFoldout = true;
        static bool _hitEffectsFoldout = true;

        public override void OnEnable()
        {
            base.OnEnable();

            maxThrowDistance = serializedObject.FindProperty("maxThrowDistance");
            maxAimRange = serializedObject.FindProperty("maxAimRange");
            maxLifetime = serializedObject.FindProperty("maxLifetime");
            tossDistanceThreshold = serializedObject.FindProperty("tossDistanceThreshold");
            useLowToss = serializedObject.FindProperty("useLowToss");
            highToss = serializedObject.FindProperty("highToss");
            lowToss = serializedObject.FindProperty("lowToss");
            useAimCamera = serializedObject.FindProperty("useAimCamera");
            aimCameraSettings = serializedObject.FindProperty("aimCameraSettings");
            isRecoverable = serializedObject.FindProperty("isRecoverable");
            hitEffects = serializedObject.FindProperty("hitEffects");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();

            DrawThrowableInspector();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawThrowableInspector()
        {
            DrawFoldout(ref _flightFoldout, "Flight", () =>
            {
                EditorGUILayout.PropertyField(maxThrowDistance);
                EditorGUILayout.PropertyField(maxAimRange);
                EditorGUILayout.PropertyField(maxLifetime);
            });

            DrawFoldout(ref _tossFoldout, "Toss", () =>
            {
                DrawTossData(highToss, "High Toss (overhand)", showAnimations: true);

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(useLowToss, new GUIContent("Use Low Toss"));

                if (useLowToss.boolValue)
                {
                    EditorGUILayout.PropertyField(tossDistanceThreshold, new GUIContent("Distance Threshold"));
                    EditorGUILayout.Space(4);
                    DrawTossData(lowToss, "Low Toss (underhand)", showAnimations: true);
                }
            });

            DrawFoldout(ref _cameraFoldout, "Aim Camera", () =>
            {
                EditorGUILayout.PropertyField(useAimCamera, new GUIContent("Use Aim Camera"));
                if (useAimCamera.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(aimCameraSettings, new GUIContent("Camera Settings"), true);
                    EditorGUI.indentLevel--;
                }
            });

            DrawFoldout(ref _recoveryFoldout, "Recovery", () =>
            {
                EditorGUILayout.PropertyField(isRecoverable);
            });

            DrawFoldout(ref _hitEffectsFoldout, "Hit Effects", DrawHitEffectsList);
        }

        void DrawTossData(SerializedProperty toss, string label, bool showAnimations)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (showAnimations)
            {
                EditorGUILayout.PropertyField(toss.FindPropertyRelative("aimAnimation"), new GUIContent("Aim Animation"));
                EditorGUILayout.PropertyField(toss.FindPropertyRelative("throwAnimation"), new GUIContent("Throw Animation"));
                EditorGUILayout.PropertyField(toss.FindPropertyRelative("dropStartTime"), new GUIContent("Drop Start Time"));
            }
            EditorGUILayout.PropertyField(toss.FindPropertyRelative("arcHeight"), new GUIContent("Arc Height"));
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        protected void DrawHitEffectsList()
        {
            for (int i = 0; i < hitEffects.arraySize; i++)
            {
                var element = hitEffects.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                element.isExpanded = EditorGUILayout.Foldout(
                    element.isExpanded, $"Effect {i + 1}", true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("\u2212", GUILayout.Width(25)))
                {
                    hitEffects.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (element.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("layer"), new GUIContent("Layer"));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("hitMarker"), new GUIContent("Hit Marker"));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("hitEffect"), new GUIContent("Hit Particle"));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("hitAudioClip"), new GUIContent("Hit Sound"));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("landingSoundRadius"), new GUIContent("Landing Sound Radius"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (hitEffects.arraySize > 0) GUILayout.FlexibleSpace();
            string btnLabel = hitEffects.arraySize == 0 ? "Add Hit Effect" : "+";
            float btnWidth = hitEffects.arraySize == 0 ? 150f : 25f;
            if (GUILayout.Button(btnLabel, GUILayout.Width(btnWidth)))
            {
                hitEffects.InsertArrayElementAtIndex(hitEffects.arraySize);
                hitEffects.GetArrayElementAtIndex(hitEffects.arraySize - 1).isExpanded = true;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
