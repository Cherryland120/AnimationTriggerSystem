#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace AnimatorTriggerSystem
{
    [CustomEditor(typeof(AnimatorTrigger))]
    public class AnimatorTriggerEditor : Editor
    {
        private SerializedProperty updateModeProp;
        private SerializedProperty animatorProp;
        private SerializedProperty rulesProp;
        private SerializedProperty debugModeProp;
        
        private void OnEnable()
        {
            updateModeProp = serializedObject.FindProperty("updateMode");
            animatorProp = serializedObject.FindProperty("animator");
            rulesProp = serializedObject.FindProperty("rules");
            debugModeProp = serializedObject.FindProperty("debugMode");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            AnimatorTrigger trigger = (AnimatorTrigger)target;
            
            // === HEADER ===
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animator Trigger System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Control Animator parameters based on values from other scripts. " +
                "Add rules below to define how parameters should be updated.",
                MessageType.Info
            );
            EditorGUILayout.Space();
            
            // === BASIC SETTINGS ===
            EditorGUILayout.PropertyField(updateModeProp);
            EditorGUILayout.PropertyField(animatorProp);
            
            // Warning if no animator
            if (trigger.animator == null)
            {
                EditorGUILayout.HelpBox("No Animator assigned! Component requires an Animator.", MessageType.Error);
            }
            else if (trigger.animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox("Animator has no Controller assigned!", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            // === RULES SECTION ===
            EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
            
            // Display rules list
            EditorGUILayout.PropertyField(rulesProp, true);
            
            // Add/Remove buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Add New Rule", GUILayout.Height(25)))
            {
                Undo.RecordObject(trigger, "Add Rule");
                trigger.AddRule();
                EditorUtility.SetDirty(trigger);
            }
            
            GUI.enabled = trigger.rules.Count > 0;
            if (GUILayout.Button("Remove Last Rule", GUILayout.Height(25)))
            {
                Undo.RecordObject(trigger, "Remove Rule");
                trigger.RemoveRule(trigger.rules.Count - 1);
                EditorUtility.SetDirty(trigger);
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Clear cache button
            if (trigger.rules.Count > 0)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Clear All Reflection Caches", GUILayout.Height(20)))
                {
                    trigger.ClearAllCaches();
                    Debug.Log("[AnimatorTrigger] Cleared all cached reflection data");
                }
                EditorGUILayout.HelpBox(
                    "Click 'Clear All Reflection Caches' if you've modified source scripts or components.",
                    MessageType.None
                );
            }
            
            EditorGUILayout.Space();
            
            // === DEBUG SECTION ===
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(debugModeProp);
            
            // Runtime test buttons
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Evaluate All Rules Now", GUILayout.Height(30)))
                {
                    trigger.EvaluateRules();
                    Debug.Log("[AnimatorTrigger] Manually evaluated all rules");
                }
                
                // Individual rule evaluation
                if (trigger.rules.Count > 0)
                {
                    EditorGUILayout.LabelField("Evaluate Individual Rules:", EditorStyles.miniBoldLabel);
                    
                    foreach (var rule in trigger.rules)
                    {
                        if (string.IsNullOrEmpty(rule.parameterName))
                            continue;
                        
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(rule.parameterName, GUILayout.Width(150));
                        
                        if (GUILayout.Button("Evaluate", GUILayout.Width(80)))
                        {
                            trigger.EvaluateRule(rule.parameterName);
                            Debug.Log($"[AnimatorTrigger] Evaluated rule for {rule.parameterName}");
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test rule evaluation in real-time.", MessageType.Info);
            }
            
            // === INFO SECTION ===
            if (trigger.animator != null && trigger.animator.runtimeAnimatorController != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Animator Parameters:", EditorStyles.boldLabel);
                
                var parameters = trigger.animator.parameters;
                if (parameters.Length == 0)
                {
                    EditorGUILayout.HelpBox("No parameters in Animator Controller", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    foreach (var param in parameters)
                    {
                        EditorGUILayout.LabelField($"• {param.name} ({param.type})");
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif