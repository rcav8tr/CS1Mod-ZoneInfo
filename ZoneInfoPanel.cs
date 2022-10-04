using ColossalFramework.UI;
using UnityEngine;
using System;
using ColossalFramework.Globalization;
using ColossalFramework.Math;

namespace ZoneInfo
{
    /// <summary>
    /// a panel to display zone info on the screen
    /// </summary>
    public class ZoneInfoPanel : UIPanel
    {
        // default panel position
        public const float DefaultPositionX = 100f;
        public const float DefaultPositionY = 100f;

        // control block counting
        private ushort _blockCounter = 0;
        private bool _countAll = false;
        private bool _displayCounts = false;
        private bool _stopUpdate = false;

        // define the zone types to be counted
        private enum Zone
        {
            ResidentialGenericLow,
            ResidentialGenericHigh,
            ResidentialSelfSuff,
            ResidentialWallToWall,
            ResidentialSubtotal,

            CommercialGenericLow,
            CommercialGenericHigh,
            CommercialTourism,
            CommercialLeisure,
            CommercialOrganic,
            CommercialWallToWall,
            CommercialSubtotal,

            IndustrialGeneric,
            IndustrialForestry,
            IndustrialFarming,
            IndustrialOre,
            IndustrialOil,
            IndustrialSubtotal,

            OfficeGeneric,
            OfficeITCluster,
            OfficelWallToWall,
            OfficeSubtotal,

            Unzoned,

            Total
        };
        private static readonly Array Zones = Enum.GetValues(typeof(Zone));
        private static readonly int ZoneCount = Zones.Length;

        /// <summary>
        /// hold the info for a data row
        /// </summary>
        private class SquareCount
        {
            // get the maximum allowed districts based on the buffer size (or the constant if the buffer size is not available)
            // +1 to make room for entry for Entire City
            private static readonly int maxDistricts = ((DistrictManager.exists && DistrictManager.instance.m_districts != null && DistrictManager.instance.m_districts.m_buffer != null) ?
                DistrictManager.instance.m_districts.m_buffer.Length : DistrictManager.MAX_DISTRICT_COUNT) + 1;

            // the 3 counts for a data row
            // index into each array is district ID
            public int[] built = new int[maxDistricts];
            public int[] empty = new int[maxDistricts];
            public int[] total = new int[maxDistricts];

            /// <summary>
            /// increment the counts for the specified district and for the Entire City
            /// </summary>
            public void Increment(byte districtID, bool occupied)
            {
                // increment either Built or Empty depending on the occupied flag
                if (occupied)
                {
                    built[districtID]++;
                    built[DistrictDropdown.DistrictIDEntireCity]++;
                }
                else
                {
                    empty[districtID]++;
                    empty[DistrictDropdown.DistrictIDEntireCity]++;
                }

                // always increment the total
                total[districtID]++;
                total[DistrictDropdown.DistrictIDEntireCity]++;
            }

            /// <summary>
            /// copy the data from another SquareCount to this one
            /// </summary>
            public void Copy(SquareCount from)
            {
                for (int districtID = 0; districtID < maxDistricts; districtID++)
                {
                    built[districtID] = from.built[districtID];
                    empty[districtID] = from.empty[districtID];
                    total[districtID] = from.total[districtID];
                }
            }

            /// <summary>
            /// reset the counts for all districts
            /// </summary>
            public void Reset()
            {
                for (int districtID = 0; districtID < maxDistricts; districtID++)
                {
                    built[districtID] = 0;
                    empty[districtID] = 0;
                    total[districtID] = 0;
                }
            }
        }

        /// <summary>
        /// the UI elements to display a SquareCount in a row
        /// </summary>
        private class UISquareCount
        {
            public bool valid;
            public UISprite symbol;
            public UILabel description;
            public UILabel built;
            public UILabel empty;
            public UILabel total;

            // routines to consistently construct sprite names for the symbol
            public static string SpriteNameNormal(Zone zone) { return zone.ToString() + "Normal"; }
            public static string SpriteNameLocked(Zone zone) { return zone.ToString() + "Locked"; }
        }

        // define square count arrays:
        //    temp for accumulating current counts
        //    final for holding the counts to display
        //    UI for holding the UI elements
        // index into array is the Zone enum
        private SquareCount[] _tempSquareCounts;
        private SquareCount[] _finalSquareCounts;
        private UISquareCount[] _uiSquareCounts;

        // other UI elements
        private DistrictDropdown _district;
        private UISquareCount _heading;
        private UISprite _countCheckbox;
        private UILabel  _countLabel;
        private UISprite _percentCheckbox;
        private UILabel  _percentLabel;
        private UISprite _includeUnzonedCheckbox;
        private UILabel  _includeUnzonedLabel;
        private UIFont _defaultFont;

        // common UI properties
        private const float DistrictHeight = 45f;
        private const float LeftPadding = 8f;
        private const float ItemHeight = 17f;
        private static readonly Color32 TextColorNormal = new Color32(185, 221, 254, 255);
        private static readonly Color32 TextColorLocked = new Color32((byte)(TextColorNormal.r * 0.5f), (byte)(TextColorNormal.g * 0.5f), (byte)(TextColorNormal.b * 0.5f), 255);
        private const float TextScale = 0.75f;

        // size of one side of a square
        private const float SquareSize = 8f;


