using UnityEngine;

namespace FS_Core 
{
    public interface ITargetable
    {
        bool IsUnreachableByGround { get; }
    }
}
