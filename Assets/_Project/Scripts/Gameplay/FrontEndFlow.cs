using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>씬 사이에서 유지되는 최소 프런트엔드 상태.</summary>
    public static class FrontEndFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string HubScene = "Hub";
        public const string DungeonScene = "IsoPrototype";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationSession()
        {
            DungeonSelection.SelectedId = DungeonCatalog.DefaultId;
        }
    }

    /// <summary>허브 던전 선택을 새 게임 생성에 전달한다.</summary>
    public static class DungeonSelection
    {
        public static string SelectedId = DungeonCatalog.DefaultId;
        public static DungeonDefinition Selected => DungeonCatalog.ById(SelectedId);
    }
}
