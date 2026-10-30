using FS_LocomotionSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FS_Core
{
    public enum Mask { Null, Arm, UpperBody, RightHand, LeftHand, UpperBodyWithRoot, RightArm, UpperBodyWithoutArm }

    public enum UpdateMode { Normal, Unscaled };

    // ANIMGRAPH WORKING

    // Default Graph Structure
    // Animator Mixer -> Locomotion Override Mixer -> Loop Action Mixer -> Action Mixer -> Final Mixer

    // How a crossfade action works:
    // Animator Mixer -> Loop Action Mixer -> Clip Mixer ->  Action Mixer -> Final Mixer
    //                       Clip Playable -> 

    // How a second crossfade action works
    // Animator Mixer -> Loop Action Mixer -> Clip Mixer    -> Clip Mixer ->  Action Mixer -> Final Mixer
    //                       Clip Playable -> Clip Playable ->

    // How a crossfade loop action works:
    // Animator Mixer -> Clip Mixer -> Loop Action Mixer -> Action Mixer -> Final Mixer
    //  Clip Playable -> 

    public class AnimGraph : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationMixerPlayable finalOutputMixer;
        AnimationMixerPlayable actionMixer;
        AnimationMixerPlayable loopActionMixer;
        AnimationMixerPlayable locomotionOverrideMixer;
        AnimationMixerPlayable locomotionBlendMixer;
        AnimationMixerPlayable crouchBlendMixer;
        AnimationMixerPlayable locomotionTypeMixer;
        bool isLocomotionOverrideActive;
        bool hasWalkClip;
        bool is2DOverride;
        Vector2[] clipPositions2D;
        float[] clipWeights2D;
        int idleClipIndex;
        Vector2[] crouchClipPositions2D;
        float[] crouchClipWeights2D;
        int crouchIdleClipIndex;
        bool hasLocomotionCrouchLayer;
        Coroutine locomotionOverrideTransition;
        Playable animatorMixer;
        Playable animatorOutput;
        AnimatorControllerPlayable playableAnimator;

        public Animator animator;
        AvatarMask armMask;
        AvatarMask upperBodyMask;
        AvatarMask rightHandMask;
        AvatarMask upperBodyWithRoot;
        AvatarMask rightArm;
        AvatarMask leftHandMask;
        AvatarMask upperBodyWithoutArm;
        bool currentAnimationIsAdditive = false;
        bool isLastAnimationNonTransitionBack = false;
        UpdateMode updateMode;

        bool cancelCurrentAction = false;
        public bool IsUpdatingOverrideController { get; private set; } = false;
        public void CancelCurrentAction()
        {
            cancelCurrentAction = true;
        }

        public int LastRebakeFrame { get; private set; } = -2;

        int lastActionAnimationFrame = -2;

        /// <summary>
        /// True while a one-shot action clip is still being driven — including the
        /// transition-out it plays after being cancelled.
        ///
        /// ActionExecutor.Cancel clears its currentAction immediately, so IsBusy goes
        /// false while the clip is still fading out; this reports the animation itself.
        /// Callers that must not splice a new animation on top of a dying one (e.g.
        /// starting to aim the moment an AI's investigate animation is cancelled) can
        /// wait on this. Frame-stamped, so it self-clears if the driving coroutine is
        /// ever stopped outright.
        /// </summary>
        public bool IsPlayingActionAnimation => Time.frameCount - lastActionAnimationFrame <= 1;

        public PlayableGraph Graph => graph;

        public float DeltaTime => updateMode == UpdateMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

        public CurrentClipStateInfo currentClipStateInfo = new CurrentClipStateInfo();
        public static KeyValuePair<string, float> TimeScaleOwner = new KeyValuePair<string, float>("", 1);

        public class CurrentClipStateInfo
        {
            public AnimGraphClipInfo currentClipInfo;
            public Playable currentMixer;
            public float normalizedTime = 0f;
            public float timer = 0f;
            public float deltaTime = 0f;
            public float clipLength = 0f;
            public float n;
            public bool isPlayingAnimation => currentClipInfo != null;
        }
        public AnimGraphClipInfo currentClipInfo => currentClipStateInfo.currentClipInfo;

        Playable currentPlayableMixer;
        public void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            CreateGraph();
        }

        public void Update()
        {
            if (animator != null)
            {
                if (playableAnimator.GetSpeed() != animator.speed)
                    playableAnimator.SetSpeed(animator.speed);

                SaveTransitionData();
                UpdateLocomotionOverride();
            }
        }

        private void OnDestroy()
        {
            graph.Destroy();
        }

        #region Create Graph

        void CreateGraph(AnimatorOverrideController runtimeAnimatorController = null)
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            graph = PlayableGraph.Create(gameObject.name + " graph");
            output = AnimationPlayableOutput.Create(graph, "Animation", animator);

            // Animator Controller -> Animator Output Mixer -> Animator Mixer -> Locomotion Override Mixer -> Loop Action Mixer -> Action Mixer -> Final Output Mixer
            // 1. Animator Controller Playable - Playable Created Using the Animator Controller
            // 2. Animator Output Mixer - Just a mixer used to avoid directly replacing connections of Animator Controller Playable
            // 3. Animator Mixer - Mixer to blend between base animator and override animator controller
            // 4. Locomotion Override Mixer - Mixer to blend between base locomotion and custom idle/walk/run clips
            // 5. Loop Action Mixer - Mixer to handle looping animations
            // 6. Action Mixer - Mixer to handle non-looping animations
            // 7. Final Output Mixer - Final mixer that will be connected to the output

            playableAnimator = AnimatorControllerPlayable.Create(graph, runtimeAnimatorController == null ? animator.runtimeAnimatorController : runtimeAnimatorController);
            animatorOutput = AnimationMixerPlayable.Create(graph, 2);
            animatorMixer = AnimationMixerPlayable.Create(graph, 3);
            locomotionOverrideMixer = AnimationMixerPlayable.Create(graph, 2);
            loopActionMixer = AnimationMixerPlayable.Create(graph, 2);
            actionMixer = AnimationMixerPlayable.Create(graph, 2);
            finalOutputMixer = AnimationMixerPlayable.Create(graph, 2);

            graph.Connect(playableAnimator, 0, animatorOutput, 0);
            graph.Connect(animatorOutput, 0, animatorMixer, 0);
            graph.Connect(animatorMixer, 0, locomotionOverrideMixer, 0);
            graph.Connect(locomotionOverrideMixer, 0, loopActionMixer, 0);
            graph.Connect(loopActionMixer, 0, actionMixer, 0);
            graph.Connect(actionMixer, 0, finalOutputMixer, 0);

            SetDefaultMixerWeights(animatorOutput);
            SetDefaultMixerWeights(animatorMixer, inputCount: 3);
            SetDefaultMixerWeights(locomotionOverrideMixer);
            SetDefaultMixerWeights(loopActionMixer);
            SetDefaultMixerWeights(actionMixer);
            SetDefaultMixerWeights(finalOutputMixer);

            output.SetSourcePlayable(finalOutputMixer);

            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            graph.Play();

            CreateDefaultMask();
        }

        void CreateDefaultMask()
        {
            armMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                armMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);

            rightArm = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                rightArm.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            rightArm.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            rightArm.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            rightArm.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

            upperBodyMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                upperBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);

            rightHandMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                rightHandMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            rightHandMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            rightHandMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

            leftHandMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                leftHandMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            leftHandMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            leftHandMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);

            upperBodyWithRoot = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                upperBodyWithRoot.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            upperBodyWithRoot.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);

            upperBodyWithoutArm = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                upperBodyWithoutArm.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            upperBodyWithoutArm.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            upperBodyWithoutArm.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        }

        #endregion

        #region Crossfade methods

        public void Crossfade(AnimationClip clip, AnimGraphClipInfo clipInfo = null, bool transitionBack = true, 
            Mask mask = Mask.Null, bool isAdditiveLayerAnimation = false, float transitionIn = .2f, 
            float transitionOut = .2f, float animationSpeed = 1, Action<float, float> onAnimationUpdate = null, 
            Action OnComplete = null, bool overridableByAnimator = false, params ActionData[] actions)
        {
            StartCoroutine(CrossfadeAsync(clip, clipInfo, false, transitionBack, mask, isAdditiveLayerAnimation, 
                transitionIn, transitionOut, animationSpeed, onAnimationUpdate, OnComplete, overridableByAnimator, actions));
        }

        public void PlayLoopingAnimation(AnimationClip clip, AnimGraphClipInfo clipInfo = null, Mask mask = Mask.Null, 
            bool isAdditiveLayerAnimation = false, float transitionIn = .2f, float transitionOut = .2f, 
            float animationSpeed = 1, Action<float, float> onAnimationUpdate = null, Action OnComplete = null)
        {
            StartCoroutine(CrossfadeAsync(clip, clipInfo, true, false, mask, isAdditiveLayerAnimation, 
                transitionIn, transitionOut, animationSpeed, onAnimationUpdate, OnComplete, overridableByAnimator: false));
        }

        public IEnumerator CrossfadeAsync(AnimationClip clip, AnimGraphClipInfo clipInfo = null, 
            bool isLoopingAnimation = false, bool transitionBack = true, Mask mask = Mask.Null, 
            bool isAdditiveLayerAnimation = false, float transitionIn = .2f, float transitionOut = .2f, 
            float animationSpeed = 1, Action<float, float> onAnimationUpdate = null, Action OnComplete = null, 
            bool overridableByAnimator = false, params ActionData[] actions)
        {
            if (clipInfo != null)
            {
                if (clip == null) clip = clipInfo.clip;
            }
            if (clip == null)
                yield break;

            var currentActionMixer = GetActionMixer(isLoopingAnimation);
            var source = currentActionMixer.GetInput(0);
            currentActionMixer.DisconnectInput(0);

            Playable clipPlayable = CreateClipPlayable(clip, animationSpeed);

            if (clipInfo != null && clipInfo.useLayeredAnimation && clipInfo.layerClip != null)
            {
                var innerMixer = AnimationLayerMixerPlayable.Create(graph, 2);

                graph.Connect(clipPlayable, 0, innerMixer, 0);
                innerMixer.SetInputWeight(0, 1f);

                var layerPlayable = CreateClipPlayable(clipInfo.layerClip, animationSpeed);
                graph.Connect(layerPlayable, 0, innerMixer, 1);
                innerMixer.SetInputWeight(1, 1f);

                if (clipInfo.layerIsAdditive)
                    innerMixer.SetLayerAdditive(1, true);

                var layerAvatarMask = GetAvatarMask(clipInfo.layerMask);
                if (layerAvatarMask != null)
                    innerMixer.SetLayerMaskFromAvatarMask(1, layerAvatarMask);

                clipPlayable = innerMixer;
            }

            Playable clipMixer = CreateClipMixer(mask, isAdditiveLayerAnimation);

            // Connect source and clip -> clip mixer
            ConnectToMixer(clipMixer, source, clipPlayable);

            // Connect clip mixer -> current action mixer
            ConnectPlayables(clipMixer, currentActionMixer);
            
            SetDefaultMixerWeights(clipMixer);
            SetDefaultMixerWeights(currentActionMixer);

            ResetTransitionPose();

            if (isLoopingAnimation)
                currentPlayableMixer = clipMixer;

            // Must mirror CreateClipMixer: additive animations always get a layer
            // mixer, so their base input (0) must keep weight 1 during the blend.
            bool isLayerMixer = mask != Mask.Null || isAdditiveLayerAnimation;


            yield return UpdateWeights(clip, clipPlayable, clipMixer, currentActionMixer, clipInfo, isLoopingAnimation,
                isAdditiveLayerAnimation, transitionBack, isLayerMixer, transitionIn, transitionOut, animationSpeed,
                onAnimationUpdate, overridableByAnimator, actions);

            if (transitionBack)
            {
                if (clipMixer.IsValid())
                {
                    // Determine the correct input mixer:
                    // - If the current mixer is the loopActionMixer, use animatorMixer (to bypass loop).
                    // - Otherwise, continue using loopActionMixer for normal chaining.
                    var inputMixer = currentActionMixer.Equals(loopActionMixer) ? (Playable)locomotionOverrideMixer : loopActionMixer;

                    // Choose the correct playable to connect:
                    // - If this is an additive layer animation, connect to the base of the clipMixer (clipMixer.GetInput(0))
                    // - Otherwise, connect to the selected inputMixer
                    var m = isAdditiveLayerAnimation ? clipMixer.GetInput(0) : inputMixer;

                    // Start coroutine to connect the output of this clip to the next mixer in the chain
                    StartCoroutine(ConnectPlayablesSmoothly(clipMixer.GetOutput(0), m));
                }
            }
            else if (!isLoopingAnimation)
            {
                if (!currentAnimationIsAdditive)
                    StartCoroutine(ChangePreviousPlayableWeight(currentActionMixer, clipMixer));
            }

            OnComplete?.Invoke();
        }

        public IEnumerator CrossfadeAsync(AnimGraphClipInfo clipInfo, AnimParameters p)
        {
            if (p == null)
            {
                p = new AnimParameters();
                p.transitionIn = clipInfo.TranistionInAndOut.x;
                p.transitionOut = clipInfo.TranistionInAndOut.y;
            }

            yield return CrossfadeAsync(clipInfo.clip, clipInfo, p.isLoopingAnimation, p.transitionBack, p.mask, 
                p.isAdditiveLayerAnimation, p.transitionIn, p.transitionOut, 
                p.animationSpeed, p.onAnimationUpdate, p.OnComplete, p.overridableByAnimator);
        }
        #endregion

        #region Override Controller

        public void UpdateOverrideController(AnimatorOverrideController overrideController, bool makeTransition = true)
        {
            StartCoroutine(UpdateOverrideControllerAsync(overrideController, makeTransition));
        }

        public IEnumerator UpdateOverrideControllerAsync(AnimatorOverrideController overrideController, bool makeTransition = true)
        {
            // Prevent concurrent executions — wait for any in-progress update to finish first
            while (IsUpdatingOverrideController)
                yield return null;

            

            IsUpdatingOverrideController = true;

            if (overrideController != null)
            {
                // Save Current Animation State (Time & Speed) 
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                float normalizedTime = currentState.normalizedTime;
                float speed = animator.speed; // Save animation speed
                if (makeTransition)
                    SaveAnimatorParameters(); // Save existing animator parameters

                var anim = AnimatorControllerPlayable.Create(graph, overrideController);
                var clipMixer = AnimationMixerPlayable.Create(graph, 2);

                clipMixer.ConnectInput(0, anim, 0);
                clipMixer.SetInputWeight(0, 1);
                clipMixer.SetInputWeight(1, 0);

                int sourPort = animatorMixer.GetInput(1).IsValid() ? 1 : 0;
                int destPort = animatorMixer.GetInput(1).IsValid() ? 2 : 1;

                graph.Connect(clipMixer, 0, animatorMixer, destPort);

                animatorMixer.SetInputWeight(sourPort, 1);
                animatorMixer.SetInputWeight(destPort, 0);

                if (makeTransition)
                {
                    graph.Evaluate();
                    animator.Update(0);

                    //ResetTransitionPose();

                    RestoreAnimatorParameters(); // Restore saved parameters

                    // Resume Animation from Saved Pose
                    AnimatorStateInfo newState = animator.GetCurrentAnimatorStateInfo(0);
                    if (newState.fullPathHash == currentState.fullPathHash)
                    {
                        animator.Play(newState.fullPathHash, 0, normalizedTime % 1);
                    }

                    animator.speed = speed; // Restore animation speed


                    // Smooth Transition to New Animator
                    yield return MakeTransition(sourPort, destPort);
                }
                else
                {
                    animatorMixer.SetInputWeight(sourPort, 0);
                    animatorMixer.SetInputWeight(destPort, 1);
                }

                if (destPort == 2)
                {
                    animatorMixer.DisconnectInput(2);
                    var port1Mixer = animatorMixer.GetInput(1);
                    var overrideAnimator = animatorMixer.GetInput(1).GetInput(0);

                    animatorMixer.DisconnectInput(1);

                    graph.Connect(clipMixer, 0, animatorMixer, 1);
                    animatorMixer.SetInputWeight(0, 0);
                    animatorMixer.SetInputWeight(1, 1);

                    port1Mixer.Destroy();
                    overrideAnimator.Destroy();

                    ResetTransitionPose();
                }
                playableAnimator = anim;
            }

            else if (animatorMixer.GetInputWeight(0) < .5f)
            {
                int sourPort = 1;
                int destPort = 0;

                if (makeTransition)
                {
                    // 4. Smooth Transition to New Animator
                    yield return MakeTransition(sourPort, destPort);
                }
                else
                {
                    animatorMixer.SetInputWeight(sourPort, 0);
                    animatorMixer.SetInputWeight(destPort, 1);
                }

                var port1Mixer = animatorMixer.GetInput(1);
                if (!port1Mixer.IsValid())
                {
                    IsUpdatingOverrideController = false;
                    yield break;
                }
                var overrideAnimator = port1Mixer.GetInput(0);

                animatorMixer.DisconnectInput(1);
                // Evaluate with 0 delta time to flush Unity's internal job references to
                // port1Mixer/overrideAnimator before destroying them. Without this, the
                // automatic GameTime evaluation may still hold native pointers to these
                // nodes (from the MakeTransition frames), causing a crash on Destroy().
                graph.Evaluate();
                port1Mixer.Destroy();
                if (overrideAnimator.IsValid())
                    overrideAnimator.Destroy();

                var baseAnimOutput = animatorMixer.GetInput(0);
                if (baseAnimOutput.IsValid())
                {
                    var baseAnim = baseAnimOutput.GetInput(0);
                    if (baseAnim.IsValid())
                        playableAnimator = (AnimatorControllerPlayable)baseAnim;
                }

                ResetTransitionPose();
            }

            IsUpdatingOverrideController = false; 
        }

        IEnumerator MakeTransition(int sourPort, int destPort)
        {
            float transitionTime = 0.2f;
            float timer = 0;
            float weight = 0;

            while (timer < transitionTime)
            {
                //ResetTransitionPose();

                weight = Mathf.Lerp(1, 0, timer / transitionTime);
                if (timer >= transitionTime - DeltaTime) weight = 0;
                animatorMixer.SetInputWeight(sourPort, weight);
                animatorMixer.SetInputWeight(destPort, 1 - weight);
                timer += DeltaTime;
                yield return null;
            }
        }

        Dictionary<string, object> savedParameters = new Dictionary<string, object>();
        private void SaveAnimatorParameters()
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (animator.IsParameterControlledByCurve(parameter.nameHash)) continue;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        savedParameters[parameter.name] = animator.GetFloat(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Int:
                        savedParameters[parameter.name] = animator.GetInteger(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        savedParameters[parameter.name] = animator.GetBool(parameter.name);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        savedParameters[parameter.name] = animator.GetBool(parameter.name);
                        break;
                }
            }
        }
        private void RestoreAnimatorParameters()
        {
            foreach (var parameter in savedParameters)
            {
                if (parameter.Value is float)
                {
                    animator.SetFloat(parameter.Key, (float)parameter.Value);
                }
                else if (parameter.Value is int)
                {
                    animator.SetInteger(parameter.Key, (int)parameter.Value);
                }
                else if (parameter.Value is bool)
                {
                    animator.SetBool(parameter.Key, (bool)parameter.Value);
                }
            }
        }

        #endregion

        #region Weight handle methods  

        IEnumerator ChangePreviousPlayableWeight(Playable currentMixer, Playable mixer)
        {
            yield return new WaitUntil(() => !currentMixer.GetInput(0).Equals(mixer));
            float transitionTime = .2f;
            yield return new WaitForSeconds(transitionTime);
            if (mixer.IsValid())
            {
                mixer.SetInputWeight(0, 1);
                mixer.SetInputWeight(1, 0);
            }
        }

        IEnumerator ConnectPlayablesSmoothly(Playable outputMixer, Playable inputMixer)
        {
            //yield break;
            if (!outputMixer.IsValid() || !inputMixer.IsValid() || outputMixer.GetInput(0).Equals(inputMixer) || !animator.enabled)
                yield break;

            // Collect all intermediate playables
            HashSet<Playable> intermediates = new HashSet<Playable>();
            CollectPlayables(outputMixer.GetInput(0), inputMixer, ref intermediates);

            //var mixer = outputMixer.GetInput(0);
            //var clip = mixer.GetInput(1);

            //float firstInputStartWeight = mixer.GetInputWeight(0);
            //float secondInputStartWeight = mixer.GetInputWeight(1);
            var prevWeightFirst = outputMixer.GetInputWeight(0);
            var prevWeightSecond = (outputMixer.Equals(actionMixer) || outputMixer.Equals(loopActionMixer)) ? 1 : outputMixer.GetInputWeight(1);
            outputMixer.DisconnectInput(0);
            inputMixer.GetOutput(0).DisconnectInput(0);
            outputMixer.ConnectInput(0, inputMixer, 0);
            outputMixer.SetInputWeight(0, prevWeightFirst);

            graph.Evaluate();
            yield return null;
            ResetTransitionPose();
            //animator.Update(0);

            // A concurrent ConnectPlayablesSmoothly (e.g. an additive overlay and the
            // reaction beneath it finishing within a frame of each other) may have
            // restructured the chain and destroyed outputMixer during the yield.
            // Skip the weight restore but still destroy our orphaned intermediates.
            if (!outputMixer.IsValid())
            {
                foreach (var intermediate in intermediates)
                {
                    if (intermediate.IsValid())
                        intermediate.Destroy();
                }
                yield break;
            }


            //while (timer < duration)
            //{
            //    weight = Mathf.Lerp(prevWeightFirst, 1, timer / duration);
            //    if (timer >= duration - DeltaTime) weight = 0;

            //    if (1 - weight > prevWeightFirst)
            //        outputMixer.SetInputWeight(0, 1 - weight);
            //    outputMixer.SetInputWeight(1, weight);

            //    timer += DeltaTime;
            //    yield return null;
            //}

            outputMixer.SetInputWeight(0, prevWeightFirst);
            //outputMixer.SetInputWeight(1, 0);

            // Destroy all collected playables
            foreach (var intermediate in intermediates)
            {
                if (intermediate.IsValid())
                    intermediate.Destroy();
            }
            //outputMixer.GetInput(0).Destroy();

            //if (mixer.IsValid())
            //    mixer.Destroy();
            //if (clip.IsValid())
            //    clip.Destroy();
            yield break;
        }
        void CollectPlayables(Playable current, Playable target, ref HashSet<Playable> collected)
        {
            if (!current.IsValid() || collected.Contains(current) || current.Equals(target))
                return;

            collected.Add(current);

            int inputCount = current.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                CollectPlayables(current.GetInput(i), target, ref collected);
            }
        }
        IEnumerator LoopingAnimationTransitionback()
        {
            float timer = 0;
            float weight = 0;
            float transitionTime = .2f;
            var mixer = loopActionMixer.GetInput(0);
            if (((Playable)locomotionOverrideMixer).Equals(mixer) || !mixer.Equals(currentPlayableMixer))
            {
                yield break;
            }
            var prevMixer = mixer.GetInput(0);
            float firstInputStartWeight = mixer.GetInputWeight(0);

            bool resetOnce = false;

            while (timer < transitionTime)
            {
                //ResetTransitionPose();

                weight = Mathf.Lerp(firstInputStartWeight, 0, timer / transitionTime);
                if (timer >= transitionTime - DeltaTime) weight = 0;

                if (mixer.IsValid())
                {
                    if (1 - weight > mixer.GetInputWeight(0))
                        mixer.SetInputWeight(0, 1 - weight);
                    mixer.SetInputWeight(1, weight);
                }
                timer += DeltaTime;
                yield return null;
                if (!resetOnce)
                {
                    ResetTransitionPose();
                    resetOnce = true;
                }
            }

            if (mixer.IsValid())
            {
                var inputPlayable = mixer.GetInput(0);
                var outputPlayable = mixer.GetOutput(0);
                StartCoroutine(ConnectPlayablesSmoothly(outputPlayable, inputPlayable));
            }
            //yield return new WaitUntil(() => loopActionMixer.GetInput(0).Equals(mixer) || !mixer.IsValid());

            //if (mixer.IsValid())
            //{
            //    mixer.SetInputWeight(0, 1);
            //    mixer.SetInputWeight(1, 0);
            //    loopActionMixer.DisconnectInput(0);
            //    mixer.DisconnectInput(0);
            //    loopActionMixer.ConnectInput(0, prevMixer, 0);
            //    loopActionMixer.SetInputWeight(0, 1);
            //    loopActionMixer.SetInputWeight(1, 0);
            //    if(mixer.GetInput(1).IsValid())
            //        mixer.GetInput(1).Destroy();
            //    mixer.Destroy();
            //    ResetTransitionPose();
            //}
            if (!loopActionMixer.GetInput(0).Equals(locomotionOverrideMixer))
                currentPlayableMixer = loopActionMixer.GetInput(0);

            //ResetTransitionPose();
        }

        IEnumerator UpdateWeights(AnimationClip clip, Playable clipPlayable, Playable mixer, Playable currentMixer, 
            AnimGraphClipInfo clipInfo = null, bool isLoopingAnimation = false, bool isAdditiveLayerAnimation = false, 
            bool transitionBack = true, bool isLayerMixer = false, float transitionIn = .2f, float transitionOut = .2f, 
            float animationSpeed = 1, Action<float, float> onAnimationUpdate = null, bool overridableByAnimator = false, 
            params ActionData[] actions)
        {
            if (clipInfo != null)
                clip = clipInfo.clip ?? clip; // Assign clip if clipInfo has a valid clip

            if (clip == null)
                yield break; // Exit coroutine if no clip is available


            float timer = 0f;
            float normalizedTimer = 0f;
            float weight = 0f;

            float clipLength = clip.length;

            bool isSmallClip = clipLength < .5f;

            if (!isLoopingAnimation && isSmallClip) clipLength = .5f;


            // Ensure transitions don't exceed clip length
            transitionIn = transitionIn > clipLength ? clipLength * 0.3f : transitionIn;
            transitionOut = transitionOut > clipLength ? clipLength * 0.3f : transitionOut;

            // Initialize clipInfo if null
            clipInfo ??= new AnimGraphClipInfo { clip = clip };

            if (!isAdditiveLayerAnimation) 
            {
                // Set initial clip state info
                currentClipStateInfo.currentClipInfo = clipInfo;
                currentClipStateInfo.normalizedTime = normalizedTimer;
                currentClipStateInfo.clipLength = clipLength;
                currentClipStateInfo.timer = timer;
                currentClipStateInfo.currentMixer = mixer;
            }

            // Only a crossfade on the ACTION mixer may write this. It answers one question for the
            // "was I replaced?" check further down: is the thing now sitting on top of me an additive
            // overlay (keep playing underneath it) or a real action (bow out)? A looping animation
            // lives on the loop mixer and cannot replace an action, so it has no business changing
            // the answer — yet it used to. The carry idle starting mid-pickup, with a flinch already
            // layered on top, flipped this to false; the pickup then read the flinch as a real
            // takeover, cut itself off, and left ActionExecutor waiting on a clip timer that nothing
            // was advancing any more.
            if (!isLoopingAnimation)
                currentAnimationIsAdditive = isAdditiveLayerAnimation;

            // Generate a unique identifier for the animation instance
            string uniqueId = Guid.NewGuid().ToString();

            // Handle time scaling if using a global time scale
            if (clipInfo.customAnimationSpeed && clipInfo.useAsGlobalTimeScale)
                TimeScaleOwner = TimeScaleOwner.Key != "" ? new(uniqueId, TimeScaleOwner.Value) : new(uniqueId, Time.timeScale);

            float currentTimescale = TimeScaleOwner.Value;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            // Tracks whether the previous animation layer has already been removed during the transition.
            // If we are NOT looping, and the last animation does not require a transition back,
            // and transitionBack is enabled, we smoothly disconnect the previous playable
            // after the transition-in time has passed.
            bool previousAnimRemoved = false;

            // Animation loop with transition in/out handling
            while (timer <= clipLength)
            {
                // Mark that a one-shot action clip is being driven this frame, so
                // IsPlayingActionAnimation stays true right through a cancel's
                // transition-out (the cancel path below only fast-forwards the timer
                // to the transition-out; the clip keeps playing until it ends).
                if (!isLoopingAnimation && !isAdditiveLayerAnimation)
                    lastActionAnimationFrame = Time.frameCount;

                ApplyAnimationSpeed(clipInfo, clipPlayable, normalizedTimer, uniqueId, currentTimescale, ref animationSpeed);

                InvokeAnimationEvents(clipInfo, normalizedTimer, animationSpeed);
                InvokeCodeEvents(actions, normalizedTimer);

                // if cancel current action, skip to transition out
                // should only be called when the animation is not in any transition
                if (cancelCurrentAction && timer > transitionIn + DeltaTime && timer < clipLength - transitionOut)
                {
                    timer = clipLength - transitionOut + DeltaTime;
                    cancelCurrentAction = false;
                }

                if (timer <= transitionIn)      // Transition In to animation
                {
                    weight = Mathf.Lerp(0, 1, timer / transitionIn);
                    if (timer >= transitionIn - DeltaTime) weight = 1; // Ensure it reaches exactly 1
                }
                else if (isLoopingAnimation)
                {
                    yield break;
                }
                else if ((transitionBack) && timer > clipLength - transitionOut)    // Transition Out of animation
                {
                    weight = Mathf.Lerp(1, 0, (timer - (clipLength - transitionOut)) / transitionOut);
                    if (timer >= clipLength) weight = 0; // Ensure it reaches exactly 0
                }

                if (!isLoopingAnimation && timer > transitionIn)
                {
                    // If this animation does not support transitioning back,
                    // mark it so the system knows to handle the transition properly
                    // when a new animation is played next.
                    if (!transitionBack)
                        isLastAnimationNonTransitionBack = true;

                    if (!previousAnimRemoved && isLastAnimationNonTransitionBack && transitionBack && !isAdditiveLayerAnimation)
                    {
                        // mixer.GetInput(0) is the stale non-transitionBack clipMixer left in the
                        // graph by the previous animation. Bypass it by connecting mixer directly
                        // to what the stale mixer was reading from, then destroy the stale mixer.
                        var staleMixer = mixer.GetInput(0);
                        if (staleMixer.IsValid())
                        {
                            var beforeStale = staleMixer.GetInput(0);
                            if (beforeStale.IsValid())
                                StartCoroutine(ConnectPlayablesSmoothly(mixer, beforeStale));
                        }

                        isLastAnimationNonTransitionBack = false;
                        previousAnimRemoved = true;
                    }
                }
                // Edge Case: if overridable by animator and animator is in transition or matching target, exit coroutine
                if ((animator.isMatchingTarget || animator.IsInTransition(0)) && overridableByAnimator)
                {
                    SetDefaultMixerWeights(mixer);
                    yield break;
                }

                // Apply weights
                if (mixer.IsValid())
                {
                    // If it's a layered animation, we should not change the weight of the first input
                    // The new animation should be applied on top of the existing one
                    if (!isLayerMixer)
                        mixer.SetInputWeight(0, 1 - weight);

                    mixer.SetInputWeight(1, weight);
                }

                // Check for animation override conditions
                // was other animation played on top of this one?
                // if so, stop playing the current action by reseting weights once the transition of the new actions is complete
                bool isPlayingAnotherAction = !currentMixer.GetInput(0).Equals(mixer);
                if (isPlayingAnotherAction && transitionBack && !currentAnimationIsAdditive)
                {
                    var waitTIme = currentClipInfo != null ? currentClipInfo.TranistionInAndOut.x : .2f;
                    yield return new WaitForSeconds(waitTIme);
                    SetDefaultMixerWeights(mixer);
                    yield break;
                }

                // Update timers
                timer += DeltaTime * animationSpeed;
                normalizedTimer = timer / clipLength;

                if (!isAdditiveLayerAnimation)
                {
                    currentClipStateInfo.normalizedTime = normalizedTimer;
                    currentClipStateInfo.timer = timer;
                    currentClipStateInfo.deltaTime = DeltaTime * animationSpeed;
                }
                onAnimationUpdate?.Invoke(normalizedTimer, timer);

                yield return null;
            }

            InvokeAnimationEndEvents(clipInfo);

            if (!isAdditiveLayerAnimation)
                currentClipStateInfo.currentClipInfo = null;

            if (TimeScaleOwner.Key == uniqueId && clipInfo.customAnimationSpeed && clipInfo.useAsGlobalTimeScale)
            {
                Time.timeScale = currentTimescale;
                TimeScaleOwner = new KeyValuePair<string, float>("", Time.timeScale);
            }
        }

        #endregion

        #region Utility Methods

        // Invokes events passed by the designer in scriptable object
        void InvokeAnimationEvents(AnimGraphClipInfo clipInfo, float normalizedTimer, float animationSpeed)
        {
            // Invoke animation events at the correct time
            foreach (var item in clipInfo.events)
            {
                if (item.normalizedTime >= normalizedTimer && item.normalizedTime <= normalizedTimer + DeltaTime * animationSpeed)
                {
                    item.InvokeCustomAnimationEvent(gameObject);
                    item.unityEvent.Invoke();
                }
            }
        }

        // Invokes animation end events passed by the designer in scriptable object
        void InvokeAnimationEndEvents(AnimGraphClipInfo clipInfo)
        {
            foreach (var item in clipInfo.onEndAnimation)
            {
                item.InvokeCustomAnimationEvent(this.gameObject);
                item.unityEvent.Invoke();
            }
        }

        // Invokes actions that can be passed by the developer to do execute some code at a specific time
        void InvokeCodeEvents(ActionData[] actions, float normalizedTimer)
        {
            foreach (var action in actions)
            {
                if (!action.actionInvoked && (normalizedTimer >= action.normalizeTime))
                {
                    action.action?.Invoke();
                    action.actionInvoked = true;
                }
            }
        }

        void ApplyAnimationSpeed(AnimGraphClipInfo clipInfo, Playable clipPlayable, float normalizedTimer,
            string timeScaleOwnerId, float currentTimescale,  ref float animationSpeed)
        {
            if (clipInfo.customAnimationSpeed)
            {
                if (clipInfo.useAsGlobalTimeScale && TimeScaleOwner.Key == timeScaleOwnerId)
                {
                    Time.timeScale = currentTimescale * Mathf.Max(clipInfo.speedModifier.GetValue(normalizedTimer), 0.01f);
                    animationSpeed = 1;
                }
                else
                {
                    animationSpeed = Mathf.Max(clipInfo.speedModifier.GetValue(normalizedTimer), 0.01f);
                    if (clipPlayable.IsValid())
                        clipPlayable.SetSpeed(animationSpeed);
                }
            }
        }

        public void SetUpdateMode(UpdateMode updateMode)
        {
            this.updateMode = updateMode;
        }

        AvatarMask GetAvatarMask(Mask mask)
        {
            if (mask == Mask.Null) return null;

            switch (mask)
            {
                case Mask.Arm:
                    return armMask;
                case Mask.UpperBody:
                    return upperBodyMask;
                case Mask.RightHand:
                    return rightHandMask;
                case Mask.LeftHand:
                    return leftHandMask;
                case Mask.UpperBodyWithRoot:
                    return upperBodyWithRoot;
                case Mask.UpperBodyWithoutArm:
                    return upperBodyWithoutArm;
                case Mask.RightArm:
                    return rightArm;
            }
            return null;
        }


        // If we want to play a looping animation, we use loopActionMixer, otherwise we use actionMixer
        Playable GetActionMixer(bool isLoopingAnimation)
        {
            if (isLoopingAnimation)
                return loopActionMixer;
            else
                return actionMixer;
        }

        /// <summary>
        /// Creates a mixer for blending clips.
        /// If there is an AvatarMask, we create an AnimationLayerMixer, otherwise we create an AnimationMixer
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="isAdditiveAnimation"></param>
        /// <returns></returns>
        Playable CreateClipMixer(Mask mask, bool isAdditiveAnimation)
        {
            var avatarMask = GetAvatarMask(mask);

            // An additive animation needs a layer mixer even without an avatar mask. A plain AnimationMixerPlayable has no 
            // additive mode — the clip would crossfade the whole pose underneath it
            if (avatarMask != null || isAdditiveAnimation)
            {
                var layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
                if (isAdditiveAnimation)
                    layerMixer.SetLayerAdditive(1, true);

                if (avatarMask != null)
                    layerMixer.SetLayerMaskFromAvatarMask(1, avatarMask);
                return layerMixer;
            }
            else
            {
                return AnimationMixerPlayable.Create(graph, 2);
            }
        }

        Playable CreateClipPlayable(AnimationClip clip, float animationSpeed)
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetSpeed(animationSpeed);
            return clipPlayable;
        }

        /// <summary>
        /// Connect playable1 to playable2
        /// </summary>
        /// <param name="playable1"></param>
        /// <param name="playable2"></param>
        void ConnectPlayables(Playable playable1, Playable playable2)
        {
            playable2.ConnectInput(0, playable1, 0);   
        }

        /// <summary>
        /// Connects a list of playables to a mixer
        /// </summary>
        /// <param name="mixer"></param>
        /// <param name="playables"></param>
        void ConnectToMixer(Playable mixer, params Playable[] playables)
        {
            for (int i = 0; i < playables.Length; i++)
                mixer.ConnectInput(i, playables[i], 0);
        }

        /// <summary>
        /// Will set the default weights of a mixer. That is first input to 1, and rest to 0
        /// </summary>
        /// <param name="mixer"></param>
        /// <param name="inputCount"></param>
        void SetDefaultMixerWeights(Playable mixer, int inputCount = 2)
        {
            if (!mixer.IsValid()) return;

            mixer.SetInputWeight(0, 1);
            
            for (int i = 1; i < inputCount; i++)
                mixer.SetInputWeight(i, 0);
        }


        public void StopLoopingAnimations(bool removeAll)
        {
            if (removeAll)
            {
                StartCoroutine(ConnectPlayablesSmoothly(loopActionMixer, locomotionOverrideMixer));
            }
            else
            {
                StartCoroutine(LoopingAnimationTransitionback());
            }
        }

        #region Locomotion Override

        public void OverrideLocomotion2D(LocomotionClipSet2D clipSet, float transitionDuration = 0.25f)
        {
            if (isLocomotionOverrideActive)
                DestroyLocomotionOverridePlayables();

            locomotionBlendMixer = Build2DMixer(clipSet.clips, out clipPositions2D, out clipWeights2D, out idleClipIndex);

            Playable outputPlayable = locomotionBlendMixer;

            if (clipSet.HasCrouchSet)
            {
                crouchBlendMixer = Build2DMixer(clipSet.crouchClips, out crouchClipPositions2D, out crouchClipWeights2D, out crouchIdleClipIndex);

                locomotionTypeMixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(locomotionBlendMixer, 0, locomotionTypeMixer, 0);
                graph.Connect(crouchBlendMixer, 0, locomotionTypeMixer, 1);
                locomotionTypeMixer.SetInputWeight(0, 1f);
                locomotionTypeMixer.SetInputWeight(1, 0f);

                hasLocomotionCrouchLayer = true;
                outputPlayable = locomotionTypeMixer;
            }

            ResetTransitionPose();

            graph.Connect(outputPlayable, 0, locomotionOverrideMixer, 1);
            isLocomotionOverrideActive = true;
            is2DOverride = true;
            hasWalkClip = false;

            if (locomotionOverrideTransition != null)
                StopCoroutine(locomotionOverrideTransition);
            locomotionOverrideTransition = StartCoroutine(
                TransitionLocomotionOverride(locomotionOverrideMixer.GetInputWeight(1), 1f, transitionDuration));
        }

        AnimationMixerPlayable Build2DMixer(LocomotionClipSet2D.ClipEntry[] clips, out Vector2[] positions, out float[] weights, out int idleIndex)
        {
            int count = clips.Length;
            var mixer = AnimationMixerPlayable.Create(graph, count);

            positions = new Vector2[count];
            weights = new float[count];
            idleIndex = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var entry = clips[i];
                var clipPlayable = AnimationClipPlayable.Create(graph, entry.clip);
                graph.Connect(clipPlayable, 0, mixer, i);
                positions[i] = entry.position;

                float dist = entry.position.sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    idleIndex = i;
                }
            }

            // Freeze very short idle clips (< 5 frames) so single-frame poses hold correctly in the blend
            var idleClip = clips[idleIndex].clip;
            if (idleClip != null && idleClip.length * idleClip.frameRate < 5f)
            {
                var idlePlayable = (AnimationClipPlayable)mixer.GetInput(idleIndex);
                idlePlayable.SetSpeed(0);
            }

            for (int i = 0; i < count; i++)
                mixer.SetInputWeight(i, i == idleIndex ? 1f : 0f);

            return mixer;
        }

        public void OverrideLocomotion(AnimationClip idle, AnimationClip walk, AnimationClip run,
            float transitionDuration = 0.25f)
        {
            if (isLocomotionOverrideActive)
                DestroyLocomotionOverridePlayables();

            is2DOverride = false;
            hasWalkClip = walk != null;
            int clipCount = hasWalkClip ? 3 : 2;
            locomotionBlendMixer = AnimationMixerPlayable.Create(graph, clipCount);

            var idlePlayable = AnimationClipPlayable.Create(graph, idle);
            graph.Connect(idlePlayable, 0, locomotionBlendMixer, 0);

            if (hasWalkClip)
            {
                var walkPlayable = AnimationClipPlayable.Create(graph, walk);
                graph.Connect(walkPlayable, 0, locomotionBlendMixer, 1);
                var runPlayable = AnimationClipPlayable.Create(graph, run);
                graph.Connect(runPlayable, 0, locomotionBlendMixer, 2);
            }
            else
            {
                var runPlayable = AnimationClipPlayable.Create(graph, run);
                graph.Connect(runPlayable, 0, locomotionBlendMixer, 1);
            }

            // Set initial blend weights (idle full)
            locomotionBlendMixer.SetInputWeight(0, 1);
            for (int i = 1; i < clipCount; i++)
                locomotionBlendMixer.SetInputWeight(i, 0);

            graph.Connect(locomotionBlendMixer, 0, locomotionOverrideMixer, 1);
            isLocomotionOverrideActive = true;

            if (locomotionOverrideTransition != null)
                StopCoroutine(locomotionOverrideTransition);
            locomotionOverrideTransition = StartCoroutine(
                TransitionLocomotionOverride(locomotionOverrideMixer.GetInputWeight(1), 1f, transitionDuration));
        }

        public void ClearLocomotionOverride(float transitionDuration = 0.25f)
        {
            if (!isLocomotionOverrideActive) return;

            if (locomotionOverrideTransition != null)
                StopCoroutine(locomotionOverrideTransition);
            locomotionOverrideTransition = StartCoroutine(TransitionLocomotionOverrideThenDestroy(transitionDuration));
        }

        IEnumerator TransitionLocomotionOverride(float fromOverrideWeight, float toOverrideWeight, float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                float t = timer / duration;
                float w = Mathf.Lerp(fromOverrideWeight, toOverrideWeight, t);
                locomotionOverrideMixer.SetInputWeight(0, 1f - w);
                locomotionOverrideMixer.SetInputWeight(1, w);
                timer += DeltaTime;
                yield return null;
            }
            locomotionOverrideMixer.SetInputWeight(0, 1f - toOverrideWeight);
            locomotionOverrideMixer.SetInputWeight(1, toOverrideWeight);
            locomotionOverrideTransition = null;
        }

        IEnumerator TransitionLocomotionOverrideThenDestroy(float duration)
        {
            // Fade out from wherever the blend currently sits. Clearing while an unfinished fade-in
            // is still running would otherwise snap the character to the full override pose first.
            float current = locomotionOverrideMixer.GetInputWeight(1);
            yield return TransitionLocomotionOverride(current, 0f, duration * current);
            DestroyLocomotionOverridePlayables();
            isLocomotionOverrideActive = false;
            locomotionOverrideTransition = null;
        }

        void UpdateLocomotionOverride()
        {
            if (!isLocomotionOverrideActive) return;

            if (is2DOverride) { UpdateLocomotionOverride2D(); return; }

            float moveAmount = animator.GetFloat(AnimatorParameters.moveAmount);

            if (hasWalkClip)
            {
                // 3-clip: idle(0) → walk(0.5) → run(1.0)
                float idleW, walkW, runW;
                if (moveAmount <= 0.5f)
                {
                    float t = moveAmount / 0.5f;
                    idleW = 1f - t;
                    walkW = t;
                    runW = 0f;
                }
                else
                {
                    float t = (moveAmount - 0.5f) / 0.5f;
                    idleW = 0f;
                    walkW = 1f - t;
                    runW = t;
                }
                locomotionBlendMixer.SetInputWeight(0, idleW);
                locomotionBlendMixer.SetInputWeight(1, walkW);
                locomotionBlendMixer.SetInputWeight(2, runW);
            }
            else
            {
                // 2-clip: idle(0) → run(1.0)
                locomotionBlendMixer.SetInputWeight(0, 1f - moveAmount);
                locomotionBlendMixer.SetInputWeight(1, moveAmount);
            }
        }

        void UpdateLocomotionOverride2D()
        {
            float move = animator.GetFloat(AnimatorParameters.moveAmount);
            float strafe = animator.GetFloat(AnimatorParameters.strafeAmount);

            Update2DMixerWeights(locomotionBlendMixer, clipPositions2D, clipWeights2D, idleClipIndex, move, strafe);

            if (hasLocomotionCrouchLayer)
            {
                Update2DMixerWeights(crouchBlendMixer, crouchClipPositions2D, crouchClipWeights2D, crouchIdleClipIndex, move, strafe);

                float crouchBlend = Mathf.Clamp01(animator.GetFloat(AnimatorParameters.idleType) / 0.5f);
                locomotionTypeMixer.SetInputWeight(0, 1f - crouchBlend);
                locomotionTypeMixer.SetInputWeight(1, crouchBlend);
            }
        }

        void Update2DMixerWeights(AnimationMixerPlayable mixer, Vector2[] positions, float[] weights, int idleIdx, float move, float strafe)
        {
            int count = positions.Length;
            float magnitude = Mathf.Sqrt(move * move + strafe * strafe);

            if (magnitude < 0.01f)
            {
                for (int i = 0; i < count; i++)
                    mixer.SetInputWeight(i, i == idleIdx ? 1f : 0f);
                return;
            }

            float inputDirX = strafe / magnitude;
            float inputDirY = move / magnitude;

            // Compute raw directional weight and clip magnitude for each non-idle clip
            float innerRadius = 0f;  // nearest clip ring in this direction
            float outerRadius = 0f;  // next clip ring outward
            bool hasInner = false;

            for (int i = 0; i < count; i++)
            {
                if (i == idleIdx) { weights[i] = 0f; continue; }

                var pos = positions[i];
                float clipMag = Mathf.Sqrt(pos.x * pos.x + pos.y * pos.y);
                if (clipMag < 0.001f) { weights[i] = 0f; continue; }

                float dot = inputDirX * (pos.x / clipMag) + inputDirY * (pos.y / clipMag);
                float w = Mathf.Max(0f, dot);
                w *= w;
                weights[i] = w;

                // Track inner/outer radius rings from clips that contribute in this direction
                if (w > 0.001f)
                {
                    if (!hasInner || clipMag < innerRadius)
                    {
                        outerRadius = hasInner ? Mathf.Max(innerRadius, clipMag) : clipMag;
                        innerRadius = hasInner ? Mathf.Min(innerRadius, clipMag) : clipMag;
                        hasInner = true;
                    }
                    else if (clipMag > outerRadius)
                    {
                        outerRadius = clipMag;
                    }
                }
            }

            // Two-ring blend: idle↔inner when magnitude < innerRadius,
            // inner↔outer when magnitude is between innerRadius and outerRadius
            if (magnitude <= innerRadius)
            {
                // Blend idle ↔ inner ring clips
                float t = innerRadius > 0.001f ? magnitude / innerRadius : 1f;
                float innerSum = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (i == idleIdx) continue;
                    float clipMag = positions[i].magnitude;
                    // Only keep clips on the inner ring
                    if (clipMag > innerRadius + 0.01f) weights[i] = 0f;
                    innerSum += weights[i];
                }
                if (innerSum > 0f)
                {
                    float scale = t / innerSum;
                    for (int i = 0; i < count; i++)
                        if (i != idleIdx) weights[i] *= scale;
                }
                weights[idleIdx] = 1f - t;
            }
            else if (outerRadius > innerRadius + 0.01f)
            {
                // Blend inner ring ↔ outer ring clips
                float t = (magnitude - innerRadius) / (outerRadius - innerRadius);
                t = Mathf.Clamp01(t);
                float innerSum = 0f, outerSum = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (i == idleIdx) continue;
                    float clipMag = positions[i].magnitude;
                    bool isInner = clipMag < (innerRadius + outerRadius) * 0.5f;
                    if (isInner) innerSum += weights[i];
                    else outerSum += weights[i];
                }
                for (int i = 0; i < count; i++)
                {
                    if (i == idleIdx) continue;
                    float clipMag = positions[i].magnitude;
                    bool isInner = clipMag < (innerRadius + outerRadius) * 0.5f;
                    if (isInner)
                        weights[i] = innerSum > 0f ? weights[i] / innerSum * (1f - t) : 0f;
                    else
                        weights[i] = outerSum > 0f ? weights[i] / outerSum * t : 0f;
                }
                weights[idleIdx] = 0f;
            }
            else
            {
                // Single ring or at/past outer — full motion, no idle
                float sum = 0f;
                for (int i = 0; i < count; i++)
                    if (i != idleIdx) sum += weights[i];
                if (sum > 0f)
                {
                    float scale = 1f / sum;
                    for (int i = 0; i < count; i++)
                        if (i != idleIdx) weights[i] *= scale;
                }
                weights[idleIdx] = 0f;
            }

            for (int i = 0; i < count; i++)
                mixer.SetInputWeight(i, weights[i]);
        }

        void DestroyLocomotionOverridePlayables()
        {
            if (!locomotionBlendMixer.IsValid()) return;

            locomotionOverrideMixer.DisconnectInput(1);

            if (hasLocomotionCrouchLayer)
            {
                if (crouchBlendMixer.IsValid())
                {
                    DestroyMixerChildren(crouchBlendMixer);
                    crouchBlendMixer.Destroy();
                }
                if (locomotionTypeMixer.IsValid())
                    locomotionTypeMixer.Destroy();

                crouchClipPositions2D = null;
                crouchClipWeights2D = null;
                hasLocomotionCrouchLayer = false;
            }

            DestroyMixerChildren(locomotionBlendMixer);
            locomotionBlendMixer.Destroy();

            is2DOverride = false;
            clipPositions2D = null;
            clipWeights2D = null;
        }

        void DestroyMixerChildren(AnimationMixerPlayable mixer)
        {
            int inputCount = mixer.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                var input = mixer.GetInput(i);
                if (input.IsValid())
                    input.Destroy();
            }
        }

        #endregion


        public void ResetAnimationGraph()
        {
            isLocomotionOverrideActive = false;
            if (locomotionOverrideTransition != null)
            {
                StopCoroutine(locomotionOverrideTransition);
                locomotionOverrideTransition = null;
            }
            CreateGraph();
        }

        public List<TransitionData> transitionDatas = new List<TransitionData>();
        public struct TransitionData
        {
            public int currentShortNameHash;
            public float currentNormalizedTime;
            public int NextShortNameHash;
            public float NextNormalizedTime;
            public float tranistionDuration;
            public float normalizedTransition;
            public DurationUnit durationUnit;
        }


        void ResetTransitionPose()
        {
            SaveTransitionData();
            if (transitionDatas.Count < 1) return;

            LastRebakeFrame = Time.frameCount;

            animator.Update(0);

            var oldPos = transform.position;
            var oldRot = transform.rotation;
            graph.Evaluate();
            animator.CrossFade(transitionDatas.First().currentShortNameHash, 0, 0, transitionDatas.First().currentNormalizedTime);
            animator.Update(0);
            graph.Evaluate();

            foreach (var data in transitionDatas)
            {
                animator.CrossFade(data.NextShortNameHash, data.tranistionDuration, 0, data.NextNormalizedTime, data.normalizedTransition);
                animator.Update(0);
                graph.Evaluate();
            }
            transform.position = oldPos;
            transform.rotation = oldRot;
            transitionDatas.Clear();
        }

        void SaveTransitionData()
        {
            if (animator.IsInTransition(0))
            {
                var newTansition = new TransitionData()
                {
                    currentShortNameHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    NextShortNameHash = animator.GetNextAnimatorStateInfo(0).shortNameHash,
                    currentNormalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    NextNormalizedTime = animator.GetNextAnimatorStateInfo(0).normalizedTime,
                    tranistionDuration = animator.GetAnimatorTransitionInfo(0).duration,
                    normalizedTransition = animator.GetAnimatorTransitionInfo(0).normalizedTime,
                    durationUnit = animator.GetAnimatorTransitionInfo(0).durationUnit
                };
                if (transitionDatas.Count == 0 || (transitionDatas.Last().currentShortNameHash != newTansition.currentShortNameHash || transitionDatas.Last().NextShortNameHash != newTansition.NextShortNameHash))
                    transitionDatas.Add(newTansition);
                else
                {
                    var last = transitionDatas.Last();
                    last.currentNormalizedTime = newTansition.currentNormalizedTime;
                    last.NextNormalizedTime = newTansition.NextNormalizedTime;
                    last.tranistionDuration = newTansition.tranistionDuration;
                    last.normalizedTransition = newTansition.normalizedTransition;
                    last.durationUnit = newTansition.durationUnit;
                    transitionDatas[transitionDatas.Count - 1] = last;
                }
            }
            else if (transitionDatas.Count > 0)
            {
                transitionDatas.Clear();
            }
        }


        #endregion
    }

    [Serializable]
    public class AnimParameters
    {
        public bool isLoopingAnimation = false;
        public bool transitionBack = true;

        public Mask mask = Mask.Null;
        public bool isAdditiveLayerAnimation = false;

        public float transitionIn = 0.2f;
        public float transitionOut = 0.2f;

        public float animationSpeed = 1f;

        /// <summary>
        /// First float - NormalizedTimer
        /// Second Float - Timer
        /// </summary>
        public Action<float, float> onAnimationUpdate = null;
        public Action OnComplete = null;

        public bool overridableByAnimator = false;
    }

    public class ActionData
    {
        public float normalizeTime = 1;
        public Action action;
        public bool actionInvoked = false;
    }
}