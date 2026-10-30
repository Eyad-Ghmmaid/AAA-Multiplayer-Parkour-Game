#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    // Implemented by modules that need to perform additional configuration after the
    // FSSystemsSetupEditorWindow finishes building an AI character. The window discovers
    // implementations via TypeCache and invokes them, so modules outside Third Person
    // Controller can hook in without this assembly referencing them.
    public interface IAICharacterSetupHandler
    {
        void OnAICharacterSetup(GameObject aiObject, List<string> selectedSystemNames);
    }
}
#endif
