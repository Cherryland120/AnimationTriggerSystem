using UnityEngine;
using System.Collections.Generic;

namespace AnimatorTriggerSystem
{
    /// <summary>
    /// Controls Animator parameters based on values from other scripts
    /// Attach this to a GameObject with an Animator component
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimatorTrigger : MonoBehaviour
    {
        [Tooltip("When to evaluate and update animator parameters")]
        public UpdateMode updateMode = UpdateMode.Update;
        
        [Tooltip("Reference to the Animator (auto-assigned if null)")]
        public Animator animator;
        
        [Tooltip("List of rules that define how to control animator parameters")]
        public List<AnimatorParameterRule> rules = new List<AnimatorParameterRule>();
        
        [Header("Debug")]
        [Tooltip("Log rule evaluations to console")]
        public bool debugMode = false;
        
        // === UNITY LIFECYCLE ===
        
        private void Awake()
        {
            // Auto-assign animator if not set
            if (animator == null)
                animator = GetComponent<Animator>();
            
            if (animator == null)
            {
                Debug.LogError("[AnimatorTrigger] No Animator found on " + gameObject.name);
                enabled = false;
                return;
            }
            
            ValidateRules();
        }
        
        private void Update()
        {
            if (updateMode == UpdateMode.Update)
                EvaluateRules();
        }
        
        private void FixedUpdate()
        {
            if (updateMode == UpdateMode.FixedUpdate)
                EvaluateRules();
        }
        
        private void LateUpdate()
        {
            if (updateMode == UpdateMode.LateUpdate)
                EvaluateRules();
        }
        
        // === PUBLIC METHODS ===
        
        /// <summary>
        /// Manually evaluate all rules (useful for event-based triggering)
        /// </summary>
        public void EvaluateRules()
        {
            if (animator == null || rules == null)
                return;
            
            foreach (var rule in rules)
            {
                if (!rule.enabled)
                    continue;
                
                try
                {
                    // Pass debug mode to rule
                    rule.debugMode = debugMode;
                    
                    object value = rule.Evaluate();
                    
                    if (value != null)
                    {
                        SetAnimatorParameter(rule.parameterName, rule.parameterType, value);
                        
                        if (debugMode)
                            Debug.Log($"[AnimatorTrigger] Set {rule.parameterName} = {value}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AnimatorTrigger] Error evaluating rule '{rule.parameterName}': {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Evaluate a specific rule by parameter name
        /// </summary>
        public void EvaluateRule(string parameterName)
        {
            if (animator == null || rules == null)
                return;
            
            var rule = rules.Find(r => r.parameterName == parameterName);
            if (rule != null && rule.enabled)
            {
                rule.debugMode = debugMode;
                object value = rule.Evaluate();
                
                if (value != null)
                {
                    SetAnimatorParameter(rule.parameterName, rule.parameterType, value);
                }
            }
        }
        
        /// <summary>
        /// Add a new rule to the list
        /// </summary>
        public void AddRule()
        {
            rules.Add(new AnimatorParameterRule());
        }
        
        /// <summary>
        /// Remove a rule at the specified index
        /// </summary>
        public void RemoveRule(int index)
        {
            if (index >= 0 && index < rules.Count)
                rules.RemoveAt(index);
        }
        
        /// <summary>
        /// Clear cached reflection data for all rules (call when sources change)
        /// </summary>
        public void ClearAllCaches()
        {
            foreach (var rule in rules)
            {
                rule.ClearCache();
            }
        }
        
        /// <summary>
        /// Get all animator parameter names
        /// </summary>
        public string[] GetAnimatorParameterNames()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return new string[0];
            
            var parameters = animator.parameters;
            string[] names = new string[parameters.Length];
            
            for (int i = 0; i < parameters.Length; i++)
            {
                names[i] = parameters[i].name;
            }
            
            return names;
        }
        
        /// <summary>
        /// Get animator parameter type by name
        /// </summary>
        public AnimatorControllerParameterType? GetParameterType(string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return null;
            
            foreach (var param in animator.parameters)
            {
                if (param.name == parameterName)
                    return param.type;
            }
            
            return null;
        }
        
        // === PRIVATE METHODS ===
        
        private void SetAnimatorParameter(string parameterName, AnimatorControllerParameterType type, object value)
        {
            if (animator == null)
                return;
            
            try
            {
                switch (type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(parameterName, (float)value);
                        break;
                    
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(parameterName, (int)value);
                        break;
                    
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(parameterName, (bool)value);
                        break;
                    
                    case AnimatorControllerParameterType.Trigger:
                        if ((bool)value)
                            animator.SetTrigger(parameterName);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AnimatorTrigger] Failed to set parameter '{parameterName}': {e.Message}");
            }
        }
        
        private void ValidateRules()
        {
            if (rules == null)
                return;
            
            foreach (var rule in rules)
            {
                string error = rule.Validate();
                if (error != null)
                {
                    Debug.LogWarning($"[AnimatorTrigger] Rule '{rule.parameterName}' is invalid: {error}");
                }
            }
        }
        
        // === EDITOR HELPERS ===
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign animator in editor
            if (animator == null)
                animator = GetComponent<Animator>();
        }
#endif
    }
    
    // === ENUMS ===
    
    public enum UpdateMode
    {
        Update,
        FixedUpdate,
        LateUpdate
    }
}