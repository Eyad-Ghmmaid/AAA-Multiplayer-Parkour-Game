#if CMPSETUP_COMPLETE
using AvocadoShark;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MenuCanvas : MonoBehaviour
{
    [SerializeField] private GameObject RoomCreationPanel;
    [SerializeField] private Toggle passwordToggle;
    [SerializeField] private Slider slider;
    [SerializeField] private Button refreshRooms;
    [SerializeField] private TextMeshProUGUI roomCountText, nameInputFieldLimitText, passwordInputFieldLimitText;
    public bool IsPasswordEnabled { private set; get; } = false;

    [SerializeField] private TMP_InputField nameInputField, passwordInputField;
    private TMP_Dropdown mapDropdown;

    internal void ConfigureMapSelection(TMP_Dropdown template)
    {
        if (RoomCreationPanel == null || template == null || template.template == null ||
            template.captionText == null || mapDropdown != null)
            return;

        var panel = RoomCreationPanel.transform;
        mapDropdown = Instantiate(template, panel, false);
        mapDropdown.name = "MapDropdown";
        mapDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        var regionIcon = mapDropdown.transform.Find("Image");
        if (regionIcon != null)
            regionIcon.gameObject.SetActive(false);

        var dropdownRect = (RectTransform)mapDropdown.transform;
        dropdownRect.anchorMin = dropdownRect.anchorMax = new Vector2(1f, 0.5f);
        dropdownRect.pivot = new Vector2(1f, 0.5f);
        dropdownRect.anchoredPosition = new Vector2(-38f, -175f);
        dropdownRect.sizeDelta = new Vector2(250f, 56f);

        var list = mapDropdown.template;
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.right;
        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = new Vector2(0f, -4f);

        var caption = mapDropdown.captionText;
        var captionRect = caption.rectTransform;
        captionRect.offsetMin = new Vector2(14f, 0f);
        captionRect.offsetMax = new Vector2(-42f, 0f);
        caption.fontSizeMax = 24f;
        caption.fontSizeMin = 16f;
        caption.enableAutoSizing = true;
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.richText = false;
        if (mapDropdown.itemText != null)
        {
            mapDropdown.itemText.fontSizeMax = 22f;
            mapDropdown.itemText.fontSizeMin = 16f;
            mapDropdown.itemText.enableAutoSizing = true;
            mapDropdown.itemText.richText = false;
        }

        var label = Instantiate(caption, panel, false);
        label.name = "MapLabel";
        label.text = "Map";
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.fontSize = 20f;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = dropdownRect.anchorMin;
        labelRect.pivot = dropdownRect.pivot;
        labelRect.anchoredPosition = new Vector2(-38f, -133f);
        labelRect.sizeDelta = new Vector2(250f, 24f);

        mapDropdown.ClearOptions();
        var names = RaceMapCatalog.Paths.Select(RaceMapCatalog.GetName).ToList();
        if (names.Count == 0)
            names.Add("No maps available");
        mapDropdown.AddOptions(names);
        mapDropdown.SetValueWithoutNotify(0);
        mapDropdown.RefreshShownValue();
        mapDropdown.interactable = RaceMapCatalog.Paths.Count > 1;
    }

    internal string GetSelectedMapPath()
    {
        int index = mapDropdown != null ? mapDropdown.value : 0;
        return index >= 0 && index < RaceMapCatalog.Paths.Count ? RaceMapCatalog.Paths[index] : null;
    }

    private void Awake()
    {
        slider.onValueChanged.AddListener(OnSliderValueChange);
        passwordInputField.interactable = IsPasswordEnabled;
        passwordToggle.isOn = IsPasswordEnabled;
        passwordToggle.onValueChanged.AddListener(TogglePassword);
        nameInputField.onValueChanged.AddListener(NameInputUpdate);
        passwordInputField.onValueChanged.AddListener(PasswordInputUpdate);
    }

    private void TogglePassword(bool value)
    {
        IsPasswordEnabled = value;
        passwordInputField.interactable = value;
    }

    public void RefreshRooms()
    {
        FusionConnection.Instance.RefreshRoomList();
    }

    public string GetRoomName()
    {
        return nameInputField.text;
    }

    public string GetPassword()
    {
        return passwordInputField.text;
    }

    public int GetMaxPlayers()
    {
        return (int)slider.value;
    }

    private void OnSliderValueChange(float value)
    {
        roomCountText.text = Mathf.RoundToInt(value).ToString();
    }

    public void PasswordInputUpdate(string text)
    {
        passwordInputFieldLimitText.text = text.Count() == passwordInputField.characterLimit
            ? $"<color=#D96222>{text.Count()}/{passwordInputField.characterLimit}</color>"
            : $"{text.Count()}/{passwordInputField.characterLimit}";
    }

    public void NameInputUpdate(string text)
    {
        nameInputFieldLimitText.text = text.Count() == nameInputField.characterLimit
            ? $"<color=#D96222>{text.Count()}/{nameInputField.characterLimit}</color>"
            : $"{text.Count()}/{nameInputField.characterLimit}";
    }
}
#endif
