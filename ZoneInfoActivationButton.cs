using ColossalFramework;
using ColossalFramework.UI;
using System;
using UnityEngine;

namespace ZoneInfo
{
    /// <summary>
    /// the button to activate the zone info panel
    /// </summary>
    public class ZoneInfoActivationButton : UIButton
    {
        // default button position
        public const float DefaultPositionX = 50f;
        public const float DefaultPositionY = 50f;

        // atlas and sprite names for button imagery
        public const string ActivationButtonAtlas   = "ZoneInfoActivationButtonAtlas";
        public const string ForegroundSprite        = "ZoneInfoForeground";
        public const string BackgroundSpriteNormal  = "ZoneInfoBackgroundNormal";
        public const string BackgroundSpriteHovered = "ZoneInfoBackgroundHovered";
        public const string BackgroundSpriteFocused = "ZoneInfoBackgroundFocused";

        /// <summary>
        /// Start is called after the button is created
        /// set up the button
        /// </summary>
        public override void Start()
        {
            // do base processing
            base.Start();

            try
            {
                // set properties
                name = "ZoneInfoActivationButton";
                opacity = 1f;
                size = new Vector2(46f, 46f);
                isVisible = true;
                atlas = TextureUtil.GenerateLinearAtlas(ActivationButtonAtlas, TextureUtil.ActivationButtonTexture2D, 4,
                    new string[] { ForegroundSprite, BackgroundSpriteNormal, BackgroundSpriteHovered, BackgroundSpriteFocused });
                normalFgSprite = ForegroundSprite;
                SetBackgroundImages(false);

                // attach drag handle
                UIDragHandle dragHandle = AddUIComponent<UIDragHandle>();
                if (dragHandle == null)
                {
                    Debug.LogError($"Unable to create drag handle on [{name}].");
                    return;
                }
                dragHandle.name = "DragHandle";
                dragHandle.relativePosition = Vector3.zero;
                dragHandle.size = size;
                dragHandle.tooltip = "Zone Info";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// adjust button background images based on panel visibility
        /// </summary>
        public void SetBackgroundImages(bool panelVisible)
        {
            normalBgSprite = focusedBgSprite = pressedBgSprite = (panelVisible ? BackgroundSpriteFocused : BackgroundSpriteNormal);
            hoveredBgSprite = (panelVisible ? BackgroundSpriteFocused : BackgroundSpriteHovered);
        }
    }
}
