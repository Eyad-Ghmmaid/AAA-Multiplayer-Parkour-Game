using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    [System.Serializable]
    public class DialogueBark
    {
        [SerializeField, BarkTag] List<string> tags = new List<string>();
        [SerializeField] List<DialogueAudio> audioClips = new List<DialogueAudio>();

        public int TagCount => tags.Count;

        public bool IsSubsetOf(IList<string> queryTags)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < queryTags.Count; j++)
                {
                    if (tags[i] == queryTags[j])
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        public DialogueAudio PickRandomClip()
        {
            if (audioClips.Count == 0) return null;
            return audioClips[Random.Range(0, audioClips.Count)];
        }
    }
}
