using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FS_Util
{
    /// <summary>
    /// Samples an <see cref="AnimationClip"/> to extract root-motion data and a tracked
    /// bone position at uniform time intervals.
    ///
    /// Uses <see cref="PlayableGraph.Evaluate"/> — the only approach that correctly drives
    /// <c>transform.position</c> via root motion in both edit-mode and runtime.
    ///
    /// Usage:
    /// <code>
    ///   // Humanoid rig — track right foot bone
    ///   var result = ClipSampler.Sample(characterPrefab, clip, HumanBodyBones.RightFoot);
    ///
    ///   // Generic rig — track a bone by name
    ///   var result = ClipSampler.Sample(characterPrefab, clip, "Bip001 R Foot");
    ///
    ///   // Query the curves
    ///   Vector3 rootAtHalfway = result.EvaluateRootPosition(clip.length * 0.5f);
    ///   Debug.Log("Total displacement: " + result.TotalDisplacement);
    /// </code>
    /// </summary>
    /// 

    public static class ClipSampler
    {
        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sample a clip using a humanoid <see cref="HumanBodyBones"/> as the tracked target.
        /// </summary>
        /// <param name="characterPrefab">
        /// Prefab to sample on. Must have an <see cref="Animator"/> with a humanoid avatar.
        /// A hidden instance is created and immediately destroyed after sampling.
        /// </param>
        /// <param name="clip">The clip to sample. Must have root motion baked for rootPosition curves to be non-zero.</param>
        /// <param name="targetBone">Humanoid bone whose position will be recorded each interval.</param>
        /// <param name="interval">Time between samples in seconds. Default 0.05 s (20 samples/s).</param>
        public static ClipSampleResult Sample(GameObject characterPrefab, AnimationClip clip,
            HumanBodyBones targetBone = HumanBodyBones.RightFoot, float interval = 0.05f)
        {
            return SampleInternal(
                characterPrefab, clip, interval,
                animator =>
                {
                    var t = animator.GetBoneTransform(targetBone);
                    if (t == null)
                        Debug.LogWarning($"[ClipSampler] Bone {targetBone} not found — is the character humanoid?");
                    return t;
                },
                boneName: targetBone.ToString());
        }

        /// <summary>
        /// Sample a clip using a bone found by Transform name. Suitable for generic rigs
        /// or any custom bone not covered by <see cref="HumanBodyBones"/>.
        /// </summary>
        /// <param name="characterPrefab">Prefab to sample on. Must have an <see cref="Animator"/>.</param>
        /// <param name="clip">The clip to sample.</param>
        /// <param name="boneName">Exact <c>Transform.name</c> to search for in the hierarchy.</param>
        /// <param name="interval">Time between samples in seconds. Default 0.05 s.</param>
        public static ClipSampleResult Sample(GameObject characterPrefab, AnimationClip clip,
            string boneName, float interval = 0.05f)
        {
            return SampleInternal(
                characterPrefab, clip, interval,
                animator =>
                {
                    var t = FindTransformByName(animator.transform, boneName);
                    if (t == null)
                        Debug.LogWarning($"[ClipSampler] Bone '{boneName}' not found in character hierarchy.");
                    return t;
                },
                boneName: boneName);
        }

        // ── Core sampling ─────────────────────────────────────────────────────────

        static ClipSampleResult SampleInternal(GameObject prefab, AnimationClip clip,
            float interval, Func<Animator, Transform> getBone, string boneName)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval), "Must be > 0");

            var result = new ClipSampleResult
            {
                clip = clip,
                targetBoneName = boneName,
                sampleInterval = interval,
                duration = clip.length
            };

            // ── Instantiate a hidden copy of the character ─────────────────────────
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[ClipSampler] Character prefab has no Animator component.");
                UnityEngine.Object.DestroyImmediate(instance);
                return result;
            }

            animator.applyRootMotion = true;
            var boneTransform = getBone(animator);

            PlayableGraph graph = BuildPlayableGraph(clip, animator);

            // Pass 1: root motion via PlayableGraph 
            try
            {
                graph.Evaluate(0f);

                var startPos = instance.transform.position;
                var startRot = instance.transform.rotation;

                result.frames.Add(BuildRootFrame(0f, instance.transform, startPos, startRot));

                float currentTime = 0f;
                while (currentTime < clip.length - 0.0001f)
                {
                    float dt = Mathf.Min(interval, clip.length - currentTime);
                    graph.Evaluate(dt);
                    currentTime = Mathf.Min(currentTime + dt, clip.length);
                    result.frames.Add(BuildRootFrame(currentTime, instance.transform, startPos, startRot));
                }
            }
            finally
            {
                graph.Destroy();
            }

            // Pass 2: bone positions via AnimationClip.SampleAnimation
            try
            {
                for (int i = 0; i < result.frames.Count; i++)
                {
                    clip.SampleAnimation(instance, result.frames[i].time);

                    var frame = result.frames[i];
                    frame.bonePosition = boneTransform != null
                        ? instance.transform.InverseTransformPoint(boneTransform.position)
                        : Vector3.zero;
                    result.frames[i] = frame;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            BuildCurves(result);

            return result;
        }

        private static PlayableGraph BuildPlayableGraph(AnimationClip clip, Animator animator)
        {
            var graph = PlayableGraph.Create($"ClipSampler_{clip.name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            var output = AnimationPlayableOutput.Create(graph, "Output", animator);
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(clipPlayable);
            return graph;
        }

        // ── Frame recording ───────────────────────────────────────────────────────

        // Captures root motion data only. bonePosition is left at zero here and
        // filled in Pass 2 after clip.SampleAnimation updates the bone Transforms.
        static ClipSampleResult.SampledFrame BuildRootFrame(float time, Transform root, Vector3 startPos, 
            Quaternion startRot)
        {
            var invStartRot = Quaternion.Inverse(startRot);

            var localRootPos = invStartRot * (root.position - startPos);

            var rotDelta = invStartRot * root.rotation;
            float yAngle = rotDelta.eulerAngles.y;
            if (yAngle > 180f) yAngle -= 360f;

            return new ClipSampleResult.SampledFrame
            {
                time = time,
                rootPosition = localRootPos,
                rootYAngle = yAngle,
                bonePosition = Vector3.zero
            };
        }

        // ── Curve building ────────────────────────────────────────────────────────

        static void BuildCurves(ClipSampleResult r)
        {
            foreach (var f in r.frames)
            {
                float t = f.time;

                r.rootPosX.AddKey(t, f.rootPosition.x);
                r.rootPosY.AddKey(t, f.rootPosition.y);
                r.rootPosZ.AddKey(t, f.rootPosition.z);

                r.rootYAngleCurve.AddKey(t, f.rootYAngle);

                r.bonePosX.AddKey(t, f.bonePosition.x);
                r.bonePosY.AddKey(t, f.bonePosition.y);
                r.bonePosZ.AddKey(t, f.bonePosition.z);
            }

            // Apply smooth auto-tangents to all curves for clean interpolation
            SmoothCurve(r.rootPosX);
            SmoothCurve(r.rootPosY);
            SmoothCurve(r.rootPosZ);

            SmoothCurve(r.rootYAngleCurve);
            SmoothCurve(r.bonePosX);
            SmoothCurve(r.bonePosY);
            SmoothCurve(r.bonePosZ);
        }

        static void SmoothCurve(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
        }

        // ── Hierarchy search ──────────────────────────────────────────────────────

        static Transform FindTransformByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindTransformByName(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
