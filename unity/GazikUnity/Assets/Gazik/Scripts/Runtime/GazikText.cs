using UnityEngine.UI;

namespace Gazik
{
    // InputField keeps raw UTF-16 text so its caret and character limits stay correct.
    public sealed class GazikText : Text
    {
        public bool EncodeEmoji=true;
        public override string text
        {
            get=>base.text;
            set=>base.text=EncodeEmoji?EmojiGlyphs.Render(value):value;
        }
    }
}
