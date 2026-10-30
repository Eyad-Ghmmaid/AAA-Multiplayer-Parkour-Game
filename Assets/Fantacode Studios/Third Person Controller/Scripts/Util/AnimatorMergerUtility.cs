//#if UNITY_EDITOR
//using System.Linq;
//using UnityEditor;
//using UnityEditor.Animations;
//using UnityEngine;

//namespace FS_ThirdPerson
//{

//    public class AnimatorMergerUtility
//    {
//        public AnimatorController sourceAnimatorController;
//        public AnimatorController targetAnimatorController;

//        public AnimatorMergerUtility(AnimatorController sourceController, AnimatorController targetController)
//        {
//            sourceAnimatorController = sourceController;
//            targetAnimatorController = targetController;
//        }
//        public void MergeAnimatorControllers(AnimatorController sourceController, AnimatorController targetController = null)
//        {
//            if (sourceAnimatorController == null || targetAnimatorController == null)
//            {
//                Debug.LogError("Both source and target Animator Controllers must be assigned.");
//                return;
//            }
//            if (sourceController == null)
//                sourceController = sourceAnimatorController;

//            if (targetController == null)
//                targetController = targetAnimatorController;

//            CopyParameters(sourceController, targetController);

//            // Find the "Locomotion" blend tree in the source controller
//            BlendTree sourceLocomotion = FindBlendTree(sourceController, "Locomotion");

//            if (sourceLocomotion == null)
//            {
//                Debug.LogError("Locomotion blend tree not found in the source controller.");
//                return;
//            }

//            // Create a new layer in the target controller for the merged content
//            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
//            {
//                name = "Merged Locomotion",
//                stateMachine = new AnimatorStateMachine()
//            };

//            //AssetDatabase.AddObjectToAsset(newLayer.stateMachine, targetController);
//            //targetController.AddLayer(newLayer);
//            // Recursively copy the state machine structure
//            CopyStateMachineStructure(sourceController.layers[0].stateMachine, targetController.layers[0].stateMachine, sourceLocomotion);

//            // Save changes
//            EditorUtility.SetDirty(targetController);
//            AssetDatabase.SaveAssets();

//        }

//        void CopyStateMachineStructure(AnimatorStateMachine source, AnimatorStateMachine target, Motion targetMotion)
//        {
//            foreach (var sourceState in source.states)
//            {
//                //skip if the state already exists
//                if (target.states.Any(x => x.state.name == sourceState.state.name)) continue;
//                AnimatorState newState = target.AddState(sourceState.state.name, sourceState.position);
//                CopyStateProperties(sourceState.state, newState);
//                if (sourceState.state.motion == targetMotion)
//                {
//                    // We found our target motion, replace it with the copied blend tree
//                    newState.motion = CopyBlendTree(targetMotion as BlendTree);
//                }
//            }

//            foreach (var sourceChildStateMachine in source.stateMachines)
//            {
//                AnimatorStateMachine existingChildStateMachine = target.stateMachines.FirstOrDefault(x => x.stateMachine.name == sourceChildStateMachine.stateMachine.name).stateMachine;
//                AnimatorStateMachine newChildStateMachine;

//                if (existingChildStateMachine == null)
//                {
//                    newChildStateMachine = target.AddStateMachine(
//                        sourceChildStateMachine.stateMachine.name,
//                        sourceChildStateMachine.position
//                    );
//                }
//                else
//                {
//                    newChildStateMachine = existingChildStateMachine;
//                }

//                CopyStateMachineStructure(sourceChildStateMachine.stateMachine, newChildStateMachine, targetMotion);
//            }

//            // Copy any state transitions
//            foreach (var sourceTransition in source.anyStateTransitions)
//            {
//                AnimatorState destinationState = GetStateInStateMachine(target, sourceTransition.destinationState.name);
//                if (destinationState != null && !target.anyStateTransitions.Any(t => t.destinationState == destinationState))
//                {
//                    AnimatorStateTransition newTransition = target.AddAnyStateTransition(destinationState);
//                    CopyTransitionProperties(sourceTransition, newTransition);
//                }
//            }

