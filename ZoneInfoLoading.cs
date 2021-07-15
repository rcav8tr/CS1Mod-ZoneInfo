using ColossalFramework.UI;
using ICities;
using UnityEngine;
using System;

namespace ZoneInfo
{
    /// <summary>
    /// handle game loading and unloading
    /// </summary>
    /// <remarks>A new instance of ZoneInfoLoading is NOT created when loading a game from the Pause Menu.</remarks>
    public class ZoneInfoLoading : LoadingExtensionBase
    {
        // the UI elements that get added directly to the main view
        private static ZoneInfoActivationButton _activationButton;
        private static Vector3 _lastButtonPosition = Vector3.zero;
        private static ZoneInfoPanel _infoPanel;

        public override void OnLevelLoaded(LoadMode mode)
        {
            // do base processing
            base.OnLevelLoaded(mode);

            try
            {
                // check for new or loaded game
                if (mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario || mode == LoadMode.LoadGame)
                {
                    // get the main view that holds most of the UI
                    UIView uiView = UIView.GetAView();

                    // create a new activation button on the main view
                    // eventually the Start method will be called to complete the initialization
                    _activationButton = (ZoneInfoActivationButton)uiView.AddUIComponent(typeof(ZoneInfoActivationButton));
                    if (_activationButton == null)
                    {
                        Debug.LogError($"Unable to create activation button on main view.");
                        return;
                    }

                    // move the activation button to its initial position according to the config
                    ZoneInfoConfiguration config = Configuration<ZoneInfoConfiguration>.Load();
                    _activationButton.relativePosition = new Vector3(config.ButtonPositionX, config.ButtonPositionY);
                    _lastButtonPosition = _activationButton.relativePosition;

                    // create a new info panel on the main view
                    // eventually the Start method will be called to complete the initialization
                    _infoPanel = (ZoneInfoPanel)uiView.AddUIComponent(typeof(ZoneInfoPanel));
                    if (_infoPanel == null)
                    {
                        Debug.LogError($"Unable to create info panel on main view.");
                        return;
                    }

                    // move the panel to its initial position according to the config
                    _infoPanel.relativePosition = new Vector3(config.PanelPositionX, config.PanelPositionY);

                    // set event handler
                    _activationButton.eventClicked += ActivationButton_eventClicked;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// handle click on activation button
        /// </summary>
        private void ActivationButton_eventClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            // check if button moved
            if (_activationButton.relativePosition != _lastButtonPosition)
            {
                // button moved due to drag, ignore clicked event
                _lastButtonPosition = _activationButton.relativePosition;
                return;
            }

            // toggle panel visibility
            TogglePanelVisibility();
            eventParam.Use();
        }

        /// <summary>
        /// hide the panel if it is displayed
        /// </summary>
        public static void HidePanel()
        {
            if (_infoPanel != null && _infoPanel.isVisible)
            {
                TogglePanelVisibility();
            }
        }

        /// <summary>
        /// toggle panel visibility
        /// </summary>
        private static void TogglePanelVisibility()
        {
            // make sure panel was created
            if (_infoPanel != null)
            {
                // toggle panel visibility
                _infoPanel.isVisible = !_infoPanel.isVisible;

                // adjust button background images based on panel visibility
                _activationButton.SetBackgroundImages(_infoPanel.isVisible);
            }
        }

        public override void OnLevelUnloading()
        {
            // do base processing
            base.OnLevelUnloading();

            try
            {
                // destroy objects that were added directly, this also destroys all contained objects
                // must do this explicitly because loading a saved game from the Pause Menu
                // does not destroy the objects implicitly like returning to the Main Menu to load a saved game
                if (_activationButton != null)
                {
                    UnityEngine.Object.Destroy(_activationButton);
                    _activationButton = null;
                }
                if (_infoPanel != null)
                {
                    _infoPanel.StopUpdate();    // try to stop update loop
                    UnityEngine.Object.Destroy(_infoPanel);
                    _infoPanel = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}