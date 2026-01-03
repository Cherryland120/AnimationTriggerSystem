using UnityEngine;

namespace AnimatorTriggerSystem
{
    /// <summary>
    /// Defines how an Animator parameter should be controlled based on external script values
    /// </summary>
    [System.Serializable]
    public class AnimatorParameterRule
    {
        // === ANIMATOR PARAMETER INFO ===
        [Tooltip("Name of the Animator parameter to control")]
        public string parameterName;
        
        [Tooltip("Type of the Animator parameter")]
        public AnimatorControllerParameterType parameterType;
        
        // === RULE CONFIGURATION ===
        [Tooltip("How this rule evaluates the source value")]
        public RuleType ruleType = RuleType.DirectBinding;
        
        [Tooltip("Enable/disable this rule without deleting it")]
        public bool enabled = true;
        
        // === SOURCE REFERENCE ===
        [Tooltip("GameObject containing the source script")]
        public GameObject sourceObject;
        
        [Tooltip("Name of the component type (stored as string for serialization)")]
        public string sourceComponentType;
        
        [Tooltip("Name of the public field/property to read from (supports nested like 'myList.Count' or 'transform.position.x')")]
        public string sourceFieldName;
        
        // Store the actual type of the source field for UI purposes
        [System.NonSerialized]
        public System.Type sourceFieldType;
        
        // === CONDITIONAL LOGIC (for RuleType.Conditional) ===
        [Tooltip("Comparison operator for conditional rules")]
        public ComparisonOperator comparisonOperator = ComparisonOperator.Equals;
        
        [Tooltip("Value to compare against")]
        public string comparisonValue;
        
        [Tooltip("Value to set if condition is TRUE")]
        public string trueValue;
        
        [Tooltip("Value to set if condition is FALSE")]
        public string falseValue;
        
        // === RUNTIME CACHED DATA (not serialized) ===
        [System.NonSerialized]
        private Component cachedComponent;
        
        [System.NonSerialized]
        private System.Reflection.FieldInfo cachedField;
        
        [System.NonSerialized]
        private System.Reflection.PropertyInfo cachedProperty;
        
        [System.NonSerialized]
        private bool reflectionCached = false;
        
        // Reference to debug mode (set by AnimatorTrigger)
        [System.NonSerialized]
        public bool debugMode = false;
        
        // === PUBLIC METHODS ===
        
        /// <summary>
        /// Evaluates this rule and returns the value to set on the Animator parameter
        /// </summary>
        public object Evaluate()
        {
            if (!enabled || sourceObject == null || string.IsNullOrEmpty(sourceFieldName))
                return null;
            
            // Cache reflection data on first use
            if (!reflectionCached)
                CacheReflectionData();
            
            if (cachedComponent == null)
                return null;
            
            // Get the source value (supports nested properties)
            object sourceValue = GetSourceValue();
            if (sourceValue == null)
                return null;
            
            if (debugMode)
                Debug.Log($"[AnimatorTrigger] Rule '{parameterName}': Source value = {sourceValue} (type: {sourceValue.GetType().Name})");
            
            // Process based on rule type
            switch (ruleType)
            {
                case RuleType.DirectBinding:
                    return ConvertValue(sourceValue, parameterType);
                
                case RuleType.Conditional:
                    return EvaluateConditional(sourceValue);
                
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Validates this rule and returns error message if invalid
        /// </summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(parameterName))
                return "Parameter name is empty";
            
            if (sourceObject == null)
                return "Source object is not assigned";
            
            if (string.IsNullOrEmpty(sourceComponentType))
                return "Source component is not selected";
            
            if (string.IsNullOrEmpty(sourceFieldName))
                return "Source field is not selected";
            
            return null; // Valid
        }
        
        // === PRIVATE METHODS ===
        
        private void CacheReflectionData()
        {
            reflectionCached = true;
            
            if (sourceObject == null || string.IsNullOrEmpty(sourceComponentType))
                return;
            
            // Get the component type
            System.Type componentType = System.Type.GetType(sourceComponentType);
            if (componentType == null)
            {
                // Try alternative: search all assemblies
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(sourceComponentType);
                    if (componentType != null)
                        break;
                }
                
                if (componentType == null)
                {
                    Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Could not find type '{sourceComponentType}'");
                    return;
                }
            }
            
            cachedComponent = sourceObject.GetComponent(componentType);
            
            if (cachedComponent == null)
            {
                Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Component '{componentType.Name}' not found on '{sourceObject.name}'");
                return;
            }
            
            // Parse nested field path (e.g., "myList.Count" or "transform.position.x")
            string[] fieldPath = sourceFieldName.Split('.');
            System.Type currentType = cachedComponent.GetType();
            