//            // Copy transitions for each state
//            foreach (var sourceState in source.states)
//            {
//                AnimatorState targetState = GetStateInStateMachine(target, sourceState.state.name);
//                if (targetState != null)
//                {
//                    foreach (var sourceTransition in sourceState.state.transitions)
//                    {
//                        AnimatorState destinationState = GetStateInStateMachine(target, sourceTransition.destinationState.name);
//                        if (destinationState == null)
//                            destinationState = GetStateInStateMachineRecursive(targetAnimatorController.layers[0].stateMachine, sourceTransition.destinationState.name);
//                        if (destinationState != null && !targetState.transitions.Any(t => t.destinationState == destinationState))
//                        {
//                            AnimatorStateTransition newTransition = targetState.AddTransition(destinationState);
//                            CopyTransitionProperties(sourceTransition, newTransition);
//                        }
//                    }
//                }
//            }

//            //// Copy entry transitions
//            //foreach (var entryTransition in source.entryTransitions)
//            //{
//            //    AnimatorState destinationState = GetStateInStateMachine(target, entryTransition.destinationState.name);
//            //    if (destinationState != null && !target.entryTransitions.Any(t => t.destinationState == destinationState))
//            //    {
//            //        AnimatorTransition newEntryTransition = target.AddEntryTransition(destinationState);
//            //        CopyEntryTransitionProperties(entryTransition, newEntryTransition);
//            //    }
//            //}

//            // Copy the default state if it exists
//            if (source.defaultState != null)
//            {
//                AnimatorState targetDefaultState = GetStateInStateMachine(target, source.defaultState.name);
//                if (targetDefaultState != null)
//                {
//                    target.defaultState = targetDefaultState;
//                }
//            }
//        }
//        void CopyStateMachineStructure1(AnimatorStateMachine source, AnimatorStateMachine target, Motion targetMotion)
//        {
//            foreach (var sourceState in source.states)
//            {
//                //skip if the state already exists
//                if (target.states.Any(x => x.state.name == sourceState.state.name)) continue;
//                AnimatorState newState = target.AddState(sourceState.state.name, sourceState.position);
//                CopyStateProperties(sourceState.state, newState);

//                if (sourceState.state.motion == targetMotion)
//                {
//                    // We found our target motion, replace it with the copied blend tree
//                    newState.motion = CopyBlendTree(targetMotion as BlendTree);
//                }
//            }

//            foreach (var sourceChildStateMachine in source.stateMachines)
//            {
//                AnimatorStateMachine newChildStateMachine = target.AddStateMachine(
//                    sourceChildStateMachine.stateMachine.name,
//                    sourceChildStateMachine.position
//                );
//                CopyStateMachineStructure(sourceChildStateMachine.stateMachine, newChildStateMachine, targetMotion);
//            }

//            // Copy transitions
//            foreach (var sourceTransition in source.anyStateTransitions)
//            {
//                AnimatorStateTransition newTransition = target.AddAnyStateTransition(GetStateInStateMachine(target, sourceTransition.destinationState.name));
//                CopyTransitionProperties(sourceTransition, newTransition);
//            }

//            foreach (var sourceState in source.states)
//            {
//                AnimatorState targetState = GetStateInStateMachine(target, sourceState.state.name);
//                foreach (var sourceTransition in sourceState.state.transitions)
//                {
//                    AnimatorStateTransition newTransition = targetState.AddTransition(GetStateInStateMachine(target, sourceTransition.destinationState.name));
//                    CopyTransitionProperties(sourceTransition, newTransition);
//                }
//            }
//        }

//        void CopyStateProperties(AnimatorState source, AnimatorState target)
//        {
//            target.speed = source.speed;
//            target.cycleOffset = source.cycleOffset;
//            target.mirror = source.mirror;
//            target.mirrorParameterActive = source.mirrorParameterActive;
//            target.mirrorParameter = source.mirrorParameter;
//            target.iKOnFeet = source.iKOnFeet;
//            target.writeDefaultValues = source.writeDefaultValues;
//            target.tag = source.tag;

//            if (source.motion is BlendTree)
//            {
//                target.motion = CopyBlendTree(source.motion as BlendTree);
//            }
//            else
//            {
//                target.motion = source.motion;
//            }

