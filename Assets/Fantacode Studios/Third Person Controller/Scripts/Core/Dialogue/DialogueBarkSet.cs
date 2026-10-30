using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    [CreateAssetMenu(menuName = "Dialogue/Bark Set")]
    public class DialogueBarkSet : ScriptableObject
    {
        [SerializeField] List<DialogueBark> barks = new List<DialogueBark>();

        static readonly string[] emptyTags = Array.Empty<string>();

        public DialogueAudio Pick(IList<string> tags)
        {
            int bestTagCount = -1;
            int bestCount = 0;       // number of barks tied at bestTagCount
            DialogueBark chosen = null;

            for (int i = 0; i < barks.Count; i++)
            {
                var bark = barks[i];
                if (!bark.IsSubsetOf(tags)) continue;

                int tc = bark.TagCount;
                if (tc > bestTagCount)
                {
                    bestTagCount = tc;
                    chosen = bark;
                    bestCount = 1;
                }
                else if (tc == bestTagCount)
                {
                    // Reservoir sampling — equal probability for all tied barks
                    bestCount++;
                    if (UnityEngine.Random.Range(0, bestCount) == 0)
                        chosen = bark;
                }
            }

            return chosen?.PickRandomClip();
        }

        public DialogueAudio Pick()
        {
            return Pick(emptyTags);
        }
    }
}