        /// <summary>
        /// Start is called after the panel is created
        /// set up the panel
        /// </summary>
        public override void Start()
        {
            // do base processing
            base.Start();

            try
            {
                // set properties
                name = "ZoneInfoPanel";
                backgroundSprite = "MenuPanel2";
                opacity = 1f;
                isVisible = false;  // default to hidden
                canFocus = true;
                eventVisibilityChanged += ZoneInfoPanel_eventVisibilityChanged;

                // get default font to use for the panel
                // the view default font is OpenSans-Semibold, but OpenSans-Regular is desired
                // so copy the font from a component with that font
                _defaultFont = GetUIView().defaultFont;
                UITextComponent[] textComponents = FindObjectsOfType<UITextComponent>();
                foreach (UITextComponent textComponent in textComponents)
                {
                    UIFont font = textComponent.font;
                    if (font != null && font.isValid && font.name == "OpenSans-Regular")
                    {
                        _defaultFont = font;
                        break;
                    }
                }

                // add district dropdown
                _district = AddUIComponent<DistrictDropdown>();
                if (_district == null || !_district.initialized)
                {
                    LogUtil.LogError($"Unable to create district dropdown on panel [{name}].");
                    return;
                }
                _district.name = "DistrictPanel";
                _district.relativePosition = new Vector3(LeftPadding, 45f);
                _district.dropdownHeight = ItemHeight + 7f;
                _district.font = _defaultFont;
                _district.textScale = TextScale;
                _district.textColor = TextColorNormal;
                _district.disabledTextColor = TextColorLocked;
                _district.listHeight = 10 * (int)ItemHeight + 8;
                _district.itemHeight = (int)ItemHeight;
                _district.builtinKeyNavigation = true;
                _district.eventSelectedDistrictChanged += SelectedDistrictChanged;

                // create heading row
                float top = 50f;            // start just below title bar of panel
                top += DistrictHeight;      // skip over district dropdown
                _heading = new UISquareCount();
                if (!CreateUISquareCount(_heading, "Heading", "", ref top)) return;
                _heading.built.text = "Built";
                _heading.empty.text = "Empty";
                _heading.total.text = "Total";
                _heading.built.tooltip = "Built Squares";
                _heading.empty.tooltip = "Empty Squares";
                _heading.total.tooltip = "Built + Empty Squares";

                // create a line under each count heading
                if (!CreateLine(_heading.built)) return;
                if (!CreateLine(_heading.empty)) return;
                if (!CreateLine(_heading.total)) return;

                // create two checkboxes left of the heading
                if (!CreateCheckbox(ref _countCheckbox, ref _countLabel, "Count", _heading, 16f, 50f)) return;
                if (!CreateCheckbox(ref _percentCheckbox, ref _percentLabel, "Percent", _heading, _countLabel.relativePosition.x + _countLabel.size.x + LeftPadding, 70f)) return;

                // initialize check boxes so that Count is checked by default
                SetCheckBox(_countCheckbox, true);
                SetCheckBox(_percentCheckbox, false);

                // set event handlers for count and percent
                _countCheckbox.eventClicked   += CheckBox_eventClicked;
                _countLabel.eventClicked      += CheckBox_eventClicked;
                _percentCheckbox.eventClicked += CheckBox_eventClicked;
                _percentLabel.eventClicked    += CheckBox_eventClicked;

                // get zone atlas
                UITextureAtlas zoneAtlas = GetZoneAtlas();
                if (zoneAtlas == null)
                {
                    LogUtil.LogError($"Unable to get atlas of zone images.");
                    return;
                }

                // create a square count for each Zone
                _tempSquareCounts  = new SquareCount[ZoneCount];
                _finalSquareCounts = new SquareCount[ZoneCount];
                _uiSquareCounts    = new UISquareCount[ZoneCount];
                foreach (Zone zone in Zones)
                {
                    _tempSquareCounts [(int)zone] = new SquareCount();
                    _finalSquareCounts[(int)zone] = new SquareCount();
                    _uiSquareCounts   [(int)zone] = new UISquareCount();
                }

                // get DLC flags
                bool dlcBaseGame         = true;
                bool dlcAfterDark        = SteamHelper.IsDLCOwned(SteamHelper.DLC.AfterDarkDLC);
                bool dlcGreenCities      = SteamHelper.IsDLCOwned(SteamHelper.DLC.GreenCitiesDLC);
                bool dlcPlazasPromenades = SteamHelper.IsDLCOwned(SteamHelper.DLC.PlazasAndPromenadesDLC);

                // create each UI square count row based on DLC
                top += 5f;
                const float SectionSpacing = 10f;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.ResidentialGenericLow,  "ResidentialLow",        "Residential Low Density",  zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.ResidentialGenericHigh, "ResidentialHigh",       "Residential High Density", zoneAtlas, ref top)) return; }
                if (dlcGreenCities     ) { if (!CreateUISquareCount(Zone.ResidentialSelfSuff,    "ResidentialSelfSuff",   "Residential Self-Suff",    zoneAtlas, ref top)) return; }
                if (dlcPlazasPromenades) { if (!CreateUISquareCount(Zone.ResidentialWallToWall,  "ResidentialWallToWall", "Residential Wall-to-Wall", zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.ResidentialSubtotal,    "ResidentialSubTotal",   "Residential Subtotal",     zoneAtlas, ref top)) return; }