//            // Copy behaviors
//            foreach (var behavior in source.behaviours)
//            {
//                var newBehavior = target.AddStateMachineBehaviour(behavior.GetType());
//                EditorUtility.CopySerialized(behavior, newBehavior);
//            }
//        }

//        void CopyTransitionProperties(AnimatorStateTransition source, AnimatorStateTransition target)
//        {
//            target.duration = source.duration;
//            target.offset = source.offset;
//            target.interruptionSource = source.interruptionSource;
//            target.orderedInterruption = source.orderedInterruption;
//            target.exitTime = source.exitTime;
//            target.hasExitTime = source.hasExitTime;
//            target.hasFixedDuration = source.hasFixedDuration;
//            target.canTransitionToSelf = source.canTransitionToSelf;

//            // Copy conditions
//            foreach (var condition in source.conditions)
//            {
//                target.AddCondition(condition.mode, condition.threshold, condition.parameter);
//            }
//        }

//        BlendTree CopyBlendTree(BlendTree source)
//        {
//            BlendTree newTree = new BlendTree
//            {
//                name = source.name,
//                blendType = source.blendType,
//                blendParameter = source.blendParameter,
//                blendParameterY = source.blendParameterY,
//                minThreshold = source.minThreshold,
//                maxThreshold = source.maxThreshold,
//                useAutomaticThresholds = source.useAutomaticThresholds
//            };

//            for (int i = 0; i < source.children.Length; i++)
//            {
//                var child = source.children[i];
//                Motion childMotion;

//                if (child.motion is BlendTree childBlendTree)
//                {
//                    childMotion = CopyBlendTree(childBlendTree);
//                }
//                else
//                {
//                    childMotion = child.motion;
//                }

//                // Copy position (only applicable for 2D blend types)
//                if (newTree.blendType == BlendTreeType.SimpleDirectional2D || newTree.blendType == BlendTreeType.FreeformDirectional2D || newTree.blendType == BlendTreeType.FreeformCartesian2D)
//                {
//                    newTree.AddChild(childMotion, child.position);

//                }
//                else
//                    newTree.AddChild(childMotion, child.threshold);

//                SerializedObject serializedTree = new SerializedObject(newTree);
//                //PrintAllProperties(serializedTree);
//                SerializedProperty childrenProp = serializedTree.FindProperty("m_Childs");
//                if (childrenProp != null && i < childrenProp.arraySize)
//                {
//                    SerializedProperty childProp = childrenProp.GetArrayElementAtIndex(i);
//                    childProp.FindPropertyRelative("m_TimeScale").floatValue = child.timeScale;
//                    childProp.FindPropertyRelative("m_CycleOffset").floatValue = child.cycleOffset;
//                    childProp.FindPropertyRelative("m_Mirror").boolValue = child.mirror;
//                }
//                serializedTree.ApplyModifiedProperties();
//            }

//            AssetDatabase.AddObjectToAsset(newTree, targetAnimatorController);
//            newTree.hideFlags = HideFlags.HideInHierarchy;

//            return newTree;
//        }

//        BlendTree FindBlendTree(AnimatorController controller, string name)
//        {
//            foreach (var layer in controller.layers)
//            {
//                var blendTree = FindBlendTreeRecursive(layer.stateMachine, name);
//                if (blendTree != null)
//                {
//                    return blendTree;
//                }
//            }
//            return null;
//        }

//        BlendTree FindBlendTreeRecursive(AnimatorStateMachine stateMachine, string name)
//        {
//            foreach (var state in stateMachine.states)
//            {
//                if (state.state.motion is BlendTree blendTree)
//                {
//                    if (state.state.name == name)
//                    {
//                        return blendTree;
//                    }
//                    var nestedResult = FindBlendTreeRecursive(blendTree, name);
//                    if (nestedResult != null)
//                    {
//                        return nestedResult;
//                    }
//                }
//            }

//            foreach (var childStateMachine in stateMachine.stateMachines)
//            {
//                var result = FindBlendTreeRecursive(childStateMachine.stateMachine, name);
//                if (result != null)
//                {
//                    return result;
//                }
//            }

//            return null;
//        }

