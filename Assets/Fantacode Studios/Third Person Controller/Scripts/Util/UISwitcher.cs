using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FS_Util
{
    public class UISwitcher : MonoBehaviour
    {
        [SerializeField] List<UIObject> uiObjects = new List<UIObject>();
        [SerializeField] Button leftButton;
        [SerializeField] Button rightButton;
        [SerializeField] TMP_Text headingText;
        [SerializeField] AudioClip uiChangeClip;

        public event Action<int, int> OnUIChanged;

        List<UIObject> activeUIObjects = new List<UIObject>();

        int currentUIIndex = 0;

        private void Start()
        {
            leftButton.onClick.AddListener(() =>
            {
                currentUIIndex = currentUIIndex > 0? currentUIIndex - 1 : activeUIObjects.Count-1 ;
                SwitchUI();
            });

            rightButton.onClick.AddListener(() =>
            {
                currentUIIndex = (currentUIIndex + 1) % activeUIObjects.Count;
                SwitchUI();
            });
        }

        private void OnEnable()
        {
            if (activeUIObjects.Count == 0)
                activeUIObjects = uiObjects;
            currentUIIndex = 0;

            if (!initialized)
                Init();
        }

        bool initialized = false;
        public void Init(List<string> activeUINames = null, string defaultUIName = null)
        {
            initialized = true;

            if (activeUINames != null && activeUINames.Count > 0)
            {
                activeUIObjects = activeUINames.Select(n => uiObjects.FirstOrDefault(o => o.name == n)).Where(o => o != null).ToList();
            }
            else
            {
                activeUIObjects = uiObjects;
            }

            if (defaultUIName != null)
            {
                var defaultObject = activeUIObjects.FirstOrDefault(o => o.name == defaultUIName);
                if (defaultObject != null)
                {
                    currentUIIndex = activeUIObjects.IndexOf(defaultObject);
                }
            }

            leftButton.gameObject.SetActive(activeUIObjects.Count > 1);
            rightButton.gameObject.SetActive(activeUIObjects.Count > 1);

            for (int i = 0; i < uiObjects.Count; i++)
                uiObjects[i].gameObject.SetActive(false);

            SwitchUI();
        }

        public void SwitchUI()
        {
            if (headingText != null)
                headingText.text = activeUIObjects[currentUIIndex].name;

            for (int i = 0; i < activeUIObjects.Count; i++)
                activeUIObjects[i].gameObject.SetActive(i == currentUIIndex);

            OnUIChanged?.Invoke(currentUIIndex, uiObjects.IndexOf(activeUIObjects[currentUIIndex]));
            FSAudioUtil.PlaySfx(uiChangeClip);
        }

        public int OverallIndex => uiObjects.IndexOf(activeUIObjects[currentUIIndex]);
    }

    [Serializable]
    public class UIObject
    {
        public string name;
        public GameObject gameObject;
    }
}
