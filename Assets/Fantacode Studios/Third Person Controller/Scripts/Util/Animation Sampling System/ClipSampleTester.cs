using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    [CreateAssetMenu(menuName = "Clip Sampler/Tester")]
    public class ClipSampleTester : ScriptableObject
    {
        [SerializeField] GameObject prefab;
        [SerializeField] AnimationClip clip;
        [SerializeField] HumanBodyBones bone;

        [SerializeField]
        ClipSampleResult clipSampleResult;

        [ContextMenu("Sample Clip")]
        void TestSample()
        {
            var result = ClipSampler.Sample(prefab, clip, bone, 0.1f);
            Debug.Log($"Sampled {result.frames.Count} frames from clip '{clip.name}' for bone '{bone}'");

            clipSampleResult = result;
        }
    }
}