//        BlendTree FindBlendTreeRecursive(BlendTree blendTree, string name)
//        {
//            foreach (var child in blendTree.children)
//            {
//                if (child.motion is BlendTree childBlendTree)
//                {
//                    if (childBlendTree.name == name)
//                    {
//                        return childBlendTree;
//                    }
//                    var nestedResult = FindBlendTreeRecursive(childBlendTree, name);
//                    if (nestedResult != null)
//                    {
//                        return nestedResult;
//                    }
//                }
//            }
//            return null;
//        }

//        AnimatorState GetStateInStateMachine(AnimatorStateMachine stateMachine, string stateName)
//        {
//            foreach (var state in stateMachine.states)
//            {
//                if (state.state.name == stateName)
//                {
//                    return state.state;
//                }
//            }
//            return null;
//        }
//        AnimatorState GetStateInStateMachineRecursive(AnimatorStateMachine stateMachine, string stateName)
//        {
//            foreach (var state in stateMachine.states)
//            {
//                if (state.state.name == stateName)
//                {
//                    return state.state;
//                }
//            }
//            foreach (var item in stateMachine.stateMachines)
//            {
//                var state = GetStateInStateMachineRecursive(item.stateMachine, stateName);
//                if (state != null) return state;
//            }
//            return null;
//        }

//        void CopyParameters(AnimatorController source, AnimatorController target)
//        {
//            foreach (var parameter in source.parameters)
//            {
//                // Check if the parameter already exists in the target controller
//                if (!HasParameter(target, parameter.name))
//                {
//                    target.AddParameter(parameter.name, parameter.type);
//                }
//            }
//        }

//        bool HasParameter(AnimatorController controller, string parameterName)
//        {
//            foreach (var parameter in controller.parameters)
//            {
//                if (parameter.name == parameterName)
//                {
//                    return true;
//                }
//            }
//            return false;
//        }
//    }
//}
//#endif


