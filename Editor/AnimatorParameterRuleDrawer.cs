#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AnimatorTriggerSystem
{
    [CustomPropertyDrawer(typeof(AnimatorParameterRule))]
    public class AnimatorParameterRuleDrawer : PropertyDrawer
    {
        private const float LINE_HEIGHT = 18f;
        private const float SPACING = 2f;
        private const float INDENT = 15f;
        
        // Cache for field options with nested properties
        private static Dictionary<string, string[]> fieldOptionsCache = new Dictionary<string, string[]>();
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = 1; // Foldout header
            
            if (property.isExpanded)
            {
                lines += 2; // Enabled + Parameter Name
                lines += 1; // Parameter Type
                lines += 1; // Rule Type
                lines += 1; // Source Object
                lines += 1; // Source Component
                lines += 1; // Source Field
                
                // Add lines for conditional rule type
                SerializedProperty ruleType = property.FindPropertyRelative("ruleType");
                if (ruleType.enumValueIndex == (int)RuleType.Conditional)
                {
                    lines += 4; // Comparison operator, value, true/false values
                }
                
                lines += 1; // Validation message space
            }
            
            return lines * (LINE_HEIGHT + SPACING);
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            Rect rect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);
            
            // === FOLDOUT HEADER ===
            SerializedProperty paramName = property.FindPropertyRelative("parameterName");
            string headerLabel = string.IsNullOrEmpty(paramName.stringValue) 
                ? "New Rule" 
                : paramName.stringValue;
            
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, headerLabel, true);
            rect.y += LINE_HEIGHT + SPACING;
            
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }
            
            EditorGUI.indentLevel++;
            
            // === ENABLED TOGGLE ===
            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            enabled.boolValue = EditorGUI.Toggle(rect, "Enabled", enabled.boolValue);
            rect.y += LINE_HEIGHT + SPACING;
            
            // === ANIMATOR PARAMETER ===
            DrawAnimatorParameterField(rect, property);
            rect.y += LINE_HEIGHT + SPACING;
            
            // === PARAMETER TYPE (read-only, auto-set) ===
            SerializedProperty paramType = property.FindPropertyRelative("parameterType");
            
            // Show the type with visual feedback
            AnimatorControllerParameterType typeValue = (AnimatorControllerParameterType)paramType.intValue;
            Color originalColor = GUI.backgroundColor;
            
            // Highlight in red if invalid (0)
            if (paramType.intValue == 0)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            }
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.PropertyField(rect, paramType, new GUIContent($"Parameter Type (auto-set)"));
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = originalColor;
            rect.y += LINE_HEIGHT + SPACING;
            
            // === RULE TYPE ===
            SerializedProperty ruleType = property.FindPropertyRelative("ruleType");
            EditorGUI.PropertyField(rect, ruleType);
            rect.y += LINE_HEIGHT + SPACING;
            
            // === SOURCE OBJECT ===
            SerializedProperty sourceObj = property.FindPropertyRelative("sourceObject");
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, sourceObj);
            if (EditorGUI.EndChangeCheck())
            {
                // Clear component and field when object changes
                property.FindPropertyRelative("sourceComponentType").stringValue = "";
                property.FindPropertyRelative("sourceFieldName").stringValue = "";
            }
            rect.y += LINE_HEIGHT + SPACING;
            
            // === SOURCE COMPONENT ===
            DrawComponentField(rect, property, sourceObj);
            rect.y += LINE_HEIGHT + SPACING;
            
            // === SOURCE FIELD ===
            DrawFieldSelection(rect, property, sourceObj);
            rect.y += LINE_HEIGHT + SPACING;
            
            // === CONDITIONAL RULE FIELDS ===
            if (ruleType.enumValueIndex == (int)RuleType.Conditional)
            {
                DrawConditionalFields(ref rect, property, paramType, sourceObj);
            }
            
            // === VALIDATION MESSAGE ===
            DrawValidationMessage(rect, property);
            
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
        
        private void DrawAnimatorParameterField(Rect rect, SerializedProperty property)
        {
            SerializedProperty paramName = property.FindPropertyRelative("parameterName");
            SerializedProperty paramType = property.FindPropertyRelative("parameterType");
            
            // Get animator from parent AnimatorTrigger
            AnimatorTrigger trigger = (property.serializedObject.targetObject as AnimatorTrigger);
            if (trigger == null || trigger.animator == null || trigger.animator.runtimeAnimatorController == null)
            {
                EditorGUI.PropertyField(rect, paramName, new GUIContent("Parameter Name"));
                return;
            }
            
            // Get all parameter names
            var parameters = trigger.animator.parameters;
            string[] paramNames = parameters.Select(p => p.name).ToArray();
            
            if (paramNames.Length == 0)
            {
                EditorGUI.LabelField(rect, "Parameter Name", "No parameters in Animator");
                return;
            }
            
            // Find current index
            int currentIndex = System.Array.IndexOf(paramNames, paramName.stringValue);
            if (currentIndex == -1) currentIndex = 0;
            
            // ALWAYS sync the parameter name and type with the current selection
            // This ensures they're set even if the user doesn't change the dropdown
            var currentParam = parameters[currentIndex];
            string expectedParamName = paramNames[currentIndex];
            int expectedTypeValue = (int)currentParam.type;
            
            // Auto-set parameter name if empty
            if (string.IsNullOrEmpty(paramName.stringValue))
            {
                paramName.stringValue = expectedParamName;
                paramType.intValue = expectedTypeValue;
                property.serializedObject.ApplyModifiedProperties();
            }
            // Auto-correct type if wrong
            else if (paramType.intValue != expectedTypeValue)
            {
                paramType.intValue = expectedTypeValue;
                property.serializedObject.ApplyModifiedProperties();
            }
            
            // Dropdown
            int newIndex = EditorGUI.Popup(rect, "Parameter Name", currentIndex, paramNames);
            
            // Handle selection change
            if (newIndex != currentIndex)
            {
                paramName.stringValue = paramNames[newIndex];
                
                // Auto-set parameter type for new selection
                var param = parameters[newIndex];
                paramType.intValue = (int)param.type;
            }
        }
        
        private void DrawComponentField(Rect rect, SerializedProperty property, SerializedProperty sourceObj)
        {
            SerializedProperty compType = property.FindPropertyRelative("sourceComponentType");
            
            GameObject obj = sourceObj.objectReferenceValue as GameObject;
            if (obj == null)
            {
                EditorGUI.LabelField(rect, "Source Component", "Assign Source Object first");
                return;
            }
            
            // Get all components
            Component[] components = obj.GetComponents<Component>();
            string[] componentNames = components.Select(c => c.GetType().Name).ToArray();
            
            if (componentNames.Length == 0)
            {
                EditorGUI.LabelField(rect, "Source Component", "No components found");
                return;
            }
            
            // Find current index by component type name (not full qualified name)
            string currentTypeName = "";
            if (!string.IsNullOrEmpty(compType.stringValue))
            {
                System.Type t = System.Type.GetType(compType.stringValue);
                if (t != null)
                    currentTypeName = t.Name;
            }
            
            int currentIndex = System.Array.IndexOf(componentNames, currentTypeName);
            if (currentIndex == -1) currentIndex = 0;
            
            // Dropdown
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(rect, "Source Component", currentIndex, componentNames);
            
            if (EditorGUI.EndChangeCheck() || string.IsNullOrEmpty(compType.stringValue))
            {
                compType.stringValue = components[newIndex].GetType().AssemblyQualifiedName;
                
                // Clear field name when component changes
                SerializedProperty fieldName = property.FindPropertyRelative("sourceFieldName");
                fieldName.stringValue = "";
                
                // Clear cache for this property
                string cacheKey = property.propertyPath + "_fields";
                if (fieldOptionsCache.ContainsKey(cacheKey))
                    fieldOptionsCache.Remove(cacheKey);
            }
        }
        
        private void DrawFieldSelection(Rect rect, SerializedProperty property, SerializedProperty sourceObj)
        {
            SerializedProperty fieldName = property.FindPropertyRelative("sourceFieldName");
            SerializedProperty compType = property.FindPropertyRelative("sourceComponentType");
            
            GameObject obj = sourceObj.objectReferenceValue as GameObject;
            if (obj == null || string.IsNullOrEmpty(compType.stringValue))
            {
                EditorGUI.LabelField(rect, "Source Field", "Select component first");
                return;
            }
            
            // Get component
            System.Type componentType = System.Type.GetType(compType.stringValue);
            if (componentType == null)
            {
                EditorGUI.LabelField(rect, "Source Field", "Invalid component type");
                return;
            }
            
            Component component = obj.GetComponent(componentType);
            if (component == null)
            {
                EditorGUI.LabelField(rect, "Source Field", "Component not found");
                return;
            }
            
            // Get or build field options with nested properties
            string cacheKey = property.propertyPath + "_fields";
            if (!fieldOptionsCache.ContainsKey(cacheKey))
            {
                fieldOptionsCache[cacheKey] = BuildFieldOptions(componentType);
            }
            
            string[] memberNames = fieldOptionsCache[cacheKey];
            
            if (memberNames.Length == 0)
            {
                EditorGUI.LabelField(rect, "Source Field", "No public fields/properties");
                return;
            }
            
            // Find current index
            int currentIndex = System.Array.IndexOf(memberNames, fieldName.stringValue);
            if (currentIndex == -1) currentIndex = 0;
            
            // Dropdown
            int newIndex = EditorGUI.Popup(rect, "Source Field", currentIndex, memberNames);
            
            if (newIndex != currentIndex || string.IsNullOrEmpty(fieldName.stringValue))
            {
                fieldName.stringValue = memberNames[newIndex];
            }
        }
        
        private string[] BuildFieldOptions(System.Type componentType)
        {
            List<string> options = new List<string>();
            
            // Get public fields
            var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                options.Add(field.Name);
                
                // Add nested properties for common types
                AddNestedProperties(options, field.Name, field.FieldType);
            }
            
            // Get public properties
            var props = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead);
            
            foreach (var prop in props)
            {
                options.Add(prop.Name);
                
                // Add nested properties
                AddNestedProperties(options, prop.Name, prop.PropertyType);
            }
            
            return options.ToArray();
        }
        
        private void AddNestedProperties(List<string> options, string baseName, System.Type type)
        {
            // Add .Count for lists and arrays
            if (typeof(System.Collections.ICollection).IsAssignableFrom(type))
            {
                options.Add($"{baseName}.Count");
            }
            
            // Add .Length for arrays
            if (type.IsArray)
            {
                options.Add($"{baseName}.Length");
            }
            
            // Add Vector components
            if (type == typeof(Vector2))
            {
                options.Add($"{baseName}.x");
                options.Add($"{baseName}.y");
                options.Add($"{baseName}.magnitude");
            }
            else if (type == typeof(Vector3))
            {
                options.Add($"{baseName}.x");
                options.Add($"{baseName}.y");
                options.Add($"{baseName}.z");
                options.Add($"{baseName}.magnitude");
            }
            else if (type == typeof(Vector4))
            {
                options.Add($"{baseName}.x");
                options.Add($"{baseName}.y");
                options.Add($"{baseName}.z");
                options.Add($"{baseName}.w");
                options.Add($"{baseName}.magnitude");
            }
            
            // Add Color components
            if (type == typeof(Color) || type == typeof(Color32))
            {
                options.Add($"{baseName}.r");
                options.Add($"{baseName}.g");
                options.Add($"{baseName}.b");
                options.Add($"{baseName}.a");
            }
            
            // Add Quaternion/Euler
            if (type == typeof(Quaternion))
            {
                options.Add($"{baseName}.eulerAngles.x");
                options.Add($"{baseName}.eulerAngles.y");
                options.Add($"{baseName}.eulerAngles.z");
            }
        }
        
        private void DrawConditionalFields(ref Rect rect, SerializedProperty property, SerializedProperty paramType, SerializedProperty sourceObj)
        {
            SerializedProperty compOp = property.FindPropertyRelative("comparisonOperator");
            SerializedProperty compValue = property.FindPropertyRelative("comparisonValue");
            SerializedProperty trueValue = property.FindPropertyRelative("trueValue");
            SerializedProperty falseValue = property.FindPropertyRelative("falseValue");
            
            // Get source field type
            System.Type sourceType = GetSourceFieldType(property, sourceObj);
            string sourceTypeStr = sourceType != null ? GetFriendlyTypeName(sourceType) : "value";
            
            // Comparison Operator
            EditorGUI.PropertyField(rect, compOp, new GUIContent("If Source"));
            rect.y += LINE_HEIGHT + SPACING;
            
            // Comparison Value (with type hint)
            string compLabel = $"Is ({sourceTypeStr})";
            EditorGUI.PropertyField(rect, compValue, new GUIContent(compLabel));
            rect.y += LINE_HEIGHT + SPACING;
            
            // Get parameter type string
            AnimatorControllerParameterType pType = (AnimatorControllerParameterType)paramType.intValue;
            string paramTypeStr = GetFriendlyTypeName(pType);
            
            // True Value
            string trueLabel = $"Then Set To ({paramTypeStr})";
            if (pType == AnimatorControllerParameterType.Bool || pType == AnimatorControllerParameterType.Trigger)
                trueLabel += " - use 'true' or 'false'";
            
            EditorGUI.PropertyField(rect, trueValue, new GUIContent(trueLabel));
            rect.y += LINE_HEIGHT + SPACING;
            
            // False Value
            string falseLabel = $"Else Set To ({paramTypeStr})";
            if (pType == AnimatorControllerParameterType.Bool || pType == AnimatorControllerParameterType.Trigger)
                falseLabel += " - use 'true' or 'false'";
            
            EditorGUI.PropertyField(rect, falseValue, new GUIContent(falseLabel));
            rect.y += LINE_HEIGHT + SPACING;
        }
        
        private System.Type GetSourceFieldType(SerializedProperty property, SerializedProperty sourceObj)
        {
            GameObject obj = sourceObj.objectReferenceValue as GameObject;
            if (obj == null)
                return null;
            
            SerializedProperty compType = property.FindPropertyRelative("sourceComponentType");
            SerializedProperty fieldName = property.FindPropertyRelative("sourceFieldName");
            
            if (string.IsNullOrEmpty(compType.stringValue) || string.IsNullOrEmpty(fieldName.stringValue))
                return null;
            
            System.Type componentType = System.Type.GetType(compType.stringValue);
            if (componentType == null)
                return null;
            
            Component component = obj.GetComponent(componentType);
            if (component == null)
                return null;
            
            // Navigate nested properties
            string[] fieldPath = fieldName.stringValue.Split('.');
            System.Type currentType = componentType;
            
            foreach (string part in fieldPath)
            {
                var field = currentType.GetField(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    currentType = field.FieldType;
                    continue;
                }
                
                var prop = currentType.GetProperty(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    currentType = prop.PropertyType;
                    continue;
                }
                
                return null;
            }
            
            return currentType;
        }
        
        private string GetFriendlyTypeName(System.Type type)
        {
            if (type == typeof(float)) return "float";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            return type.Name.ToLower();
        }
        
        private string GetFriendlyTypeName(AnimatorControllerParameterType type)
        {
            switch (type)
            {
                case AnimatorControllerParameterType.Float: return "float";
                case AnimatorControllerParameterType.Int: return "int";
                case AnimatorControllerParameterType.Bool: return "bool";
                case AnimatorControllerParameterType.Trigger: return "bool";
                default: return type.ToString();
            }
        }
        
        private void DrawValidationMessage(Rect rect, SerializedProperty property)
        {
            AnimatorTrigger trigger = property.serializedObject.targetObject as AnimatorTrigger;
            if (trigger == null) return;
            
            // Find which rule this is
            int ruleIndex = GetRuleIndex(property);
            if (ruleIndex < 0 || ruleIndex >= trigger.rules.Count) return;
            
            AnimatorParameterRule rule = trigger.rules[ruleIndex];
            string error = rule.Validate();
            
            if (!string.IsNullOrEmpty(error))
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.helpBox);
                errorStyle.normal.textColor = Color.red;
                EditorGUI.LabelField(rect, "⚠ " + error, errorStyle);
            }
        }
        
        private int GetRuleIndex(SerializedProperty property)
        {
            // Extract index from property path (e.g., "rules.Array.data[0]")
            string path = property.propertyPath;
            int startIndex = path.IndexOf('[') + 1;
            int endIndex = path.IndexOf(']');
            
            if (startIndex > 0 && endIndex > startIndex)
            {
                string indexStr = path.Substring(startIndex, endIndex - startIndex);
                if (int.TryParse(indexStr, out int index))
                    return index;
            }
            
            return -1;
        }
    }
}
#endif