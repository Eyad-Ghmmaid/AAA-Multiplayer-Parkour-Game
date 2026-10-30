using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    [CreateAssetMenu(menuName = "Dialogue/Bark Tags")]
    public class DialogueBarkTags : ScriptableObject
    {
        [SerializeField] List<string> tags = new List<string>();
        public List<string> Tags => tags;
    }
}
