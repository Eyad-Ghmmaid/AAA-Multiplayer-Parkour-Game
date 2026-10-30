#if CMPSETUP_COMPLETE
namespace AvocadoShark
{
    /// <summary>
    /// Escaping for text that came from another player (chat messages, display names).
    /// TextMeshPro parses rich-text tags, so an unescaped name like "&lt;size=500&gt;" or an
    /// unclosed "&lt;color&gt;" would deface the roster, vote panel and info feed for everyone
    /// who sees it. UI character limits are client-side only, so a modified client can put
    /// anything in a networked name.
    /// </summary>
    public static class RichTextSafety
    {
        /// <summary>
        /// Neutralises every '&lt;' so player text can never open a tag. Escaping the angle
        /// bracket alone is sufficient - without it no tag, including &lt;/noparse&gt;, can form.
        /// </summary>
        public static string Escape(string text)
        {
            return string.IsNullOrEmpty(text) ? text : text.Replace("<", "<noparse><</noparse>");
        }
    }
}
#endif
