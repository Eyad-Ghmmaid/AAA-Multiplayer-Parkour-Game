using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{

    public interface ITargetValidator
    {
        bool IsTargetValid(Transform target);
    }
}
