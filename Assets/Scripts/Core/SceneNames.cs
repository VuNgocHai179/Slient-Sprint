namespace SilentSprint.Core
{
    /// <summary>
    /// Tên các scene của game, để chỗ nối scene không phải viết tay chuỗi mỗi nơi một kiểu.
    ///
    /// Tên phải khớp đúng tên file scene trong Assets/Scenes (và scene đó phải nằm trong
    /// Build Settings theo đúng thứ tự MainMenu, CutScene1, CutScene2, Gameplay).
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Bảng chính: Play Game, Continue, Settings, Quit.</summary>
        public const string MainMenu = "MainMenu";

        /// <summary>Cảnh phim mở đầu (Timeline_Intro — Cutscene_Bar_Intro).</summary>
        public const string CutScene1 = "CutScene1";

        /// <summary>Cảnh phim thứ hai (Timeline_Encounter — EncounterCutscene).</summary>
        public const string CutScene2 = "CutScene2";

        /// <summary>Màn chơi chính.</summary>
        public const string Gameplay = "Gameplay";
    }
}