            // Cache the first level field/property
            cachedField = currentType.GetField(fieldPath[0], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (cachedField == null)
            {
                cachedProperty = currentType.GetProperty(fieldPath[0], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (cachedProperty == null)
                {
                    Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Field/property '{fieldPath[0]}' not found on '{currentType.Name}'. Make sure it's PUBLIC!");
                    return;
                }
            }
            
            // Determine the final type for UI purposes
            sourceFieldType = GetSourceFieldType();
        }
        
        private object GetSourceValue()
        {
            // Get the first level value
            object currentValue = null;
            
            if (cachedField != null)
                currentValue = cachedField.GetValue(cachedComponent);
            else if (cachedProperty != null && cachedProperty.CanRead)
                currentValue = cachedProperty.GetValue(cachedComponent);
            else
                return null;
            
            if (currentValue == null)
                return null;
            
            // Handle nested properties (e.g., "myList.Count" or "position.x")
            string[] fieldPath = sourceFieldName.Split('.');
            
            for (int i = 1; i < fieldPath.Length; i++)
            {
                if (currentValue == null)
                    return null;
                
                System.Type currentType = currentValue.GetType();
                
                // Try field first
                var field = currentType.GetField(fieldPath[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    currentValue = field.GetValue(currentValue);
                    continue;
                }
                
                // Try property
                var prop = currentType.GetProperty(fieldPath[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    currentValue = prop.GetValue(currentValue);
                    continue;
                }
                
                // Property not found
                if (debugMode)
                    Debug.LogWarning($"[AnimatorTrigger] Rule '{parameterName}': Could not find property '{fieldPath[i]}' in type {currentType}");
                return null;
            }
            
            return currentValue;
        }
        
        private System.Type GetSourceFieldType()
        {
            string[] fieldPath = sourceFieldName.Split('.');
            System.Type currentType = cachedComponent.GetType();
            
            // Navigate through the field path to find the final type
            for (int i = 0; i < fieldPath.Length; i++)
            {
                var field = currentType.GetField(fieldPath[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    currentType = field.FieldType;
                    continue;
                }
                
                var prop = currentType.GetProperty(fieldPath[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    currentType = prop.PropertyType;
                    continue;
                }
                
                return typeof(object); // Unknown type
            }
            
            return currentType;
        }
        
        private object ConvertValue(object value, AnimatorControllerParameterType targetType)
        {
            try
            {
                switch (targetType)
                {
                    case AnimatorControllerParameterType.Float:
                        return System.Convert.ToSingle(value);
                    
                    case AnimatorControllerParameterType.Int:
                        return System.Convert.ToInt32(value);
                    
                    case AnimatorControllerParameterType.Bool:
                        return System.Convert.ToBoolean(value);
                    
                    case AnimatorControllerParameterType.Trigger:
                        // Triggers are activated if source is true/non-zero
                        if (value is bool)
                            return (bool)value;
                        if (value is float)
                            return (float)value != 0f;
                        if (value is int)
                            return (int)value != 0;
                        return System.Convert.ToBoolean(value);
                    
                    default:
                        return value;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Failed to convert {value} to {targetType}: {e.Message}");
                return null;
            }
        }
        
        private object EvaluateConditional(object sourceValue)
        {
            bool conditionMet = EvaluateComparison(sourceValue);
            string resultString = conditionMet ? trueValue : falseValue;
            
            if (debugMode)
                Debug.Log($"[AnimatorTrigger] Rule '{parameterName}': Condition {(conditionMet ? "MET" : "NOT MET")}, result = '{resultString}'");
            
            // Parse result based on parameter type
            return ParseStringToType(resultString, parameterType);
        }
        
        private bool EvaluateComparison(object sourceValue)
        {
            if (string.IsNullOrEmpty(comparisonValue))
                return false;
            
            try
            {
                // Convert both to comparable types
                System.IComparable sourceComparable = sourceValue as System.IComparable;
                if (sourceComparable == null)
                    return false;
                
                // Parse comparison value to same type as source
                object parsedComparison = System.Convert.ChangeType(comparisonValue, sourceValue.GetType());
                
                int comparison = sourceComparable.CompareTo(parsedComparison);
                
                bool result = false;
                switch (comparisonOperator)
                {
                    case ComparisonOperator.Equals:
                        result = comparison == 0;
                        break;
                    case ComparisonOperator.NotEquals:
                        result = comparison != 0;
                        break;
                    case ComparisonOperator.GreaterThan:
                        result = comparison > 0;
                        break;
                    case ComparisonOperator.LessThan:
                        result = comparison < 0;
                        break;
                    case ComparisonOperator.GreaterOrEqual:
                        result = comparison >= 0;
                        break;
                    case ComparisonOperator.LessOrEqual:
                        result = comparison <= 0;
                        break;
                }
                
                if (debugMode)
                    Debug.Log($"[AnimatorTrigger] Rule '{parameterName}': {sourceValue} {comparisonOperator} {parsedComparison} = {result}");
                
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Failed to compare {sourceValue} with {comparisonValue}: {e.Message}");
                return false;
            }
        }
        
        private object ParseStringToType(string value, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            
            try
            {
                switch (type)
                {
                    case AnimatorControllerParameterType.Float:
                        return float.Parse(value);
                    
                    case AnimatorControllerParameterType.Int:
                        return int.Parse(value);
                    
                    case AnimatorControllerParameterType.Bool:
                        // Parse bool with case-insensitive comparison
                        string lower = value.ToLower().Trim();
                        if (lower == "true" || lower == "1")
                            return true;
                        if (lower == "false" || lower == "0")
                            return false;
                        return bool.Parse(value);
                    
                    case AnimatorControllerParameterType.Trigger:
                        // Parse bool with case-insensitive comparison
                        string lowerTrigger = value.ToLower().Trim();
                        if (lowerTrigger == "true" || lowerTrigger == "1")
                            return true;
                        if (lowerTrigger == "false" || lowerTrigger == "0")
                            return false;
                        return bool.Parse(value);
                    
                    default:
                        Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Unexpected parameter type {type}");
                        return null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AnimatorTrigger] Rule '{parameterName}': Failed to parse '{value}' as {type}: {e.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Clears cached reflection data (call when source changes)
        /// </summary>
        public void ClearCache()
        {
            reflectionCached = false;
            cachedComponent = null;
            cachedField = null;
            cachedProperty = null;
            sourceFieldType = null;
        }
    }
    
    // === ENUMS ===
    
    public enum RuleType
    {
        DirectBinding,  // Direct copy if types match
        Conditional     // If-else logic
        // Future: Expression, MultiSource
    }
    
    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual
    }
}