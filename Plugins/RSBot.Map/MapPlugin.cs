using RSBot.Core;
using RSBot.Core.Plugins;

namespace RSBot.Map
{
    public class MapPlugin : IPlugin
    {
        public string InternalName => "RSBot.Map";
        public static MapPlugin Instance { get; private set; }
        public MapManager Manager { get; private set; }

        public void Initialize()
        {
            // Если плагин отключён в глобальном конфиге, не инициализируем
            if (!GlobalConfig.IsPluginEnabled("RSBot.Map", true))
                return;

            Instance = this;
            Manager = new MapManager();
        }

        public void OnLoadCharacter()
        {
            if (!GlobalConfig.IsPluginEnabled("RSBot.Map", true))
                return;

            Views.View.Instance?.InitUniqueObjects();
        }
    }
}
