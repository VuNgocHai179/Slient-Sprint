using System.Collections.Generic;
using SilentSprint.Dialogues;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Nội dung hội thoại của màn chơi. Tách khỏi hệ thống hội thoại (SilentSprint.Dialogues)
    /// để phần kỹ thuật không phải biết gì về mini game.
    ///
    /// Muốn sửa lời thoại thì sửa ở đây; đổi cấu trúc hội thoại thì sửa ở DialogueLine/DialogueManager.
    ///
    /// Quy ước trình bày: KHÔNG viết hoa giữa dòng để nhấn mạnh (kiểu "Nút ĐÈN") — nhìn như lỗi
    /// chính tả. Chỗ cần nhấn mạnh thì bọc bằng màu qua Highlight/HighlightItem/HighlightDanger,
    /// TextMeshPro của DialogBox đã bật rich text.
    /// </summary>
    public static class GameplayDialogues
    {
        /// <summary>Người dẫn chuyện của đoạn hướng dẫn mở đầu.</summary>
        public const string Narrator = "Người bí ẩn";

        /// <summary>Giọng nói phát ra từ các tượng đá thiên thần sa ngã.</summary>
        public const string FallenAngel = "Thiên thần sa ngã";

        /// <summary>Chỉ số nút "đồng ý vào chơi" trong đoạn hội thoại ở tượng đá.</summary>
        public const int ChoiceEnterGame = 0;

        private const string AccentColor = "#FFD24A";   // hành động, nút bấm
        private const string ItemColor = "#8FE3FF";     // vật phẩm, tượng đá
        private const string DangerColor = "#FF6B6B";   // mối nguy hiểm

        private static string Highlight(string text) => $"<color={AccentColor}>{text}</color>";
        private static string HighlightItem(string text) => $"<color={ItemColor}>{text}</color>";
        private static string HighlightDanger(string text) => $"<color={DangerColor}>{text}</color>";

        /// <summary>Đoạn hướng dẫn cách chơi, phát ngay khi vào màn.</summary>
        public static IList<DialogueLine> Intro()
        {
            return new List<DialogueLine>
            {
                new DialogueLine(Narrator, "Ngươi tỉnh dậy trong một nơi không có lấy một tia sáng."),
                new DialogueLine(Narrator, $"Giữ và kéo {Highlight("cần gạt ở góc trái màn hình")} để di chuyển."),
                new DialogueLine(Narrator, $"Nút {Highlight("đèn")} để bật đèn pin soi đường. Nút {Highlight("chạy")} giúp ngươi chạy nhanh hơn, nhưng sẽ bào mòn thể lực."),
                new DialogueLine(Narrator, "Thanh thể lực cạn thì phải đi bộ một lúc mới hồi lại — hãy giữ sức cho lúc cần."),
                new DialogueLine(Narrator, $"Thu thập đủ những {HighlightItem("mảnh đá tự do")} rải rác trong khu này để mở đường thoát."),
                new DialogueLine(Narrator, $"Trên đường đi ngươi sẽ gặp những {HighlightItem("tượng đá thiên thần sa ngã")}. Lại gần chúng để nghe điều chúng muốn."),
                new DialogueLine(Narrator, $"Và nhớ lấy: đừng để {HighlightDanger("thứ đang lang thang trong bóng tối")} tìm thấy ngươi."),
                new DialogueLine(Narrator, "Chúc may mắn.")
            };
        }

        /// <summary>Lời cảnh báo của người bí ẩn khi người chơi nhặt được một nửa số mảnh đá, kèm chiếc la bàn.</summary>
        public static IList<DialogueLine> CompassWarning()
        {
            return new List<DialogueLine>
            {
                new DialogueLine(Narrator, $"Từ bây giờ mọi thứ sẽ phải cẩn thận bởi {HighlightDanger("thứ quái quỷ kia")} đã biết bạn đang làm gì, hãy giữ lấy chiếc {HighlightItem("la bàn")} để dành lấy sự sống cho mình.")
            };
        }

        /// <summary>Cuộc nói chuyện ở một tượng đá: dẫn dắt, dạy cách chơi, rồi hỏi vào chơi.</summary>
        public static IList<DialogueLine> PedestalConversation(MinigameKind kind)
        {
            return new List<DialogueLine>
            {
                new DialogueLine(FallenAngel, "Ngươi... lại gần ta làm gì?"),
                new DialogueLine(FallenAngel, $"Ta là một trong những {HighlightItem("tượng đá thiên thần sa ngã")}. Muốn lấy sức mạnh của ta, ngươi phải vượt qua thử thách của ta."),
                new DialogueLine(FallenAngel, $"Thử thách lần này là: {Highlight(GetGameTitle(kind))}."),
                new DialogueLine(FallenAngel, GetHowToPlay(kind)),
                new DialogueLine(FallenAngel, $"Sai một bước là ngươi ở lại đây mãi mãi. Ngươi dám thử chứ?", new[]
                {
                    new DialogueChoice("Vào chơi"),
                    new DialogueChoice("Để ta suy nghĩ thêm")
                })
            };
        }

        /// <summary>Tên hiển thị của từng mini game.</summary>
        public static string GetGameTitle(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Cup: return "Đoán cốc";
                case MinigameKind.Memory: return "Ghi nhớ Rune";
                case MinigameKind.Bomb: return "Gỡ bom";
                default: return "Mini game";
            }
        }

        /// <summary>Luật chơi của từng mini game, hiện ngay trong hội thoại trước khi vào.</summary>
        public static string GetHowToPlay(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Cup:
                    return "Ta sẽ giấu viên sỏi dưới một chiếc cốc rồi đảo chúng thật nhanh. Nhìn cho kỹ, rồi chỉ vào chiếc cốc ngươi cho là có sỏi. Ngươi có 5 lượt.";

                case MinigameKind.Memory:
                    return "Các ký tự cổ sẽ lần lượt sáng lên theo một thứ tự. Hãy bấm lại đúng thứ tự đó. Sai một ký tự là coi như xong. Ngươi có 5 lượt.";

                case MinigameKind.Bomb:
                    return "Trước mặt ngươi là 20 nút, một trong số đó sẽ kích nổ quả bom. Ngươi và ta thay phiên nhau bấm. Ai bấm trúng nút nổ thì người đó thua. Ngươi đi trước.";

                default:
                    return "Hãy làm đúng theo yêu cầu mà ta đưa ra.";
            }
        }
    }
}
