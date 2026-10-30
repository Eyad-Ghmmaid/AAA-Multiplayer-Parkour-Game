using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    public abstract class FSInputUIBase : MonoBehaviour
    {
        protected virtual void Awake()
        {
            CacheButtons();
        }

        protected virtual void OnValidate()
        {
            CacheButtons();
        }

        public abstract void CacheButtons();

        protected UIImageInteraction FindButton(params string[] candidateNames)
        {
            var root = transform.root != null ? transform.root : transform;
            return FindBestMatch(root.GetComponentsInChildren<UIImageInteraction>(true), candidateNames);
        }

        protected UIImageInteraction FindButtonInContainer(string containerName, params string[] candidateNames)
        {
            if (string.IsNullOrWhiteSpace(containerName))
                return FindButton(candidateNames);

            var root = transform.root != null ? transform.root : transform;
            var container = root
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => string.Equals(t.name, containerName, StringComparison.OrdinalIgnoreCase));

            if (container == null)
                return null;

            return FindBestMatch(container.GetComponentsInChildren<UIImageInteraction>(true), candidateNames);
        }

        static UIImageInteraction FindBestMatch(IEnumerable<UIImageInteraction> buttons, params string[] candidateNames)
        {
            if (buttons == null || candidateNames == null || candidateNames.Length == 0)
                return null;

            var buttonList = buttons.Where(button => button != null).ToList();
            if (buttonList.Count == 0)
                return null;

            foreach (var candidate in candidateNames)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var exactMatch = buttonList.FirstOrDefault(button => string.Equals(button.name, candidate, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                    return exactMatch;
            }

            foreach (var candidate in candidateNames)
            {
                var normalizedCandidate = NormalizeName(candidate);
                if (string.IsNullOrEmpty(normalizedCandidate))
                    continue;

                var normalizedMatch = buttonList.FirstOrDefault(button => NormalizeName(button.name) == normalizedCandidate);
                if (normalizedMatch != null)
                    return normalizedMatch;
            }

            foreach (var candidate in candidateNames)
            {
                var normalizedCandidate = NormalizeName(candidate);
                if (string.IsNullOrEmpty(normalizedCandidate))
                    continue;

                var partialMatch = buttonList.FirstOrDefault(button => NormalizeName(button.name).Contains(normalizedCandidate));
                if (partialMatch != null)
                    return partialMatch;
            }

            return null;
        }

        static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}