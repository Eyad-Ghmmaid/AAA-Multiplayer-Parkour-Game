#if CMPSETUP_COMPLETE
using TMPro;
using UnityEngine;

public class ChatItem : MonoBehaviour
{
    public void Init(bool isLeft, Chat chat)
    {
        var textComponent = GetComponent<TextMeshProUGUI>();
        var sender = EscapeRichText(chat.Sender.ToString());
        var message = EscapeRichText(chat.Message.ToString());
        textComponent.text = isLeft
            ? $"<uppercase><b>{sender}</b></uppercase>\n{message}"
            : $"<color=orange><uppercase><b>{sender}</b></uppercase></color>\n{message}";
    }

    /// <summary>
    /// Player-controlled text goes into TMP rich-text - escape every '<' so no tag
    /// (e.g. &lt;size=1000&gt; or an unclosed &lt;color&gt;) can affect the chat UI.
    /// </summary>
    public static string EscapeRichText(string s)
    {
        return string.IsNullOrEmpty(s) ? s : s.Replace("<", "<noparse><</noparse>");
    }
}

#endif