                top += SectionSpacing;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.CommercialGenericLow,   "CommercialLow",         "Commercial Low Density",   zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.CommercialGenericHigh,  "CommercialHigh",        "Commercial High Density",  zoneAtlas, ref top)) return; }
                if (dlcAfterDark       ) { if (!CreateUISquareCount(Zone.CommercialTourism,      "CommercialTourism",     "Commercial Tourism",       zoneAtlas, ref top)) return; }
                if (dlcAfterDark       ) { if (!CreateUISquareCount(Zone.CommercialLeisure,      "CommercialLeisure",     "Commercial Leisure",       zoneAtlas, ref top)) return; }
                if (dlcGreenCities     ) { if (!CreateUISquareCount(Zone.CommercialOrganic,      "CommercialOrganic",     "Commercial Organic",       zoneAtlas, ref top)) return; }
                if (dlcPlazasPromenades) { if (!CreateUISquareCount(Zone.CommercialWallToWall,   "CommercialWallToWall",  "Commercial Wall-to-Wall",  zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.CommercialSubtotal,     "CommercialSubTotal",    "Commercial Subtotal",      zoneAtlas, ref top)) return; }

                top += SectionSpacing;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialGeneric,      "IndustrialGeneric",     "Industrial Generic",       zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialForestry,     "IndustrialForestry",    "Industrial Forestry",      zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialFarming,      "IndustrialFarming",     "Industrial Farming",       zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialOre,          "IndustrialOre",         "Industrial Ore",           zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialOil,          "IndustrialOil",         "Industrial Oil",           zoneAtlas, ref top)) return; }
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.IndustrialSubtotal,     "IndustrialSubTotal",    "Industrial Subtotal",      zoneAtlas, ref top)) return; }

                top += SectionSpacing;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.OfficeGeneric,          "OfficeGeneric",         "Office Generic",           zoneAtlas, ref top)) return; }
                if (dlcGreenCities     ) { if (!CreateUISquareCount(Zone.OfficeITCluster,        "OfficeITCluster",       "Office IT Cluster",        zoneAtlas, ref top)) return; }
                if (dlcPlazasPromenades) { if (!CreateUISquareCount(Zone.OfficelWallToWall,      "OfficeWallToWall",      "Office Wall-to-Wall",      zoneAtlas, ref top)) return; }
                if (dlcGreenCities ||
                    dlcPlazasPromenades) { if (!CreateUISquareCount(Zone.OfficeSubtotal,         "OfficeSubTotal",        "Office Subtotal",          zoneAtlas, ref top)) return; }

                top += SectionSpacing;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.Unzoned,                "Unzoned",               "Unzoned",                  zoneAtlas, ref top)) return; }

                top += SectionSpacing;
                if (dlcBaseGame        ) { if (!CreateUISquareCount(Zone.Total,                  "Total",                 "Total",                    zoneAtlas, ref top)) return; }

                // add the Include Unzoned checkbox, but hidden
                if (!CreateCheckbox(ref _includeUnzonedCheckbox, ref _includeUnzonedLabel, "Include", _uiSquareCounts[(int)Zone.Unzoned], 110f, 60f)) return;
                SetCheckBox(_includeUnzonedCheckbox, true);
                _includeUnzonedCheckbox.isVisible = false;
                _includeUnzonedLabel.isVisible = false;

                // set panel size based on size and position of total row
                UISquareCount totalRow = _uiSquareCounts[(int)Zone.Total];
                size = new Vector3(
                    totalRow.total.relativePosition.x + totalRow.total.size.x + totalRow.symbol.relativePosition.x,
                    totalRow.total.relativePosition.y + totalRow.total.size.y + 5f);
                _district.size = new Vector2(width - 2f * LeftPadding, DistrictHeight);

                // get the atlas of activation button images
                UITextureAtlas activationButtonImages = ZoneInfoActivationButton.GetActivationButtonAtlas();
                if (activationButtonImages == null)
                {
                    LogUtil.LogError($"Unable to get atlas of activation button images.");
                    return;
                }

                // create icon in upper left
                UISprite panelIcon = AddUIComponent<UISprite>();
                if (panelIcon == null)
                {
                    LogUtil.LogError($"Unable to create icon on panel [{name}].");
                    return;
                }
                panelIcon.name = "Icon";
                panelIcon.autoSize = false;
                panelIcon.size = new Vector2(36f, 36f);
                panelIcon.relativePosition = new Vector3(10f, 2f);
                panelIcon.atlas = activationButtonImages;
                panelIcon.spriteName = ZoneInfoActivationButton.ForegroundSprite;
                panelIcon.isVisible = true;

                // create the title label
                UILabel title = AddUIComponent<UILabel>();
                if (title == null)
                {
                    LogUtil.LogError($"Unable to create title label on [{name}].");
                    return;
                }
                title.name = "Title";
                title.font = _defaultFont;
                title.text = "Zones";
                title.textAlignment = UIHorizontalAlignment.Center;
                title.textScale = 1f;
                title.textColor = new Color32(254, 254, 254, 255);
                title.autoSize = false;
                title.size = new Vector2(size.x, 18f);
                title.relativePosition = new Vector3(0f, 11f);
                title.isVisible = true;

                // create close button
                UIButton closeButton = AddUIComponent<UIButton>();
                if (closeButton == null)
                {
                    LogUtil.LogError($"Unable to create close button on panel [{name}].");
                    return;
                }
                closeButton.name = "Close";
                closeButton.autoSize = false;
                closeButton.size = new Vector2(32f, 32f);
                closeButton.relativePosition = new Vector3(width - 34f, 2f);
                closeButton.normalBgSprite = "buttonclose";
                closeButton.hoveredBgSprite = "buttonclosehover";
                closeButton.pressedBgSprite = "buttonclosepressed";
                closeButton.isVisible = true;
                closeButton.eventClicked += CloseButton_eventClicked;

                // attach drag handle
                UIDragHandle dragHandle = AddUIComponent<UIDragHandle>();
                if (dragHandle == null)
                {
                    LogUtil.LogError($"Unable to create drag handle on [{name}].");
                    return;
                }
                dragHandle.name = "DragHandle";
                dragHandle.relativePosition = Vector3.zero;
                dragHandle.size = new Vector3(size.x, 40f);
                dragHandle.eventMouseUp += DragHandle_eventMouseUp;

                // make sure drag handle is in front of icon and title
                // make sure close button is in front of drag handle
                dragHandle.BringToFront();
                closeButton.BringToFront();

                // update loop is not stopped
                _stopUpdate = false;
            }
            catch (Exception ex)
            {
                LogUtil.LogException(ex);
            }
        }

        private void DragHandle_eventMouseUp(UIComponent component, UIMouseEventParameter eventParam)
        {
            // save position
            ZoneInfoConfiguration.SavePanelPosition(relativePosition);
        }

        /// <summary>
        /// get an atlas of the zone images
        /// </summary>
        public static UITextureAtlas GetZoneAtlas()
        {
            // load zone image texture from the DLL
            int zoneImageCount = ZoneCount * 2;
            string resourceName = typeof(ZoneInfoPanel).Namespace + ".ZoneImages.png";
            Texture2D zoneImages = TextureUtil.GetDllResource(resourceName, zoneImageCount * 40, 40);
            if (zoneImages == null)
            {
                LogUtil.LogError($"Unable to get zone image resource.");
                return null;
            }

            // construct array of sprite names
            // assumes zone images are in the same order as the Zone enum
            string[] spriteNames = new string[zoneImageCount];
            int i = 0;
            foreach (Zone zone in Zones)
            {
                spriteNames[i++] = UISquareCount.SpriteNameNormal(zone);
                spriteNames[i++] = UISquareCount.SpriteNameLocked(zone);
            }

            // create a new atlas of zone images
            return TextureUtil.GenerateAtlasFromHorizontalResource("ZoneImages", zoneImages, zoneImageCount, spriteNames);
        }

        /// <summary>
        /// create a UI square count for the specified zone
        /// </summary>
        private bool CreateUISquareCount(Zone zone, string namePrefix, string text, UITextureAtlas zoneAtlas, ref float top)
        {
            // create the UI square count
            UISquareCount uiSquareCount = _uiSquareCounts[(int)zone];
            if (!CreateUISquareCount(uiSquareCount, namePrefix, text, ref top))
            {
                return false;
            }

            // set symbol image, default to locked
            uiSquareCount.symbol.atlas = zoneAtlas;
            uiSquareCount.symbol.spriteName = UISquareCount.SpriteNameLocked(zone);

            // square count is valid
            uiSquareCount.valid = true;

            // success
            return true;
        }

        /// <summary>
        /// create a square count row in the specified UISquareCount
        /// </summary>
        private bool CreateUISquareCount(UISquareCount uiSquareCount, string namePrefix, string text, ref float top)
        {
            // add the symbol sprite
            uiSquareCount.symbol = AddUIComponent<UISprite>();
            if (uiSquareCount.symbol == null)
            {
                LogUtil.LogError($"Unable to add symbol sprite for {text} on {name}.");
                return false;
            }
            uiSquareCount.symbol.name = namePrefix + "Symbol";
            uiSquareCount.symbol.autoSize = false;
            uiSquareCount.symbol.size = new Vector2(ItemHeight, ItemHeight);    // width is same as height
            uiSquareCount.symbol.relativePosition = new Vector3(LeftPadding, top - 2f);  // -2 to align properly with text in labels
            uiSquareCount.symbol.isVisible = true;

            // add the labels
            const float CountWidth = 67f;
            const string DefaultCountText = "0,000,000";
            if (!CreateLabel(ref uiSquareCount.description, uiSquareCount.symbol,      top, 170f,       ItemHeight, namePrefix + "Description", text            )) return false;
            if (!CreateLabel(ref uiSquareCount.built,       uiSquareCount.description, top, CountWidth, ItemHeight, namePrefix + "Built",       DefaultCountText)) return false;
            if (!CreateLabel(ref uiSquareCount.empty,       uiSquareCount.built,       top, CountWidth, ItemHeight, namePrefix + "Empty",       DefaultCountText)) return false;
            if (!CreateLabel(ref uiSquareCount.total,       uiSquareCount.empty,       top, CountWidth, ItemHeight, namePrefix + "Total",       DefaultCountText)) return false;
            uiSquareCount.description.textAlignment = UIHorizontalAlignment.Left;

            // increment top for next one
            top += ItemHeight;

            // success
            return true;
        }

        /// <summary>
        /// create a label for a data row
        /// </summary>
        private bool CreateLabel(ref UILabel label, UIComponent priorComponent, float top, float width, float height, string name, string text)
        {
            label = AddUIComponent<UILabel>();
            if (label == null)
            {
                LogUtil.LogError($"Unable to add {name} label on {name}.");
                return false;
            }
            label.name = name;
            label.font = _defaultFont;
            label.text = text;
            label.textAlignment = UIHorizontalAlignment.Right;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textScale = TextScale;
            label.textColor = TextColorNormal;
            label.autoSize = false;
            label.size = new Vector2(width, height);
            label.relativePosition = new Vector3(priorComponent.relativePosition.x + priorComponent.size.x + 4f, top);
            label.isVisible = true;

            // success
            return true;
        }

        /// <summary>
        /// create a line under the label
        /// </summary>
        private bool CreateLine(UILabel label)
        {
            // create the line
            UISprite line = label.AddUIComponent<UISprite>();
            if (line == null)
            {
                LogUtil.LogError($"Unable to create line under [{label.name}] on [{name}].");
                return false;
            }
            line.name = label.name + "Line";
            line.autoSize = false;
            line.size = new Vector2(label.width, 1f);
            line.relativePosition = new Vector3(1f, label.size.y);
            line.spriteName = "EmptySprite";
            line.color = TextColorNormal;
            line.isVisible = true;

            // success
            return true;
        }

        /// <summary>
        /// create a check box and corresponding label
        /// </summary>
        private bool CreateCheckbox(ref UISprite checkbox, ref UILabel label, string text, UISquareCount createRelativeTo, float x, float width)
        {
            // create check box
            checkbox = AddUIComponent<UISprite>();
            if (checkbox == null)
            {
                LogUtil.LogError($"Unable to create {text} checkbox sprite on {name}.");
                return false;
            }
            checkbox.name = text + "Checkbox";
            checkbox.autoSize = false;
            checkbox.size = createRelativeTo.symbol.size;
            checkbox.relativePosition = new Vector3(x, createRelativeTo.symbol.relativePosition.y);
            checkbox.isVisible = true;

            // create label for check box
            label = AddUIComponent<UILabel>();
            if (label == null)
            {
                LogUtil.LogError($"Unable to create {text} checkbox label on {name}.");
                return false;
            }
            label.name = text + "Label";
            label.font = _defaultFont;
            label.text = text;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textScale = TextScale;
            label.textColor = TextColorNormal;
            label.autoSize = false;
            label.size = new Vector2(width, createRelativeTo.description.size.y);
            label.relativePosition = new Vector3(checkbox.relativePosition.x + checkbox.size.x + 4f, createRelativeTo.description.relativePosition.y);
            label.isVisible = true;

            // success
            return true;
        }

        /// <summary>
        /// handle change in selected district
        /// </summary>
        private void SelectedDistrictChanged(object sender, SelectedDistrictChangedEventArgs eventParam)
        {
            // redisplay counts
            _displayCounts = true;
        }

        /// <summary>
        ///  handle change in panel visibility
        /// </summary>
        private void ZoneInfoPanel_eventVisibilityChanged(UIComponent component, bool value)
        {
            if (value)
            {
                // immediately count all
                _countAll = true;
            }
            else
            {
                // hide districts,
                // however if the Districts info view mode or the district drawing tool are active,
                // they will keep districts shown, even after being hidden here
                if (DistrictManager.exists)
                {
                    DistrictManager.instance.DistrictsVisible = false;
                }

                // hide zones except if ZoneTool is active,
                // however if a tool is active that shows zones,
                // the tool will keep zones shown, even after being hidden here
                // except that (interestingly) the ZoneTool does not keep zones shown
                if (TerrainManager.exists && ToolsModifierControl.toolController.CurrentTool.GetType() != typeof(ZoneTool))
                {
                    TerrainManager.instance.RenderZones = false;
                }
            }
        }

        /// <summary>
        /// handle click on Close button
        /// </summary>
        private void CloseButton_eventClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            // hide the panel
            ZoneInfoLoading.HidePanel();
        }

        /// <summary>
        /// handle clicks on count and percent check boxes
        /// </summary>
        private void CheckBox_eventClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            // regardless of which one was clicked, toggle both check boxes
            SetCheckBox(_countCheckbox,   !IsCheckBoxChecked(_countCheckbox  ));
            SetCheckBox(_percentCheckbox, !IsCheckBoxChecked(_percentCheckbox));

            // redisplay counts
            _displayCounts = true;
        }

        /// <summary>
        /// handle click on Include Unzoned checkbox
        /// </summary>
        private void _includeUnzonedCheckbox_eventClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            // toggle check box
            SetCheckBox(_includeUnzonedCheckbox, !IsCheckBoxChecked(_includeUnzonedCheckbox));

            // redisplay counts
            _displayCounts = true;
        }

        /// <summary>
        /// set the check box (i.e. sprite) status
        /// </summary>
        private void SetCheckBox(UISprite checkBox, bool value)
        {
            checkBox.spriteName = (value ? "check-checked" : "check-unchecked");
        }

        /// <summary>
        /// return whether or not the check box (i.e. sprite) is checked
        /// </summary>
        private bool IsCheckBoxChecked(UISprite checkBox)
        {
            return checkBox.spriteName == "check-checked";
        }

        /// <summary>
        /// stop the Update loop
        /// </summary>
        public void StopUpdate()
        {
            _stopUpdate = true;

            // hide districts and zones so they will not be initially shown if another game is started
            if (DistrictManager.exists)
            {
                DistrictManager.instance.DistrictsVisible = false;
            }
            if (TerrainManager.exists)
            {
                TerrainManager.instance.RenderZones = false;
            }
        }

        /// <summary>
        /// Update is called every frame
        /// </summary>
        public override void Update()
        {
            // do base processing
            base.Update();

            try
            {
                // only run logic when panel is visible
                if (!isVisible)
                {
                    return;
                }

                // check for stop
                if (_stopUpdate)
                {
                    return;
                }

                // managers must be ready
                if (!ZoneManager.exists || !DistrictManager.exists || !InfoManager.exists || !TerrainManager.exists || !BuildingManager.exists)
                {
                    return;
                }

                //  show or hide districts
                DistrictManager instance = DistrictManager.instance;
                InfoManager.InfoMode currentInfoViewMode = InfoManager.instance.CurrentMode;
                Type currentToolType = ToolsModifierControl.toolController.CurrentTool.GetType();
                if (currentInfoViewMode == InfoManager.InfoMode.None && currentToolType == typeof(DefaultTool))
                {
                    // no info mode and no tool, show districts
                    instance.DistrictsVisible = true;
                }
                else
                {
                    // info view is visible or a tool is selected, hide districts
                    instance.DistrictsVisible = false;
                }

                // show or hide zones
                if (currentInfoViewMode == InfoManager.InfoMode.None)
                {
                    // no info mode, check tool type
                    if (currentToolType == typeof(NetTool) || currentToolType == typeof(BuildingTool))
                    {
                        // leave show/hide status unchanged and allow the tool to control the status
                    }
                    else
                    {
                        // for other tools (especially the ZoneTool), display zones
                        TerrainManager.instance.RenderZones = true;
                    }
                }
                else
                {
                    // info view is visible, hide zones
                    TerrainManager.instance.RenderZones = false;
                }

                // if counting all, reset counters to start over at first zone block
                bool countAll = false;
                if (_countAll)
                {
                    _blockCounter = 0;
                    ResetSquareCounts();
                    countAll = true;
                    _countAll = false;
                }

                // data for the previous building found
                Quad2 buildingCorners = default;
                ItemClass.SubService buildingSubservice = ItemClass.SubService.None;
                bool buildingFound = false;

                // set the number of blocks to do each frame
                // a full recount occurs every:  ZoneManager.MAX_BLOCK_COUNT (i.e. 49152) / MaxBlocksPerFrame = 192 frames
                const ushort MaxBlocksPerFrame = 256;

                // do each zone block
                // do only some blocks each frame, unless counting all
                ZoneBlock[] blocks = ZoneManager.instance.m_blocks.m_buffer;
                for (ushort blocksThisFrame = 0; (blocksThisFrame < MaxBlocksPerFrame || countAll) && _blockCounter < blocks.Length; blocksThisFrame++, _blockCounter++)
                {
                    // do only created blocks
                    ZoneBlock block = blocks[_blockCounter];
                    if ((block.m_flags & ZoneBlock.FLAG_CREATED) != 0)
                    {
                        // compute values for the block that will be used later to compute the position and corners of each square in the block
                        float angle = block.m_angle;
                        Vector2 vector1 = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SquareSize;
                        Vector2 vector2 = new Vector2(vector1.y, 0f - vector1.x);
                        Vector2 blockPosition = VectorUtils.XZ(block.m_position);

                        // do each row along the segment
                        int rowCount = block.RowCount;
                        for (int z = 0; z < rowCount; ++z)
                        {
                            // do each column in from the segment
                            // there can never be more than 4
                            for (int x = 0; x < 4; ++x)
                            {
                                // check for stop
                                if (_stopUpdate)
                                {
                                    return;
                                }

                                // do only valid non-shared squares
                                ulong mask = (ulong)(1L << ((z << 3) | x));
                                bool occupied = (((block.m_occupied1 | block.m_occupied2) & mask) != 0);
                                if ((block.m_valid & mask) != 0 && (block.m_shared & mask) == 0)
                                {
                                    // compute square's center position
                                    // start with the block position
                                    // +1 to convert 0-based x,z into 1-based x,z
                                    // -4 because (I don't really know why, copied logic from various ZoneManager and ZoneBlock methods, but it works)
                                    // -0.5f to put the position in the center of the square
                                    Vector2 squareCenter2 = blockPosition + ((x + 1 - 4 - 0.5f) * vector1) + ((z + 1 - 4 - 0.5f) * vector2);
                                    Vector3 squareCenter3 = new Vector3(squareCenter2.x, block.m_position.y, squareCenter2.y);

                                    // get the zone of the square
                                    ItemClass.Zone zone = block.GetZone(x, z);

                                    // get the specialization, if any, from the square's District
                                    DistrictPolicies.Specialization specialization = DistrictPolicies.Specialization.None;
                                    byte districtID = instance.GetDistrict(squareCenter3);
                                    District district = instance.m_districts.m_buffer[districtID];
                                    if ((district.m_flags & District.Flags.Created) != 0)
                                    {
                                        specialization = district.m_specializationPolicies;
                                    }

                                    // use the square's zone and specialization to determine which SquareCount and subtotal to increment
                                    Zone countToIncrement    = (Zone)(-1);
                                    Zone subtotalToIncrement = (Zone)(-1);
                                    switch (zone)
                                    {
                                        case ItemClass.Zone.ResidentialLow:
                                        case ItemClass.Zone.ResidentialHigh:
                                            if      ((specialization & DistrictPolicies.Specialization.Selfsufficient       ) != 0) { countToIncrement = Zone.ResidentialSelfSuff;    }
                                            else if ((specialization & DistrictPolicies.Specialization.ResidentialWallToWall) != 0) { countToIncrement = Zone.ResidentialWallToWall;  }
                                            else if (zone == ItemClass.Zone.ResidentialLow)                                         { countToIncrement = Zone.ResidentialGenericLow;  }
                                            else if (zone == ItemClass.Zone.ResidentialHigh)                                        { countToIncrement = Zone.ResidentialGenericHigh; }
                                            subtotalToIncrement = Zone.ResidentialSubtotal;
                                            break;

                                        case ItemClass.Zone.CommercialLow:
                                        case ItemClass.Zone.CommercialHigh:
                                            if      ((specialization & DistrictPolicies.Specialization.Tourist             ) != 0) { countToIncrement = Zone.CommercialTourism;     }
                                            else if ((specialization & DistrictPolicies.Specialization.Leisure             ) != 0) { countToIncrement = Zone.CommercialLeisure;     }
                                            else if ((specialization & DistrictPolicies.Specialization.Organic             ) != 0) { countToIncrement = Zone.CommercialOrganic;     }
                                            else if ((specialization & DistrictPolicies.Specialization.CommercialWallToWall) != 0) { countToIncrement = Zone.CommercialWallToWall;  }
                                            else if (zone == ItemClass.Zone.CommercialLow)                                         { countToIncrement = Zone.CommercialGenericLow;  }
                                            else if (zone == ItemClass.Zone.CommercialHigh)                                        { countToIncrement = Zone.CommercialGenericHigh; }
                                            subtotalToIncrement = Zone.CommercialSubtotal;
                                            break;

                                        case ItemClass.Zone.Industrial:
                                            if      ((specialization & DistrictPolicies.Specialization.Forest ) != 0) { countToIncrement = Zone.IndustrialForestry; }
                                            else if ((specialization & DistrictPolicies.Specialization.Farming) != 0) { countToIncrement = Zone.IndustrialFarming;  }
                                            else if ((specialization & DistrictPolicies.Specialization.Ore    ) != 0) { countToIncrement = Zone.IndustrialOre;      }
                                            else if ((specialization & DistrictPolicies.Specialization.Oil    ) != 0) { countToIncrement = Zone.IndustrialOil;      }
                                            else                                                                      { countToIncrement = Zone.IndustrialGeneric;  }
                                            subtotalToIncrement = Zone.IndustrialSubtotal;
                                            break;

                                        case ItemClass.Zone.Office:
                                            if      ((specialization & DistrictPolicies.Specialization.Hightech        ) != 0) { countToIncrement = Zone.OfficeITCluster;   }
                                            else if ((specialization & DistrictPolicies.Specialization.OfficeWallToWall) != 0) { countToIncrement = Zone.OfficelWallToWall; }
                                            else                                                                               { countToIncrement = Zone.OfficeGeneric;   }
                                            subtotalToIncrement = Zone.OfficeSubtotal;
                                            break;

                                        case ItemClass.Zone.Unzoned:
                                            // check if occupied
                                            if (occupied)
                                            {
                                                // compute the corners of the square
                                                // use 0.5 because need to go half of the square size in each direction
                                                // use 0.99 to get just inside the square's corners to prevent finding buildings on adjacent squares
                                                const float multiplier = 0.5f * 0.99f;
                                                Vector2 squareVector1 = vector1 * multiplier;
                                                Vector2 squareVector2 = vector2 * multiplier;
                                                Quad2 squareCorners = default;
                                                squareCorners.a = squareCenter2 - squareVector1 - squareVector2;
                                                squareCorners.b = squareCenter2 + squareVector1 - squareVector2;
                                                squareCorners.c = squareCenter2 + squareVector1 + squareVector2;
                                                squareCorners.d = squareCenter2 - squareVector1 + squareVector2;

                                                // performance enhancement:
                                                // it is likely that the current square position being checked is in the building previously found
                                                // the Quad2.Intersect logic is much faster than GetBuildingAtPosition

                                                // check if a building was found and current square corners being checked intersect with the previous building's corners
                                                if (buildingFound && squareCorners.Intersect(buildingCorners))
                                                {
                                                    // use subservice from previous building
                                                }
                                                else
                                                {
                                                    // no building previously found or building does not intersect with the current square
                                                    // check if there is a building at the square position and get the building's corners and subservice
                                                    buildingFound = GetBuildingAtPosition(squareCenter3, squareCorners, ref buildingCorners, ref buildingSubservice);
                                                }

                                                // if a building is found, use the subservice to determine which square count and subtotal to increment
                                                if (buildingFound)
                                                {
                                                    switch (buildingSubservice)
                                                    {
                                                        case ItemClass.SubService.ResidentialLowEco:
                                                        case ItemClass.SubService.ResidentialHighEco:    countToIncrement = Zone.ResidentialSelfSuff;    subtotalToIncrement = Zone.ResidentialSubtotal; break;
                                                        case ItemClass.SubService.ResidentialWallToWall: countToIncrement = Zone.ResidentialWallToWall;  subtotalToIncrement = Zone.ResidentialSubtotal; break;
                                                        case ItemClass.SubService.ResidentialLow:        countToIncrement = Zone.ResidentialGenericLow;  subtotalToIncrement = Zone.ResidentialSubtotal; break;
                                                        case ItemClass.SubService.ResidentialHigh:       countToIncrement = Zone.ResidentialGenericHigh; subtotalToIncrement = Zone.ResidentialSubtotal; break;

                                                        case ItemClass.SubService.CommercialTourist:     countToIncrement = Zone.CommercialTourism;      subtotalToIncrement = Zone.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialLeisure:     countToIncrement = Zone.CommercialLeisure;      subtotalToIncrement = Zone.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialEco:         countToIncrement = Zone.CommercialOrganic;      subtotalToIncrement = Zone.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialWallToWall:  countToIncrement = Zone.CommercialWallToWall;   subtotalToIncrement = Zone.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialLow:         countToIncrement = Zone.CommercialGenericLow;   subtotalToIncrement = Zone.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialHigh:        countToIncrement = Zone.CommercialGenericHigh;  subtotalToIncrement = Zone.CommercialSubtotal;  break;

                                                        case ItemClass.SubService.PlayerIndustryForestry:
                                                        case ItemClass.SubService.IndustrialForestry:    countToIncrement = Zone.IndustrialForestry;     subtotalToIncrement = Zone.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryFarming:
                                                        case ItemClass.SubService.IndustrialFarming:     countToIncrement = Zone.IndustrialFarming;      subtotalToIncrement = Zone.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryOre:
                                                        case ItemClass.SubService.IndustrialOre:         countToIncrement = Zone.IndustrialOre;          subtotalToIncrement = Zone.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryOil:
                                                        case ItemClass.SubService.IndustrialOil:         countToIncrement = Zone.IndustrialOil;          subtotalToIncrement = Zone.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.IndustrialGeneric:     countToIncrement = Zone.IndustrialGeneric;      subtotalToIncrement = Zone.IndustrialSubtotal;  break;

                                                        case ItemClass.SubService.OfficeHightech:        countToIncrement = Zone.OfficeITCluster;        subtotalToIncrement = Zone.OfficeSubtotal;      break;
                                                        case ItemClass.SubService.OfficeWallToWall:      countToIncrement = Zone.OfficelWallToWall;      subtotalToIncrement = Zone.OfficeSubtotal;      break;
                                                        case ItemClass.SubService.OfficeGeneric:         countToIncrement = Zone.OfficeGeneric;          subtotalToIncrement = Zone.OfficeSubtotal;      break;

                                                        default:
                                                            // building is not a subservice being counted
                                                            // building could be a service building, park, or other structure that causes the square to be unzoned
                                                            // this is not an error, just count as unzoned
                                                            countToIncrement = Zone.Unzoned;
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    // no building found even though zone block indicates occupied
                                                    // this is not an error, just count as unzoned
                                                    countToIncrement = Zone.Unzoned;
                                                }
                                            }
                                            else
                                            {
                                                // unoccupied always gets counted as unzoned
                                                countToIncrement = Zone.Unzoned;
                                            }
                                            break;

                                        case ItemClass.Zone.None:
                                        case ItemClass.Zone.Distant:
                                            // ignore, should never get here
                                            break;
                                    }

                                    // increment the square count (if valid), subtotal (if valid), and total (always)
                                    if ((int)countToIncrement    != -1) _tempSquareCounts[(int)countToIncrement   ].Increment(districtID, occupied);
                                    if ((int)subtotalToIncrement != -1) _tempSquareCounts[(int)subtotalToIncrement].Increment(districtID, occupied);
                                                                        _tempSquareCounts[(int)Zone.Total         ].Increment(districtID, occupied);
                                }
                            }
                        }
                    }
                }

                // check if went thru all blocks
                if (_blockCounter >= blocks.Length)
                {
                    // copy temp to final
                    foreach (Zone zone in Zones)
                    {
                        _finalSquareCounts[(int)zone].Copy(_tempSquareCounts[(int)zone]);
                    }

                    // display square counts
                    DisplaySquareCounts();

                    // reset counts
                    ResetSquareCounts();
                    _blockCounter = 0;
                }
                // check if should display counts other than after went thru all blocks
                else if (_displayCounts)
                {
                    DisplaySquareCounts();
                }
            }
            catch (Exception ex)
            {
                LogUtil.LogException(ex);
            }
        }

        /// <summary>
        /// check if there is a building at the square position
        /// </summary>
        /// <remarks>logic adapted from BuildingManager.FindBuilding</remarks>
        /// <returns>whether or not a building is found</returns>
        /// <param name="buildingCorners">return the corners of the found building</param>
        /// <param name="buildingSubservice">return the subservice of the found building</param>
        private bool GetBuildingAtPosition(Vector3 squarePosition, Quad2 squareCorners, ref Quad2 buildingCorners, ref ItemClass.SubService buildingSubservice)
        {
            // compute XZ building grid indexes of the building grid cell that contains the square position
            // Min and Max prevent the indexes from being off the map
            const int GridRes = BuildingManager.BUILDINGGRID_RESOLUTION;
            int baseGridIndexX = Math.Min(Math.Max((int)(squarePosition.x / BuildingManager.BUILDINGGRID_CELL_SIZE + GridRes / 2f), 0), GridRes - 1);
            int baseGridIndexZ = Math.Min(Math.Max((int)(squarePosition.z / BuildingManager.BUILDINGGRID_CELL_SIZE + GridRes / 2f), 0), GridRes - 1);

            // compute low and high XZ grid indexes that are -1 and +1 from the base
            // this defines a 3x3 matrix of grid cells that are horizontally, vertically, and diagonally adjacent to the base grid cell
            // a building could be centered in any of these grid cells but overlap the square being checked
            // Min and Max prevent the indexes from being off the map
            int loGridIndexX = Math.Max(baseGridIndexX - 1, 0);
            int loGridIndexZ = Math.Max(baseGridIndexZ - 1, 0);
            int hiGridIndexX = Math.Min(baseGridIndexX + 1, GridRes - 1);
            int hiGridIndexZ = Math.Min(baseGridIndexZ + 1, GridRes - 1);

            // start with the base grid cell because it is most likely to contain the building being sought
            int gridIndexX = baseGridIndexX;
            int gridIndexZ = baseGridIndexZ;

            // do each grid cell in the 3x3 matrix (i.e. 9 grid cells)
            for (int gridCellCounter = 0; gridCellCounter < 9; )
            {
                // loop over every building in the grid cell
                int buildingCounter = 0;
                ushort buildingID = BuildingManager.instance.m_buildingGrid[gridIndexZ * GridRes + gridIndexX];
                while (buildingID != 0)
                {
                    // check for stop
                    if (_stopUpdate)
                    {
                        return false;
                    }

                    // building AI must derive from CommonBuildingAI
                    Building building = BuildingManager.instance.m_buildings.m_buffer[buildingID];
                    if (building.Info.GetAI().GetType().IsSubclassOf(typeof(CommonBuildingAI)))
                    {
                        // compute the corner positions of the building
                        // need to go half the width and length in each direction
                        // logic adapted from BuildingManager.UpdateParkingSpaces
                        float buildingAngle = building.m_angle;
                        Vector2 buildingVector1 = new Vector2(Mathf.Cos(buildingAngle), Mathf.Sin(buildingAngle)) * SquareSize;
                        Vector2 buildingVector2 = new Vector2(buildingVector1.y, 0f - buildingVector1.x);
                        buildingVector1 *= 0.5f * building.Width;
                        buildingVector2 *= 0.5f * building.Length;
                        Vector2 buildingPosition = VectorUtils.XZ(building.m_position);
                        Quad2 tempCorners = default;
                        tempCorners.a = buildingPosition - buildingVector1 - buildingVector2;
                        tempCorners.b = buildingPosition + buildingVector1 - buildingVector2;
                        tempCorners.c = buildingPosition + buildingVector1 + buildingVector2;
                        tempCorners.d = buildingPosition - buildingVector1 + buildingVector2;

                        // square's area must intersect with the building's area
                        if (squareCorners.Intersect(tempCorners))
                        {
                            // found a building at the position, return building corners and subservice
                            buildingCorners = tempCorners;
                            buildingSubservice = building.Info.GetSubService();
                            return true;
                        }
                    }

                    // get the next building from the grid
                    buildingID = building.m_nextGridBuilding;

                    // check for error (e.g. circular reference)
                    if (++buildingCounter >= BuildingManager.MAX_BUILDING_COUNT)
                    {
                        LogUtil.LogError("Invalid list detected!" + Environment.NewLine + Environment.StackTrace);
                        break;
                    }
                }

                // get next building grid cell
                // do in order:  horizontally, vertically, and diagonally adjacent to base grid cell
                gridCellCounter++;
                if (gridCellCounter == 1) { if (                                  loGridIndexX != baseGridIndexX) { gridIndexZ = baseGridIndexZ; gridIndexX = loGridIndexX;   } else gridCellCounter++; }   // left
                if (gridCellCounter == 2) { if (                                  hiGridIndexX != baseGridIndexX) { gridIndexZ = baseGridIndexZ; gridIndexX = hiGridIndexX;   } else gridCellCounter++; }   // right
                if (gridCellCounter == 3) { if (loGridIndexZ != baseGridIndexZ                                  ) { gridIndexZ = loGridIndexZ;   gridIndexX = baseGridIndexX; } else gridCellCounter++; }   // down
                if (gridCellCounter == 4) { if (hiGridIndexZ != baseGridIndexZ                                  ) { gridIndexZ = hiGridIndexZ;   gridIndexX = baseGridIndexX; } else gridCellCounter++; }   // up
                if (gridCellCounter == 5) { if (loGridIndexZ != baseGridIndexZ && loGridIndexX != baseGridIndexX) { gridIndexZ = loGridIndexZ;   gridIndexX = loGridIndexX;   } else gridCellCounter++; }   // down left
                if (gridCellCounter == 6) { if (loGridIndexZ != baseGridIndexZ && hiGridIndexX != baseGridIndexX) { gridIndexZ = loGridIndexZ;   gridIndexX = hiGridIndexX;   } else gridCellCounter++; }   // down right
                if (gridCellCounter == 7) { if (hiGridIndexZ != baseGridIndexZ && loGridIndexX != baseGridIndexX) { gridIndexZ = hiGridIndexZ;   gridIndexX = loGridIndexX;   } else gridCellCounter++; }   // up left
                if (gridCellCounter == 8) { if (hiGridIndexZ != baseGridIndexZ && hiGridIndexX != baseGridIndexX) { gridIndexZ = hiGridIndexZ;   gridIndexX = hiGridIndexX;   } else gridCellCounter++; }   // up right
            }

            // building not found
            return false;
        }

        /// <summary>
        /// display all square counts
        /// </summary>
        private void DisplaySquareCounts()
        {
            // manager must be ready
            if (!UnlockManager.exists)
            {
                return;
            }

            // set district dropdown
            UnlockManager instance = UnlockManager.instance;
            if (instance.Unlocked(UnlockManager.Feature.Districts))
            {
                if (!_district.isEnabled)
                {
                    _district.isEnabled = true;
                    _district.Invalidate();
                }
            }
            else
            {
                if (_district.isEnabled)
                {
                    _district.isEnabled = false;
                    _district.Invalidate();
                }
            }

            // get whether or not to format as percent
            bool formatAsPercent = IsCheckBoxChecked(_percentCheckbox);

            // when Unzoned feature is unlocked, enable Include Unzoned, but only once
            if (instance.Unlocked(ItemClass.Zone.Unzoned) && _includeUnzonedCheckbox.isVisible == false)
            {
                _includeUnzonedCheckbox.isVisible = true;
                _includeUnzonedLabel.isVisible = true;
                SetCheckBox(_includeUnzonedCheckbox, true);
                _includeUnzonedCheckbox.eventClicked += _includeUnzonedCheckbox_eventClicked;
                _includeUnzonedLabel.eventClicked += _includeUnzonedCheckbox_eventClicked;
            }

            // get totals for the selected district
            int selectedDistrict = _district.selectedDistrictID;
            SquareCount totalSquareCount = _finalSquareCounts[(int)Zone.Total];
            int totalBuilt = totalSquareCount.built[selectedDistrict];
            int totalEmpty = totalSquareCount.empty[selectedDistrict];
            int totalTotal = totalSquareCount.total[selectedDistrict];

            // if Include Unzoned is unchecked, then subtract Unzoned
            bool includeUnzoned = IsCheckBoxChecked(_includeUnzonedCheckbox);
            if (!includeUnzoned)
            {
                SquareCount unzonedSquareCount = _finalSquareCounts[(int)Zone.Unzoned];
                totalBuilt -= unzonedSquareCount.built[selectedDistrict];
                totalEmpty -= unzonedSquareCount.empty[selectedDistrict];
                totalTotal -= unzonedSquareCount.total[selectedDistrict];
            }

            // do each zone
            foreach (Zone zone in Zones)
            {
                // display only if UI is valid
                UISquareCount uiSquareCount = _uiSquareCounts[(int)zone];
                if (uiSquareCount.valid)
                {
                    // display values
                    SquareCount finalSquareCount = _finalSquareCounts[(int)zone];
                    if (zone == Zone.Unzoned && !includeUnzoned)
                    {
                        uiSquareCount.built.text = "---";
                        uiSquareCount.empty.text = "---";
                        uiSquareCount.total.text = "---";
                    }
                    else if (zone == Zone.Total && !includeUnzoned)
                    {
                        uiSquareCount.built.text = FormatValue(formatAsPercent, totalBuilt, totalBuilt);
                        uiSquareCount.empty.text = FormatValue(formatAsPercent, totalEmpty, totalEmpty);
                        uiSquareCount.total.text = FormatValue(formatAsPercent, totalTotal, totalTotal);
                    }
                    else
                    {
                        uiSquareCount.built.text = FormatValue(formatAsPercent, finalSquareCount.built[selectedDistrict], totalBuilt);
                        uiSquareCount.empty.text = FormatValue(formatAsPercent, finalSquareCount.empty[selectedDistrict], totalEmpty);
                        uiSquareCount.total.text = FormatValue(formatAsPercent, finalSquareCount.total[selectedDistrict], totalTotal);
                    }

                    // get whether row should be shown normal or locked
                    bool showNormal = false;
                    if (finalSquareCount.total[selectedDistrict] > 0)
                    {
                        // Total is greater than zero, show normal
                        showNormal = true;
                    }
                    else
                    {
                        // for generic zones, check if zone is unlocked
                        // for specialized zones, check if district policy is unlocked
                        // for total, check if zoning feature is unlocked
                        // if zone/policy/feature is unlocked, show normal
                        switch (zone)
                        {
                            case Zone.ResidentialGenericLow:
                                showNormal = instance.Unlocked(ItemClass.Zone.ResidentialLow);
                                break;
                            case Zone.ResidentialGenericHigh:
                                showNormal = instance.Unlocked(ItemClass.Zone.ResidentialHigh);
                                break;
                            case Zone.ResidentialSelfSuff:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Selfsufficient);
                                break;
                            case Zone.ResidentialWallToWall:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.ResidentialWallToWall);
                                break;
                            case Zone.ResidentialSubtotal:
                                showNormal = instance.Unlocked(ItemClass.Zone.ResidentialLow) ||
                                             instance.Unlocked(ItemClass.Zone.ResidentialHigh) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Selfsufficient) ||
                                             instance.Unlocked(DistrictPolicies.Policies.ResidentialWallToWall);
                                break;

                            case Zone.CommercialGenericLow:
                                showNormal = instance.Unlocked(ItemClass.Zone.CommercialLow);
                                break;
                            case Zone.CommercialGenericHigh:
                                showNormal = instance.Unlocked(ItemClass.Zone.CommercialHigh);
                                break;
                            case Zone.CommercialTourism:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Tourist);
                                break;
                            case Zone.CommercialLeisure:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Leisure);
                                break;
                            case Zone.CommercialOrganic:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Organic);
                                break;
                            case Zone.CommercialWallToWall:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.CommercialWallToWall);
                                break;
                            case Zone.CommercialSubtotal:
                                showNormal = instance.Unlocked(ItemClass.Zone.CommercialLow) ||
                                             instance.Unlocked(ItemClass.Zone.CommercialHigh) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Tourist) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Leisure) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Organic) ||
                                             instance.Unlocked(DistrictPolicies.Policies.CommercialWallToWall);
                                break;

                            case Zone.IndustrialGeneric:
                                showNormal = instance.Unlocked(ItemClass.Zone.Industrial);
                                break;
                            case Zone.IndustrialForestry:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Forest);
                                break;
                            case Zone.IndustrialFarming:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Farming);
                                break;
                            case Zone.IndustrialOre:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Ore);
                                break;
                            case Zone.IndustrialOil:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Oil);
                                break;
                            case Zone.IndustrialSubtotal:
                                showNormal = instance.Unlocked(ItemClass.Zone.Industrial) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Forest) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Farming) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Ore) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Oil);
                                break;

                            case Zone.OfficeGeneric:
                                showNormal = instance.Unlocked(ItemClass.Zone.Office);
                                break;
                            case Zone.OfficeITCluster:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.Hightech);
                                break;
                            case Zone.OfficelWallToWall:
                                showNormal = instance.Unlocked(DistrictPolicies.Policies.OfficeWallToWall);
                                break;
                            case Zone.OfficeSubtotal:
                                showNormal = instance.Unlocked(ItemClass.Zone.Office) ||
                                             instance.Unlocked(DistrictPolicies.Policies.Hightech) ||
                                             instance.Unlocked(DistrictPolicies.Policies.OfficeWallToWall);
                                break;

                            case Zone.Unzoned:
                                showNormal = instance.Unlocked(ItemClass.Zone.Unzoned);
                                break;

                            case Zone.Total:
                                showNormal = instance.Unlocked(UnlockManager.Feature.Zoning);
                                break;
                        }
                    }

                    // set symbol sprite
                    uiSquareCount.symbol.spriteName = (showNormal ? UISquareCount.SpriteNameNormal(zone) : UISquareCount.SpriteNameLocked(zone));

                    // set text color
                    Color32 textColor = (showNormal ? TextColorNormal : TextColorLocked);
                    uiSquareCount.description.textColor = textColor;
                    uiSquareCount.built.textColor = textColor;
                    uiSquareCount.empty.textColor = textColor;
                    uiSquareCount.total.textColor = textColor;
                }
            }

            // counts were refreshed
            _displayCounts = false;
        }

        /// <summary>
        /// format a value as a count or as a percent
        /// </summary>
        private string FormatValue(bool formatAsPercent, int value, int total)
        {
            // format as percent
            if (formatAsPercent)
            {
                float percent = 0f;
                if (total != 0)
                {
                    percent = 100f * value / total;
                }
                return percent.ToString("F0", LocaleManager.cultureInfo) + "%";
            }

            // format as count
            return value.ToString("N0", LocaleManager.cultureInfo);
        }

        /// <summary>
        /// reset temp square counts
        /// </summary>
        private void ResetSquareCounts()
        {
            foreach (Zone zone in Zones)
            {
                _tempSquareCounts[(int)zone].Reset();
            }
        }

    }
}
