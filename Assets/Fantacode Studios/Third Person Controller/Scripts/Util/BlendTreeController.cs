# if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace FS_Core
{

    public class BlendTreeController : MonoBehaviour
    {
        public static void AddAnimationToBlendTree(BlendTree blendTree, AnimationClip animationClip, float threshold)
        {
            AddOrOverrideAnimationInBlendTree(blendTree, animationClip, threshold);
        }

        private static void AddOrOverrideAnimationInBlendTree(BlendTree blendTree, AnimationClip animationClip, float threshold)
        {
            if(blendTree == null)
            {
                Debug.LogWarning($"Blend Tree not found");
                return;
            }

            Undo.RecordObject(blendTree, "Modify Blend Tree");

            var children = blendTree.children;
            bool thresholdExists = false;

            for (int i = 0; i < children.Length; i++)
            {
                if (Mathf.Approximately(children[i].threshold, threshold))
                {
                    children[i].motion = animationClip;
                    thresholdExists = true;
                    break;
                }
            }

            blendTree.children = children;
            EditorUtility.SetDirty(blendTree);

            if (!thresholdExists)
            {
                blendTree.AddChild(animationClip, threshold);
            }
        }

        private static List<BlendTree> GetBlendTreesFromStateMachine(AnimatorStateMachine stateMachine)
        {
            var blendTrees = new List<BlendTree>();

            foreach (var state in stateMachine.states)
            {
                if (state.state.motion is BlendTree blendTree)
                {
                    blendTrees.Add(blendTree);
                }
            }

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                blendTrees.AddRange(GetBlendTreesFromStateMachine(subStateMachine.stateMachine));
            }

            return blendTrees;
        }

        public static AnimatorControllerLayer FindLayerByName(AnimatorController controller, string layerName)
        {
            foreach (var layer in controller.layers)
            {
                if (layer.name == layerName)
                    return layer;
            }
            return null;
        }

        public static BlendTree GetArmLayerBlendTree(AnimatorController controller, string blendTreeName)
        {
            var layer = BlendTreeController.FindLayerByName(controller, AnimatorLayer.armLayer.layerName);

            var stateMachine = layer.stateMachine;

            foreach (var state in stateMachine.states)
            {
                if (state.state.motion is BlendTree bt && bt.name == blendTreeName)
                {
                    return bt;
                }
            }

            return null;
        }

        public static void ChangeAnimationClip(AnimatorController animatorController, string layerName, string stateName, AnimationClip newClip)
        {
            if (animatorController == null || newClip == null)
            {
                //Debug.LogWarning("AnimatorController or new animation clip is null.");
                return;
            }

            // Find the layer
            AnimatorControllerLayer layer = null;
            foreach (var l in animatorController.layers)
            {
                if (l.name == layerName)
                {
                    layer = l;
                    break;
                }
            }

            if (layer == null)
            {
                //Debug.LogWarning($"Layer '{layerName}' not found in AnimatorController.");
                return;
            }

            // Find the state in the layer
            AnimatorState state = null;
            foreach (var stateMachine in layer.stateMachine.states)
            {
                if (stateMachine.state.name == stateName)
                {
                    state = stateMachine.state;
                    break;
                }
            }

            if (state == null)
            {
                //Debug.LogWarning($"State '{stateName}' not found in layer '{layerName}'.");
                return;
            }

            // Change the animation clip
            state.motion = newClip;
            //Debug.Log($"Replaced animation clip in state '{stateName}' on layer '{layerName}'.");

            // Mark as dirty to save changes
            EditorUtility.SetDirty(animatorController);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif