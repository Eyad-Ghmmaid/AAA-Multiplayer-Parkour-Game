using System.Collections.Generic;
using System;
using System.Reflection;


#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace FS_Util
{
    public enum LogicOperator
    {
        And,
        Or
    }


    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionField1 { get; }
        public object ExpectedValue1 { get; }
        public string ConditionField2 { get; }
        public object ExpectedValue2 { get; }
        public ComparisonType Comparison { get; }
        public LogicOperator LogicOp { get; }
        public bool HasTwoConditions { get; }

        public ShowIfAttribute(string conditionField, object expectedValue, ComparisonType comparison = ComparisonType.Equals)
        {
            ConditionField1 = conditionField;
            ExpectedValue1 = expectedValue;
            Comparison = comparison;
        }
        public ShowIfAttribute(LogicOperator logicOperator, string conditionField1, object expectedValue1, string conditionField2, object expectedValue2)
        {
            ConditionField1 = conditionField1;
            ExpectedValue1 = expectedValue1;
            ConditionField2 = conditionField2;
            ExpectedValue2 = expectedValue2;
            LogicOp = logicOperator;
            Comparison = ComparisonType.Equals;
            HasTwoConditions = true;
        }
    }

    public enum ComparisonType
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(FS_Util.ShowIfAttribute), true)]
    public class ShowIfDrawer : PropertyDrawer
    {
        // Cache for reflection calls (speeds up repeated calls)
        static MethodInfo s_GetDrawerTypeForType;
        static bool s_ReflectionInitialized;

        // Optionally cache created drawers across calls to avoid allocations
        // This demo uses a simple temporary instance per call for safety and simplicity.
        const float MISSING_HEIGHT = 0f;
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        void EnsureReflection()
        {
            if (s_ReflectionInitialized) return;

            var editorAsm = typeof(Editor).Assembly;
            // Unity internal class ScriptAttributeUtility contains GetDrawerTypeForType method
            var scriptAttributeUtilityType = editorAsm.GetType("UnityEditor.ScriptAttributeUtility");
            if (scriptAttributeUtilityType != null)
            {
                // Method name can be "GetDrawerTypeForType" (internal, static)
                s_GetDrawerTypeForType = scriptAttributeUtilityType.GetMethod(
                    "GetDrawerTypeForType",
                    BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Type) },
                    null
                );
            }

            s_ReflectionInitialized = true;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIf = (FS_Util.ShowIfAttribute)attribute;

            // If should not show, do nothing (no drawing)
            if (!ShouldShow(property, showIf))
                return;

            EditorGUI.BeginProperty(position, label, property);

            // Try to find the 'real' PropertyDrawer for this field's type
            PropertyDrawer realDrawer = CreateDrawerForProperty(property);

            if (realDrawer != null && realDrawer != this)
            {
                // Safely call the drawer's OnGUI. We pass the same attribute/fieldInfo into the created drawer
                try
                {
                    realDrawer.OnGUI(position, property, label);
                }
                catch (Exception ex)
                {
                    // If the custom drawer fails for any reason, fallback to default property field to avoid breaking the inspector
                    Debug.LogWarning($"ShowIfDrawer: exception while calling real drawer {realDrawer.GetType().Name}: {ex.Message}\nFalling back to default PropertyField.");
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
            else
            {
                // fallback: let Unity draw using default property field (this will use built-in drawers)
                EditorGUI.PropertyField(position, property, label, true);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIf = (FS_Util.ShowIfAttribute)attribute;

            if (!ShouldShow(property, showIf))
                return MISSING_HEIGHT;

            // Try to get the real drawer and ask it for height
            PropertyDrawer realDrawer = CreateDrawerForProperty(property);

            if (realDrawer != null && realDrawer != this)
            {
                try
                {
                    return realDrawer.GetPropertyHeight(property, label);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ShowIfDrawer: exception in GetPropertyHeight of {realDrawer.GetType().Name}: {ex.Message}\nFalling back to EditorGUI.GetPropertyHeight.");
                    return EditorGUI.GetPropertyHeight(property, label, true);
                }
            }

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        // --------------------
        // Drawer resolution & creation logic (reflection)
        // --------------------
        PropertyDrawer CreateDrawerForProperty(SerializedProperty property)
        {
            // initialization for reflection
            EnsureReflection();

            // If reflection helper isn't available, bail to default behavior
            if (s_GetDrawerTypeForType == null)
                return null;

            // Determine the type of the field. The base PropertyDrawer has 'fieldInfo' field we can use.
            Type fieldType = fieldInfo?.FieldType;
            if (fieldType == null)
            {
                // fallback: try to infer type from property (limited)
                fieldType = InferFieldTypeFromProperty(property);
                if (fieldType == null)
                    return null;
            }

            // Call Unity internal method to get the drawer type for the field's type
            Type drawerType = null;
            try
            {
                drawerType = s_GetDrawerTypeForType.Invoke(null, new object[] { fieldType }) as Type;
            }
            catch (Exception)
            {
                drawerType = null;
            }

            if (drawerType == null)
                return null;

            // If the drawerType is the same as this ShowIfDrawer's type, avoid recursion (rare)
            if (drawerType == this.GetType())
                return null;

            // Instantiate the drawer
            PropertyDrawer drawerInstance = null;
            try
            {
                drawerInstance = Activator.CreateInstance(drawerType) as PropertyDrawer;
                if (drawerInstance == null)
                    return null;

                // We must populate the internal fields of PropertyDrawer (m_FieldInfo and m_Attribute) so that
                // the created drawer behaves like Unity-created drawers.
                // Reflection field names are internal — but commonly "m_FieldInfo" and "m_Attribute".
                // Guarded with try/catch so upgrades won't crash.

                // set m_FieldInfo
                try
                {
                    var mFieldInfo = typeof(PropertyDrawer).GetField("m_FieldInfo", InstanceFlags);
                    if (mFieldInfo != null)
                        mFieldInfo.SetValue(drawerInstance, fieldInfo);
                }
                catch { /* ignore */ }

                // set m_Attribute (some drawers read their attribute instance)
                try
                {
                    var mAttrField = typeof(PropertyDrawer).GetField("m_Attribute", InstanceFlags);
                    if (mAttrField != null)
                    {
                        // We don't have the original attribute instance of the other drawer (if it was attribute-driven drawer).
                        // For type-based drawers this can be left null. For attribute-based drawers it's complex to recreate.
                        // Try to find an attribute instance of the drawer's target attribute type on the field; otherwise leave null.
                        var drawerAttrType = GetDrawerAttributeType(drawerType);
                        if (drawerAttrType != null)
                        {
                            // find attribute from the actual field (fieldInfo) if present
                            var attrInstance = fieldInfo?.GetCustomAttribute(drawerAttrType, inherit: true);
                            if (attrInstance != null)
                                mAttrField.SetValue(drawerInstance, attrInstance);
                        }
                    }
                }
                catch { /* ignore */ }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ShowIfDrawer: failed to create drawer instance for type {drawerType?.FullName}: {e.Message}");
                drawerInstance = null;
            }

            return drawerInstance;
        }

        // Try to infer the C# type of the serialized property when fieldInfo is not available (best-effort).
        // This is a fallback and won't be perfect for arrays, generic lists, etc.
        Type InferFieldTypeFromProperty(SerializedProperty property)
        {
            // If it's an object reference, try to use objectReferenceValue
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                return property.objectReferenceValue.GetType();

            // For primitives map the propertyType
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return typeof(int);
                case SerializedPropertyType.Boolean: return typeof(bool);
                case SerializedPropertyType.Float: return typeof(float);
                case SerializedPropertyType.String: return typeof(string);
                case SerializedPropertyType.Enum: return typeof(Enum);
                // A few more could be added if desired
                default: return null;
            }
        }

        // Inspect a drawer type to find if it was written for an attribute and return that attribute type.
        Type GetDrawerAttributeType(Type drawerType)
        {
            try
            {
                var attr = drawerType.GetCustomAttribute<CustomPropertyDrawer>(true);
                if (attr == null)
                    return null;

                // Unity stores the target type inside private field "m_Type"
                var field = typeof(CustomPropertyDrawer)
                    .GetField("m_Type", BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null)
                    return field.GetValue(attr) as Type;
            }
            catch { }

            return null;
        }

        // --------------------
        // ShouldShow & helpers (adapted from your earlier code)
        // --------------------
        public static bool ShouldShow(SerializedProperty property, FS_Util.ShowIfAttribute showIf)
        {
            if (!showIf.HasTwoConditions)
            {
                return EvaluateCondition(property, showIf.ConditionField1, showIf.ExpectedValue1, showIf.Comparison);
            }

            bool c1 = EvaluateCondition(property, showIf.ConditionField1, showIf.ExpectedValue1, showIf.Comparison);
            bool c2 = EvaluateCondition(property, showIf.ConditionField2, showIf.ExpectedValue2, showIf.Comparison);

            return showIf.LogicOp == FS_Util.LogicOperator.And ? (c1 && c2) : (c1 || c2);
        }

        static bool EvaluateCondition(SerializedProperty property, string conditionField, object expectedValue, FS_Util.ComparisonType comparison)
        {
            var conditionProperty = FindRelativeProperty(property, conditionField);
            if (conditionProperty == null) return false;

            object left = GetPropertyValue(conditionProperty);
            if (left == null) return false;

            return CompareValues(left, expectedValue, comparison);
        }

        static SerializedProperty FindRelativeProperty(SerializedProperty property, string relativePath)
        {
            var so = property.serializedObject;
            var found = so.FindProperty(relativePath);
            if (found != null) return found;

            var fullPath = property.propertyPath; // e.g. someList.Array.data[0].field
            int lastDot = fullPath.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var parentPath = fullPath.Substring(0, lastDot + 1);
                var candidate = parentPath + relativePath;
                found = so.FindProperty(candidate);
                if (found != null) return found;
            }

            // Walk up
            var parts = fullPath.Split('.');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string prefix = string.Join(".", parts, 0, i);
                if (string.IsNullOrEmpty(prefix)) continue;
                var candidate = prefix + "." + relativePath;
                found = so.FindProperty(candidate);
                if (found != null) return found;
            }

            return null;
        }

        static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Enum: return prop.enumValueIndex;
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue;
                default: return null;
            }
        }

        static bool CompareValues(object a, object b, FS_Util.ComparisonType comparison)
        {
            if (a == null || b == null) return false;

            // numeric comparison if possible
            try
            {
                double da = Convert.ToDouble(a);
                double db = Convert.ToDouble(b);

                return comparison switch
                {
                    FS_Util.ComparisonType.Equals => da == db,
                    FS_Util.ComparisonType.NotEquals => da != db,
                    FS_Util.ComparisonType.GreaterThan => da > db,
                    FS_Util.ComparisonType.LessThan => da < db,
                    FS_Util.ComparisonType.GreaterThanOrEqual => da >= db,
                    FS_Util.ComparisonType.LessThanOrEqual => da <= db,
                    _ => false,
                };
            }
            catch
            {
                // fallback to object equals (for strings/enums/bools)
                bool eq = a.Equals(b);
                return comparison == FS_Util.ComparisonType.Equals ? eq : !eq;
            }
        }
    }
#endif
}