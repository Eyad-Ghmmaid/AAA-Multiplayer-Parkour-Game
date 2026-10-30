using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace FS_Core
{
    [Serializable]
    public class FSInput
    {
        [SerializeField] KeyCode key = KeyCode.None;
        [SerializeField] string button;
        [SerializeField] UIImageInteraction uiButton;

        public FSInput()
        {
        }

        public FSInput(KeyCode defaultKey, string defaultButton = "")
        {
            key = defaultKey;
            button = defaultButton;
        }

        public KeyCode Key => key;
        public string Button => button;
        public UIImageInteraction UIButton => uiButton;

        public void SetUIButton(UIImageInteraction buttonReference)
        {
            uiButton = buttonReference;
        }

        public bool GetButton()
        {
            if (uiButton != null && uiButton.ButtonHolding)
                return true;
#if ENABLE_LEGACY_INPUT_MANAGER
            return (key != KeyCode.None && Input.GetKey(key)) ||
                (!string.IsNullOrEmpty(button) && Input.GetButton(button));
#else
            return false;
#endif
        }

        public bool GetButtonDown()
        {
            if (uiButton != null && uiButton.ButtonDown)
                return true;
#if ENABLE_LEGACY_INPUT_MANAGER
            return (key != KeyCode.None && Input.GetKeyDown(key)) ||
                (!string.IsNullOrEmpty(button) && Input.GetButtonDown(button));
#else
            return false;
#endif
        }

        public bool GetButtonUp()
        {
            if (uiButton != null && uiButton.ButtonUp)
                return true;
#if ENABLE_LEGACY_INPUT_MANAGER
            return (key != KeyCode.None && Input.GetKeyUp(key)) ||
                (!string.IsNullOrEmpty(button) && Input.GetButtonUp(button));
#else
            return false;
#endif
        }
    }

    public readonly struct FSInputBindingInfo
    {
        public FSInputBindingInfo(string id, string displayName, FSInput input, FieldInfo fieldInfo)
        {
            Id = id;
            DisplayName = displayName;
            Input = input;
            FieldInfo = fieldInfo;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public FSInput Input { get; }
        public FieldInfo FieldInfo { get; }
    }

    public abstract class FSInputManagerBase : MonoBehaviour
    {
#if inputsystem
        protected FSSystemsInputAction input;
#endif

        static readonly Dictionary<Type, List<FieldInfo>> inputFieldCache = new Dictionary<Type, List<FieldInfo>>();

        protected virtual void Awake()
        {
            EnsureEventSystemInputModule();
            CacheUIInputs();
        }

        /// <summary>
        /// Guarantees the scene's EventSystem has an input module that matches the
        /// project's active Input Handling. The bundled player prefabs ship an
        /// EventSystem with NO input module attached, so under "Input System Package
        /// (New)" no pointer/touch events are ever dispatched — the on-screen
        /// joystick, UIImageInteraction buttons and MobileCameraInputController all
        /// go dead in mobile builds. (Keyboard/gamepad still work because those read
        /// the input actions directly, bypassing the EventSystem.)
        ///
        /// Runs from the base Awake so every player is covered (each player has at
        /// least one FSInputManagerBase) without hand-editing each EventSystem.
        /// Idempotent — the GetComponent check makes repeat calls from sibling
        /// managers no-ops.
        /// </summary>
        static void EnsureEventSystemInputModule()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null) return;

            var go = eventSystem.gameObject;

#if ENABLE_INPUT_SYSTEM
            // New input is active. A legacy StandaloneInputModule can't dispatch in
            // this mode, so drop it and ensure the InputSystem UI module is present.
            var legacyModule = go.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) Destroy(legacyModule);

            if (go.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = go.AddComponent<InputSystemUIInputModule>();
                // A module added at runtime has null action references; without this
                // it still won't process pointer/click input.
                module.AssignDefaultActions();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (go.GetComponent<BaseInputModule>() == null)
                go.AddComponent<StandaloneInputModule>();
#endif
        }

        protected virtual void OnValidate()
        {
            CacheUIInputs();
        }

#if inputsystem
        protected virtual void OnEnable()
        {
            input = new FSSystemsInputAction();
            input.Enable();
            CacheUIInputs();
        }

        protected virtual void OnDisable()
        {
            input?.Disable();
        }
#else
        protected virtual void OnEnable()
        {
            CacheUIInputs();
        }
#endif

        public IReadOnlyList<FSInputBindingInfo> GetInputBindings()
        {
            var bindings = new List<FSInputBindingInfo>();
            foreach (var field in GetInputFields(GetType()))
            {
                if (!(field.GetValue(this) is FSInput binding))
                    continue;

                bindings.Add(new FSInputBindingInfo(
                    field.Name,
                    NicifyVariableName(field.Name),
                    binding,
                    field));
            }

            return bindings;
        }

        public bool TryGetInput(string id, out FSInput inputBinding)
        {
            foreach (var binding in GetInputBindings())
            {
                if (string.Equals(binding.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    inputBinding = binding.Input;
                    return true;
                }
            }

            inputBinding = null;
            return false;
        }

        protected bool ReadButton(FSInput binding, Func<bool> inputSystemReader = null)
        {
#if inputsystem
            return (inputSystemReader != null && inputSystemReader()) || (binding != null && binding.GetButton());
#else
            return binding != null && binding.GetButton();
#endif
        }

        protected bool ReadButtonDown(FSInput binding, Func<bool> inputSystemReader = null)
        {
#if inputsystem
            return (inputSystemReader != null && inputSystemReader()) || (binding != null && binding.GetButtonDown());
#else
            return binding != null && binding.GetButtonDown();
#endif
        }

        protected bool ReadButtonUp(FSInput binding, Func<bool> inputSystemReader = null)
        {
#if inputsystem
            return (inputSystemReader != null && inputSystemReader()) || (binding != null && binding.GetButtonUp());
#else
            return binding != null && binding.GetButtonUp();
#endif
        }

        protected void BindUIButton(FSInput binding, params string[] candidateNames)
        {
            if (binding == null)
                return;

            binding.SetUIButton(FindUIButton(candidateNames));
        }

        protected void BindUIButton(FSInput binding, string containerName, params string[] candidateNames)
        {
            if (binding == null)
                return;

            binding.SetUIButton(FindUIButtonInContainer(containerName, candidateNames));
        }

        protected UIImageInteraction FindUIButton(params string[] candidateNames)
        {
            return FindUIButton(GetSearchRoot().GetComponentsInChildren<UIImageInteraction>(true), candidateNames);
        }

        protected UIImageInteraction FindUIButtonInContainer(string containerName, params string[] candidateNames)
        {
            if (string.IsNullOrWhiteSpace(containerName))
                return FindUIButton(candidateNames);

            var container = GetSearchRoot()
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => string.Equals(t.name, containerName, StringComparison.OrdinalIgnoreCase));

            if (container == null)
                return null;

            var buttons = container
                .GetComponentsInChildren<UIImageInteraction>(true)
                .Where(button => string.Equals(GetOwningContainerName(button.transform, container), container.name, StringComparison.OrdinalIgnoreCase));

            return FindUIButton(buttons, candidateNames);
        }

        protected virtual void CacheUIInputs()
        {
        }

        Transform GetSearchRoot()
        {
            return transform.root != null ? transform.root : transform;
        }

        static string GetOwningContainerName(Transform target, Transform fallbackContainer)
        {
            var current = target.parent;
            while (current != null && current != fallbackContainer)
            {
                if (current.name.EndsWith("Mobile Controller", StringComparison.OrdinalIgnoreCase))
                    return current.name;

                current = current.parent;
            }

            return fallbackContainer != null ? fallbackContainer.name : string.Empty;
        }

        static UIImageInteraction FindUIButton(IEnumerable<UIImageInteraction> buttons, params string[] candidateNames)
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

        static string NicifyVariableName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var nicified = System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");
            nicified = nicified.Replace("_", " ").Trim();
            return char.ToUpper(nicified[0]) + nicified.Substring(1);
        }

        static List<FieldInfo> GetInputFields(Type type)
        {
            if (inputFieldCache.TryGetValue(type, out var cachedFields))
                return cachedFields;

            var fields = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(FSInput))
                .OrderBy(field => field.MetadataToken)
                .ToList();

            inputFieldCache[type] = fields;
            return fields;
        }
    }
}