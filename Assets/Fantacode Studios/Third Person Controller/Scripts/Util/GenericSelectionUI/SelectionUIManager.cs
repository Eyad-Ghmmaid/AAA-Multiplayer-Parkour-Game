using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Util
{

    public class SelectionUIManager : MonoBehaviour
    {
        [SerializeField] SelectorBase rootSelector;

        private void Awake()
        {
            if (rootSelector == null)
                rootSelector = GetComponent<SelectorBase>();
        }

        List<SelectorBase> selectorList;

        private void Start()
        {
            if (rootSelector == null) return;

            selectorList = GetConnectedSelectors(rootSelector);

            foreach (var selector in selectorList)
            {
                selector.OnFocusGained += () => RemoveFocusOfConnections(selector);
            }
        }

        List<SelectorBase> GetConnectedSelectors(SelectorBase root)
        {
            var visited = new HashSet<SelectorBase>();
            Traverse(root, visited);
            return visited.ToList();

        }

        void Traverse(SelectorBase selector, HashSet<SelectorBase> visited)
        {
            if (visited.Contains(selector)) return;

            visited.Add(selector);
            foreach (var connection in selector.GetLinkedSelectors())
            {
                Traverse(connection.selector, visited);
            }
        }

        void RemoveFocusOfConnections(SelectorBase newlyFocusedSelector)
        {
            selectorList.Where(s => s.GetIsInFocus() && s != newlyFocusedSelector).ToList().ForEach(s => s.RemoveFocus());
        }

        private void OnGUI()
        {
            //GUILayout.Label("Selected Items - " + string.Join(",", selectorList.Select(s => s.GetSelectedItem())));
            //GUILayout.Label("In Focus  - " + string.Join(",", selectorList.Select(s => s.GetIsInFocus())));
        }
    }
}
