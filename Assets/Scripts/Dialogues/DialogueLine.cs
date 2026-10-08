using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentSprint.Dialogues
{
    /// <summary>
    /// Một đáp án người chơi có thể bấm khi câu thoại đang chờ chọn.
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        [Tooltip("Chữ hiện trên nút chọn.")]
        public string label = "";

        [Tooltip("Dòng sẽ hiện tiếp sau khi chọn. -1 = chọn xong thì kết thúc hội thoại.")]
        public int nextLineIndex = -1;

        public DialogueChoice() { }

        public DialogueChoice(string label, int nextLineIndex = -1)
        {
            this.label = label;
            this.nextLineIndex = nextLineIndex;
        }
    }

    /// <summary>
    /// Một câu thoại. Không có đáp án thì người chơi bấm vào màn hình để đi tiếp;
    /// có đáp án thì phải bấm một nút chọn mới đi tiếp được.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Tên người nói. Để trống nếu đây là lời dẫn.")]
        public string speaker = "";

        [TextArea(2, 6)]
        public string content = "";

        [Tooltip("Danh sách đáp án. Để trống nếu chỉ cần bấm để đọc câu tiếp theo.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        public bool HasChoices => choices != null && choices.Count > 0;

        public DialogueLine() { }

        public DialogueLine(string speaker, string content)
        {
            this.speaker = speaker;
            this.content = content;
        }

        public DialogueLine(string speaker, string content, params DialogueChoice[] options)
        {
            this.speaker = speaker;
            this.content = content;
            choices = new List<DialogueChoice>(options);
        }
    }

    /// <summary>Kết quả trả về cho người gọi khi một cuộc hội thoại kết thúc.</summary>
    public struct DialogueResult
    {
        /// <summary>Chỉ số đáp án người chơi chọn. -1 nếu hội thoại chỉ toàn câu bấm để đọc.</summary>
        public int choiceIndex;

        /// <summary>Chữ trên nút vừa chọn. null nếu hội thoại không có đáp án.</summary>
        public string choiceLabel;

        /// <summary>True nếu hội thoại bị đóng giữa chừng thay vì chạy hết.</summary>
        public bool closed;

        public static DialogueResult Finished()
        {
            return new DialogueResult { choiceIndex = -1, choiceLabel = null, closed = false };
        }

        public static DialogueResult Closed()
        {
            return new DialogueResult { choiceIndex = -1, choiceLabel = null, closed = true };
        }
    }
}