#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FS_Core
{
    public class AnimatorMergerUtility
    {
        public AnimatorController sourceAnimatorController;
        public AnimatorController targetAnimatorController;

        public AnimatorMergerUtility(AnimatorController sourceController, AnimatorController targetController)
        {
            sourceAnimatorController = sourceController;
            targetAnimatorController = targetController;
        }

        public void MergeAnimatorControllers(AnimatorController sourceController = null, AnimatorController targetController = null)
        {
            if (sourceController == null)
                sourceController = sourceAnimatorController;

            if (targetController == null)
                targetController = targetAnimatorController;

            if (sourceController == null || targetController == null)
            {
                Debug.LogError("Both source and target Animator Controllers must be assigned.");
                return;
            }

            // Copy parameters
            CopyParameters(sourceController, targetController);

            // Merge all layers
            MergeLayers(sourceController, targetController);

            // Save changes
            EditorUtility.SetDirty(targetController);
            AssetDatabase.SaveAssets();
        }

        void MergeLayers(AnimatorController source, AnimatorController target)
        {
            // First, merge existing layers by name
            for (int i = 0; i < source.layers.Length; i++)
            {
                var sourceLayer = source.layers[i];
                var existingLayer = FindLayerByName(target, sourceLayer.name);

                if (existingLayer != null)
                {
                    // Merge content into existing layer
                    MergeStateMachines(sourceLayer.stateMachine, existingLayer.stateMachine);
                }
                else
                {
                    // Create a new layer
                    AnimatorControllerLayer newLayer = new AnimatorControllerLayer
                    {
                        name = sourceLayer.name,
                        defaultWeight = sourceLayer.defaultWeight,
                        syncedLayerIndex = sourceLayer.syncedLayerIndex,
                        syncedLayerAffectsTiming = sourceLayer.syncedLayerAffectsTiming,
                        iKPass = sourceLayer.iKPass,
                        stateMachine = new AnimatorStateMachine(),
                        avatarMask = sourceLayer.avatarMask,
                        blendingMode = sourceLayer.blendingMode
                    };

                    newLayer.stateMachine.name = sourceLayer.stateMachine.name;
                    AssetDatabase.AddObjectToAsset(newLayer.stateMachine, target);
                    newLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

                    // Copy the content to the new state machine
                    CopyStateMachineStructure(sourceLayer.stateMachine, newLayer.stateMachine);

                    target.AddLayer(newLayer);
                }
            }
        }

        AnimatorControllerLayer FindLayerByName(AnimatorController controller, string layerName)
        {
            return controller.layers.FirstOrDefault(l => l.name == layerName);
        }

        void MergeStateMachines(AnimatorStateMachine source, AnimatorStateMachine target)
        {
            // Copy state machine structure while merging blend trees
            CopyStateMachineStructure(source, target);
        }

        void CopyStateMachineStructure(AnimatorStateMachine source, AnimatorStateMachine target)
        {
            // Copy states
            foreach (var sourceState in source.states)
            {
                // Skip if the state already exists
                AnimatorState existingState = GetStateInStateMachine(target, sourceState.state.name);

                if (existingState == null)
                {
                    // Create new state
                    AnimatorState newState = target.AddState(sourceState.state.name, sourceState.position);
                    CopyStateProperties(sourceState.state, newState);
                }
                else
                {
                    // Merge existing state if needed (especially for blend trees)
                    MergeStateProperties(sourceState.state, existingState);
                }
            }

            // Copy child state machines
            foreach (var sourceChildStateMachine in source.stateMachines)
            {
                // Check if this child state machine already exists
                var existingChildStateMachine = target.stateMachines
                    .FirstOrDefault(sm => sm.stateMachine.name == sourceChildStateMachine.stateMachine.name);

                AnimatorStateMachine targetChildStateMachine;

                if (existingChildStateMachine.stateMachine == null)
                {
                    // Create new child state machine
                    targetChildStateMachine = new AnimatorStateMachine();
                    targetChildStateMachine.name = sourceChildStateMachine.stateMachine.name;
                    AssetDatabase.AddObjectToAsset(targetChildStateMachine, targetAnimatorController);
                    targetChildStateMachine.hideFlags = HideFlags.HideInHierarchy;
                    // First add the state machine, then we can get a reference to it
                    target.AddStateMachine(
                        sourceChildStateMachine.stateMachine.name,
                        sourceChildStateMachine.position
                    );

                    // Get the reference to the newly created state machine
                    targetChildStateMachine = target.stateMachines
                        .First(sm => sm.stateMachine.name == sourceChildStateMachine.stateMachine.name)
                        .stateMachine;
                }
                else
                {
                    targetChildStateMachine = existingChildStateMachine.stateMachine;
                }

                // Recursively copy/merge the child state machine
                CopyStateMachineStructure(sourceChildStateMachine.stateMachine, targetChildStateMachine);
            }

            // Copy any state transitions
            foreach (var sourceTransition in source.anyStateTransitions)
            {
                AnimatorState destinationState = GetStateInStateMachine(target, sourceTransition.destinationState.name);
                if (destinationState != null && !target.anyStateTransitions.Any(t => t.destinationState == destinationState))
                {
                    AnimatorStateTransition newTransition = target.AddAnyStateTransition(destinationState);
                    CopyTransitionProperties(sourceTransition, newTransition);
                }
            }

            // Copy transitions for each state
            foreach (var sourceState in source.states)
            {
                AnimatorState targetState = GetStateInStateMachine(target, sourceState.state.name);

                if (targetState != null)
                {
                    foreach (var sourceTransition in sourceState.state.transitions)
                    {
                        AnimatorState destinationState = null;

                        // First try to find the state in the current state machine
                        if (sourceTransition.destinationState != null)
                        {
                            destinationState = GetStateInStateMachine(target, sourceTransition.destinationState.name);

                            // If not found, try to find it recursively in the entire animator
                            if (destinationState == null)
                            {
                                destinationState = GetStateInStateMachineRecursive(
                                    targetAnimatorController.layers.FirstOrDefault(l => l.stateMachine == target)?.stateMachine ??
                                    targetAnimatorController.layers[0].stateMachine,
                                    sourceTransition.destinationState.name);
                            }

                            // Only add the transition if the destination state exists and the transition doesn't already exist
                            if (destinationState != null && !targetState.transitions.Any(t =>
                                t.destinationState == destinationState &&
                                TransitionsAreEquivalent(t, sourceTransition)))
                            {
                                AnimatorStateTransition newTransition = targetState.AddTransition(destinationState);
                                CopyTransitionProperties(sourceTransition, newTransition);
                            }
                        }
                    }
                }
            }

            // Copy default state if it exists
            if (source.defaultState != null)
            {
                AnimatorState targetDefaultState = GetStateInStateMachine(target, source.defaultState.name);
                if (targetDefaultState != null)
                {
                    target.defaultState = targetDefaultState;
                }
            }
        }

        bool TransitionsAreEquivalent(AnimatorStateTransition t1, AnimatorStateTransition t2)
        {
            // Check if transitions have the same conditions
            if (t1.conditions.Length != t2.conditions.Length)
                return false;

            foreach (var c1 in t1.conditions)
            {
                bool conditionMatched = false;
                foreach (var c2 in t2.conditions)
                {
                    if (c1.parameter == c2.parameter && c1.mode == c2.mode &&
                        Mathf.Approximately(c1.threshold, c2.threshold))
                    {
                        conditionMatched = true;
                        break;
                    }
                }
                if (!conditionMatched)
                    return false;
            }

            return true;
        }

        void MergeStateProperties(AnimatorState source, AnimatorState target)
        {
            // For blend trees, we want to merge them if they exist in both
            if (source.motion is BlendTree sourceBlendTree && target.motion is BlendTree targetBlendTree)
            {
                MergeBlendTrees(sourceBlendTree, targetBlendTree);
            }
            else if (source.motion is BlendTree && target.motion == null)
            {
                // If target has no motion but source has a blend tree, copy it
                target.motion = CopyBlendTree(source.motion as BlendTree);
            }
            // We don't override other properties if the state already exists
        }

        void MergeBlendTrees(BlendTree source, BlendTree target)
        {
            // Only proceed if blend trees are of the same type
            if (source.blendType != target.blendType)
            {
                Debug.LogWarning($"Cannot merge blend trees of different types: {source.name} and {target.name}");
                return;
            }

            // Merge children from source to target (only add missing ones)
            foreach (var sourceChild in source.children)
            {
                // Check if this motion or a similar one already exists in the target blend tree
                bool exists = false;

                foreach (var targetChild in target.children)
                {
                    if (MotionsAreEquivalent(sourceChild.motion, targetChild.motion))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    // Add the child to the target blend tree
                    Motion childMotion;

                    if (sourceChild.motion is BlendTree childBlendTree)
                    {
                        childMotion = CopyBlendTree(childBlendTree);
                    }
                    else
                    {
                        childMotion = sourceChild.motion;
                    }

                    // Add the child based on blend tree type
                    if (target.blendType == BlendTreeType.SimpleDirectional2D ||
                        target.blendType == BlendTreeType.FreeformDirectional2D ||
                        target.blendType == BlendTreeType.FreeformCartesian2D)
                    {
                        target.AddChild(childMotion, sourceChild.position);
                    }
                    else
                    {
                        target.AddChild(childMotion, sourceChild.threshold);
                    }

                    // Find the index of the newly added child
                    int childIndex = target.children.Length - 1;

                    // Set additional properties
                    SerializedObject serializedTree = new SerializedObject(target);
                    SerializedProperty childrenProp = serializedTree.FindProperty("m_Childs");

                    if (childrenProp != null && childIndex < childrenProp.arraySize)
                    {
                        SerializedProperty childProp = childrenProp.GetArrayElementAtIndex(childIndex);
                        childProp.FindPropertyRelative("m_TimeScale").floatValue = sourceChild.timeScale;
                        childProp.FindPropertyRelative("m_CycleOffset").floatValue = sourceChild.cycleOffset;
                        childProp.FindPropertyRelative("m_Mirror").boolValue = sourceChild.mirror;
                    }

                    serializedTree.ApplyModifiedProperties();
                }
            }
        }

        bool MotionsAreEquivalent(Motion motion1, Motion motion2)
        {
            // Simple check if motions are the same asset
            if (motion1 == motion2)
                return true;

            // If they're both blend trees, we could implement deeper comparison
            // For now, just check names
            if (motion1 is BlendTree tree1 && motion2 is BlendTree tree2)
            {
                return tree1.name == tree2.name;
            }

            // For regular animation clips, compare by name
            if (motion1 != null && motion2 != null)
            {
                return motion1.name == motion2.name;
            }

            return false;
        }

        void CopyStateProperties(AnimatorState source, AnimatorState target)
        {
            target.speed = source.speed;
            target.cycleOffset = source.cycleOffset;
            target.mirror = source.mirror;
            target.mirrorParameterActive = source.mirrorParameterActive;
            target.mirrorParameter = source.mirrorParameter;
            target.iKOnFeet = source.iKOnFeet;
            target.writeDefaultValues = source.writeDefaultValues;
            target.tag = source.tag;

            if (source.motion is BlendTree)
            {
                target.motion = CopyBlendTree(source.motion as BlendTree);
            }
            else
            {
                target.motion = source.motion;
            }

            // Copy behaviors
            foreach (var behavior in source.behaviours)
            {
                var newBehavior = target.AddStateMachineBehaviour(behavior.GetType());
                EditorUtility.CopySerialized(behavior, newBehavior);
            }
        }

        void CopyTransitionProperties(AnimatorStateTransition source, AnimatorStateTransition target)
        {
            target.duration = source.duration;
            target.offset = source.offset;
            target.interruptionSource = source.interruptionSource;
            target.orderedInterruption = source.orderedInterruption;
            target.exitTime = source.exitTime;
            target.hasExitTime = source.hasExitTime;
            target.hasFixedDuration = source.hasFixedDuration;
            target.canTransitionToSelf = source.canTransitionToSelf;

            // Copy conditions
            foreach (var condition in source.conditions)
            {
                target.AddCondition(condition.mode, condition.threshold, condition.parameter);
            }
        }

        BlendTree CopyBlendTree(BlendTree source)
        {
            BlendTree newTree = new BlendTree
            {
                name = source.name,
                blendType = source.blendType,
                blendParameter = source.blendParameter,
                blendParameterY = source.blendParameterY,
                minThreshold = source.minThreshold,
                maxThreshold = source.maxThreshold,
                useAutomaticThresholds = source.useAutomaticThresholds
            };

            // Make sure the new tree is added to the asset 
            AssetDatabase.AddObjectToAsset(newTree, targetAnimatorController);
            newTree.hideFlags = HideFlags.HideInHierarchy;

            for (int i = 0; i < source.children.Length; i++)
            {
                var child = source.children[i];
                Motion childMotion;

                if (child.motion is BlendTree childBlendTree)
                {
                    childMotion = CopyBlendTree(childBlendTree);
                }
                else
                {
                    childMotion = child.motion;
                }

                // Add the child based on blend tree type
                if (newTree.blendType == BlendTreeType.SimpleDirectional2D ||
                    newTree.blendType == BlendTreeType.FreeformDirectional2D ||
                    newTree.blendType == BlendTreeType.FreeformCartesian2D)
                {
                    newTree.AddChild(childMotion, child.position);
                }
                else
                {
                    newTree.AddChild(childMotion, child.threshold);
                }

                SerializedObject serializedTree = new SerializedObject(newTree);
                SerializedProperty childrenProp = serializedTree.FindProperty("m_Childs");

                if (childrenProp != null && i < childrenProp.arraySize)
                {
                    SerializedProperty childProp = childrenProp.GetArrayElementAtIndex(i);
                    childProp.FindPropertyRelative("m_TimeScale").floatValue = child.timeScale;
                    childProp.FindPropertyRelative("m_CycleOffset").floatValue = child.cycleOffset;
                    childProp.FindPropertyRelative("m_Mirror").boolValue = child.mirror;
                }

                serializedTree.ApplyModifiedProperties();
            }

            return newTree;
        }

        AnimatorState GetStateInStateMachine(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.name == stateName)
                {
                    return state.state;
                }
            }
            return null;
        }

        AnimatorState GetStateInStateMachineRecursive(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.name == stateName)
                {
                    return state.state;
                }
            }

            foreach (var item in stateMachine.stateMachines)
            {
                var state = GetStateInStateMachineRecursive(item.stateMachine, stateName);
                if (state != null) return state;
            }

            return null;
        }

        void CopyParameters(AnimatorController source, AnimatorController target)
        {
            foreach (var parameter in source.parameters)
            {
                // Check if the parameter already exists in the target controller
                if (!HasParameter(target, parameter.name))
                {
                    target.AddParameter(parameter.name, parameter.type);
                }
            }
        }

        bool HasParameter(AnimatorController controller, string parameterName)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
#endif