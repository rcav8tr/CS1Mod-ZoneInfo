using UnityEngine;

namespace ZoneInfo
{
    /// <summary>
    /// define global (i.e. for this mod but not game specific) configuration properties
    /// </summary>
    /// <remarks>convention for the config file name seems to be the mod name + "Config.xml"</remarks>
    [ConfigurationFileName("ZoneInfoConfig.xml")]
    public class ZoneInfoConfiguration
    {
        // it is important to set default config values in case there is no config file

        // button position
        public float ButtonPositionX = ZoneInfoActivationButton.DefaultPositionX;
        public float ButtonPositionY = ZoneInfoActivationButton.DefaultPositionY;

        // panel position
        public float PanelPositionX = ZoneInfoPanel.DefaultPositionX;
        public float PanelPositionY = ZoneInfoPanel.DefaultPositionY;

        /// <summary>
        /// save the button position to the global config file
        /// </summary>
        public static void SaveButtonPosition(Vector3 position)
        {
            ZoneInfoConfiguration config = Configuration<ZoneInfoConfiguration>.Load();
            config.ButtonPositionX = position.x;
            config.ButtonPositionY = position.y;
            Configuration<ZoneInfoConfiguration>.Save();
        }

        /// <summary>
        /// save the panel position to the global config file
        /// </summary>
        public static void SavePanelPosition(Vector3 position)
        {
            ZoneInfoConfiguration config = Configuration<ZoneInfoConfiguration>.Load();
            config.PanelPositionX = position.x;
            config.PanelPositionY = position.y;
            Configuration<ZoneInfoConfiguration>.Save();
        }
    }
}