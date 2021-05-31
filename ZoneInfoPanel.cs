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

        // define the square types to be counted
        // define as int instead of as enum so they can be used directly as array indexes
        struct SquareType
        {
            // zero based square types
            public const int
                ResidentialGenericLow   = 0,
                ResidentialGenericHigh  = 1,
                ResidentialSelfSuff     = 2,
                ResidentialSubtotal     = 3,

                CommercialGenericLow    = 4,
                CommercialGenericHigh   = 5,
                CommercialTourism       = 6,
                CommercialLeisure       = 7,
                CommercialOrganic       = 8,
                CommercialSubtotal      = 9,

                IndustrialGeneric       = 10,
                IndustrialForestry      = 11,
                IndustrialFarming       = 12,
                IndustrialOre           = 13,
                IndustrialOil           = 14,
                IndustrialSubtotal      = 15,

                OfficeGeneric           = 16,
                OfficeIT                = 17,
                OfficeSubtotal          = 18,

                Unzoned                 = 19,

                Total                   = 20,

                Count                   = 21;   // number of SquareType

            /// <summary>
            /// return whether or not the square type is valid
            /// </summary>
            public static bool IsValid(int value)
            {
                return value >= 0 && value < Count;
            }
        }

        /// <summary>
        /// hold the info for a data row
        /// </summary>
        private class SquareCount
        {
            // +1 to make room for entry for Entire City
            private const int ArraySize = DistrictManager.MAX_DISTRICT_COUNT + 1;

            // the 3 counts for a data row
            // index into each array is district ID
            public int[] Built = new int[ArraySize];
            public int[] Empty = new int[ArraySize];
            public int[] Total = new int[ArraySize];

            /// <summary>
            /// increment the counts for the specified district and for the Entire City
            /// </summary>
            public void Increment(byte districtID, bool occupied)
            {
                // increment either Built or Empty depending on the occupied flag
                if (occupied)
                {
                    Built[districtID]++;
                    Built[DistrictDropdown.DistrictIDEntireCity]++;
                }
                else
                {
                    Empty[districtID]++;
                    Empty[DistrictDropdown.DistrictIDEntireCity]++;
                }

                // always increment the total
                Total[districtID]++;
                Total[DistrictDropdown.DistrictIDEntireCity]++;
            }

            /// <summary>
            /// copy the data from another SquareCount to this one
            /// </summary>
            public void Copy(SquareCount from)
            {
                for (int districtID = 0; districtID < ArraySize; districtID++)
                {
                    Built[districtID] = from.Built[districtID];
                    Empty[districtID] = from.Empty[districtID];
                    Total[districtID] = from.Total[districtID];
                }
            }

            /// <summary>
            /// reset the counts for all districts
            /// </summary>
            public void Reset()
            {
                for (int districtID = 0; districtID < ArraySize; districtID++)
                {
                    Built[districtID] = 0;
                    Empty[districtID] = 0;
                    Total[districtID] = 0;
                }
            }
        }

        // define two square count arrays:
        //    Temp for accumulating current counts
        //    Final for holding the counts to display
        private SquareCount[] _squareCountTemp;
        private SquareCount[] _squareCountFinal;

        /// <summary>
        /// the UI elements to display a SquareCount in a row
        /// </summary>
        private class SquareCountUI
        {
            public UISprite Symbol;
            public UILabel DescriptionLabel;
            public UILabel BuiltLabel;
            public UILabel EmptyLabel;
            public UILabel TotalLabel;
        }
        private SquareCountUI[] _squareCountUI;

        // other UI elements
        private DistrictDropdown _district;
        private SquareCountUI _heading;
        private UISprite _countCheckbox;
        private UILabel  _countLabel;
        private UISprite _percentCheckbox;
        private UILabel  _percentLabel;
        private UIFont _defaultFont;

        // common UI properties
        private const float DistrictHeight = 45f;
        private const float LeftPadding = 8f;
        private const float ItemHeight = 17f;
        private readonly Color32 TextColor = new Color32(185, 221, 254, 255);
        private const float TextScale = 0.75f;

        // size of one side of a square
        const float SquareSize = 8f;


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

                // get game atlases
                UITextureAtlas ingameAtlas = null;
                UITextureAtlas thumbnailsAtlas = null;
                UITextureAtlas[] atlases = Resources.FindObjectsOfTypeAll(typeof(UITextureAtlas)) as UITextureAtlas[];
                foreach (UITextureAtlas atlas in atlases)
                {
                    if (atlas != null)
                    {
                        if (atlas.name == "Ingame")
                        {
                            ingameAtlas = atlas;
                        }
                        if (atlas.name == "Thumbnails")
                        {
                            thumbnailsAtlas = atlas;
                        }
                    }
                }
                if (ingameAtlas == null)
                {
                    Debug.LogError("Unable to find Ingame atlas.");
                    return;
                }
                if (thumbnailsAtlas == null)
                {
                    Debug.LogError("Unable to find Thumbnails atlas.");
                    return;
                }

                // add district dropdown
                _district = AddUIComponent<DistrictDropdown>();
                if (_district == null || !_district.initialized)
                {
                    Debug.LogError($"Unable to create district dropdown on panel [{name}].");
                    return;
                }
                _district.name = "DistrictPanel";
                _district.text = "District:";
                _district.relativePosition = new Vector3(LeftPadding, 45f);
                _district.dropdownHeight = ItemHeight + 7f;
                _district.font = _defaultFont;
                _district.textScale = TextScale;
                _district.textColor = TextColor;
                _district.listHeight = 10 * (int)ItemHeight + 8;
                _district.itemHeight = (int)ItemHeight;
                _district.builtinKeyNavigation = true;
                _district.eventSelectedDistrictChanged += SelectedDistrictChanged;

                // create heading row
                float top = 50f;            // start just below title bar of panel
                top += DistrictHeight;      // skip over district dropdown
                _heading = new SquareCountUI();
                if (!CreateSquareCount(true, ref top, ingameAtlas, "", "Heading", "", _heading)) return;
                _heading.BuiltLabel.text = "Built";
                _heading.EmptyLabel.text = "Empty";
                _heading.TotalLabel.text = "Total";
                _heading.BuiltLabel.tooltip = "Built Squares";
                _heading.EmptyLabel.tooltip = "Empty Squares";
                _heading.TotalLabel.tooltip = "Built + Empty Squares";

                // create a line under each count heading
                if (!CreateLine(_heading.BuiltLabel)) return;
                if (!CreateLine(_heading.EmptyLabel)) return;
                if (!CreateLine(_heading.TotalLabel)) return;

                // create two checkboxes left of the heading
                if (!CreateCheckbox(ref _countCheckbox, ref _countLabel, "Count", 16f, 50f)) return;
                if (!CreateCheckbox(ref _percentCheckbox, ref _percentLabel, "Percent", _countLabel.relativePosition.x + _countLabel.size.x + LeftPadding, 70f)) return;

                // initialize check boxes so that Count is checked by default
                SetCheckBox(_countCheckbox, true);
                SetCheckBox(_percentCheckbox, false);

                // set event handlers for count and percent
                _countCheckbox.eventClicked   += CheckBox_eventClicked;
                _countLabel.eventClicked      += CheckBox_eventClicked;
                _percentCheckbox.eventClicked += CheckBox_eventClicked;
                _percentLabel.eventClicked    += CheckBox_eventClicked;

                // create a square count for each SquareType
                _squareCountTemp  = new SquareCount  [SquareType.Count];
                _squareCountFinal = new SquareCount  [SquareType.Count];
                _squareCountUI    = new SquareCountUI[SquareType.Count];
                for (int squareType = 0; squareType < SquareType.Count; squareType++)
                {
                    _squareCountTemp [squareType] = new SquareCount();
                    _squareCountFinal[squareType] = new SquareCount();
                    _squareCountUI   [squareType] = new SquareCountUI();
                }

                // get DLC flags
                bool dlcBaseGame    = true;
                bool dlcAfterDark   = SteamHelper.IsDLCOwned(SteamHelper.DLC.AfterDarkDLC);
                bool dlcGreenCities = SteamHelper.IsDLCOwned(SteamHelper.DLC.GreenCitiesDLC);

                // create each square count row based on DLC
                // only the final square counts get the UI elements
                top += 5f;
                const float SectionSpacing = 10f;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningResidentialLow",                  "ResidentialLow",      "Residential Low Density",  _squareCountUI[SquareType.ResidentialGenericLow ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningResidentialHigh",                 "ResidentialHigh",     "Residential High Density", _squareCountUI[SquareType.ResidentialGenericHigh])) return;
                if (!CreateSquareCount(dlcGreenCities, ref top, ingameAtlas,     "IconPolicySelfsufficient",              "ResidentialSelfSuff", "Residential Self-Suff",    _squareCountUI[SquareType.ResidentialSelfSuff   ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "DistrictSpecializationResidentialNone", "ResidentialSubTotal", "Residential Subtotal",     _squareCountUI[SquareType.ResidentialSubtotal   ])) return;

                top += SectionSpacing;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningCommercialLow",                   "CommercialLow",       "Commercial Low Density",   _squareCountUI[SquareType.CommercialGenericLow  ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningCommercialHigh",                  "CommercialHigh",      "Commercial High Density",  _squareCountUI[SquareType.CommercialGenericHigh ])) return;
                if (!CreateSquareCount(dlcAfterDark,   ref top, ingameAtlas,     "IconPolicyTourist",                     "CommercialTourism",   "Commercial Tourism",       _squareCountUI[SquareType.CommercialTourism     ])) return;
                if (!CreateSquareCount(dlcAfterDark,   ref top, ingameAtlas,     "IconPolicyLeisure",                     "CommercialLeisure",   "Commercial Leisure",       _squareCountUI[SquareType.CommercialLeisure     ])) return;
                if (!CreateSquareCount(dlcGreenCities, ref top, ingameAtlas,     "IconPolicyOrganic",                     "CommercialOrganic",   "Commercial Organic",       _squareCountUI[SquareType.CommercialOrganic     ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "DistrictSpecializationCommercialNone",  "CommercialSubTotal",  "Commercial Subtotal",      _squareCountUI[SquareType.CommercialSubtotal    ])) return;
                top += SectionSpacing;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningIndustrial",                      "IndustrialGeneric",   "Industrial Generic",       _squareCountUI[SquareType.IndustrialGeneric     ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, ingameAtlas,     "IconPolicyForest",                      "IndustrialForestry",  "Industrial Forestry",      _squareCountUI[SquareType.IndustrialForestry    ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, ingameAtlas,     "IconPolicyFarming",                     "IndustrialFarming",   "Industrial Farming",       _squareCountUI[SquareType.IndustrialFarming     ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, ingameAtlas,     "IconPolicyOre",                         "IndustrialOre",       "Industrial Ore",           _squareCountUI[SquareType.IndustrialOre         ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, ingameAtlas,     "IconPolicyOil",                         "IndustrialOil",       "Industrial Oil",           _squareCountUI[SquareType.IndustrialOil         ])) return;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "DistrictSpecializationNone",            "IndustrialSubTotal",  "Industrial Subtotal",      _squareCountUI[SquareType.IndustrialSubtotal    ])) return;

                top += SectionSpacing;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningOffice",                          "OfficeGeneric",       "Office Generic",           _squareCountUI[SquareType.OfficeGeneric         ])) return;
                if (!CreateSquareCount(dlcGreenCities, ref top, ingameAtlas,     "IconPolicyHightech",                    "OfficeITCluster",     "Office IT Cluster",        _squareCountUI[SquareType.OfficeIT              ])) return;
                if (!CreateSquareCount(dlcGreenCities, ref top, thumbnailsAtlas, "DistrictSpecializationOfficeNone",      "OfficeSubTotal",      "Office Subtotal",          _squareCountUI[SquareType.OfficeSubtotal        ])) return;

                top += SectionSpacing;
                if (!CreateSquareCount(dlcBaseGame,    ref top, thumbnailsAtlas, "ZoningUnzoned",                         "Unzoned",             "Unzoned",                  _squareCountUI[SquareType.Unzoned               ])) return;

                top += SectionSpacing;
                if (!CreateSquareCount(dlcBaseGame,    ref top, ingameAtlas,     "ToolbarIconZoning",                     "Total",               "Total",                    _squareCountUI[SquareType.Total                 ])) return;

                // set panel size based on size and position of total row
                SquareCountUI totalRow = _squareCountUI[SquareType.Total];
                size = new Vector3(
                    totalRow.TotalLabel.relativePosition.x + totalRow.TotalLabel.size.x + totalRow.Symbol.relativePosition.x,
                    totalRow.TotalLabel.relativePosition.y + totalRow.TotalLabel.size.y + 5f);
                _district.size = new Vector2(width - 2f * LeftPadding, DistrictHeight);

                // create icon in upper left
                UISprite panelIcon = AddUIComponent<UISprite>();
                if (panelIcon == null)
                {
                    Debug.LogError($"Unable to create icon on panel [{name}].");
                    return;
                }
                panelIcon.name = "Icon";
                panelIcon.autoSize = false;
                panelIcon.size = new Vector2(36f, 36f);
                panelIcon.relativePosition = new Vector3(10f, 2f);
                UITextureAtlas buttonAtlas = TextureUtil.GenerateLinearAtlas(ZoneInfoActivationButton.ActivationButtonAtlas, TextureUtil.ActivationButtonTexture2D, 4,
                    new string[] { ZoneInfoActivationButton.ForegroundSprite, ZoneInfoActivationButton.BackgroundSpriteNormal, ZoneInfoActivationButton.BackgroundSpriteHovered, ZoneInfoActivationButton.BackgroundSpriteFocused });
                panelIcon.atlas = buttonAtlas;
                panelIcon.spriteName = ZoneInfoActivationButton.ForegroundSprite;
                panelIcon.isVisible = true;

                // create the title label
                UILabel title = AddUIComponent<UILabel>();
                if (title == null)
                {
                    Debug.LogError($"Unable to create title label on [{name}].");
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
                    Debug.LogError($"Unable to create close button on panel [{name}].");
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
                    Debug.LogError($"Unable to create drag handle on [{name}].");
                    return;
                }
                dragHandle.name = "DragHandle";
                dragHandle.relativePosition = Vector3.zero;
                dragHandle.size = new Vector3(size.x, 40f);

                // make sure drag handle is in front of icon and title
                // make sure close button is in front of drag handle
                dragHandle.BringToFront();
                closeButton.BringToFront();

                // update loop is not stopped
                _stopUpdate = false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// create a square count row
        /// </summary>
        private bool CreateSquareCount(bool dlcActive, ref float top, UITextureAtlas atlas, string spriteName, string namePrefix, string text, SquareCountUI squareCountUI)
        {
            // skip if DLC is not active
            if (!dlcActive)
            {
                return true;
            }

            // add the symbol sprite
            squareCountUI.Symbol = AddUIComponent<UISprite>();
            if (squareCountUI.Symbol == null)
            {
                Debug.LogError($"Unable to add symbol sprite for {text} on {name}.");
                return false;
            }
            squareCountUI.Symbol.name = namePrefix + "Symbol";
            squareCountUI.Symbol.autoSize = false;
            squareCountUI.Symbol.size = new Vector2(ItemHeight, ItemHeight);    // width is same as height
            squareCountUI.Symbol.relativePosition = new Vector3(LeftPadding, top - 2f);  // -2 to align properly with text in labels
            squareCountUI.Symbol.atlas = atlas;
            squareCountUI.Symbol.spriteName = spriteName;
            squareCountUI.Symbol.isVisible = true;

            // add the labels
            const float CountWidth = 67f;
            const string DefaultCountText = "0,000,000";
            if (!CreateLabel(ref squareCountUI.DescriptionLabel, squareCountUI.Symbol,           top, 170f,       ItemHeight, namePrefix + "Description", text            )) return false;
            if (!CreateLabel(ref squareCountUI.BuiltLabel,       squareCountUI.DescriptionLabel, top, CountWidth, ItemHeight, namePrefix + "Built",       DefaultCountText)) return false;
            if (!CreateLabel(ref squareCountUI.EmptyLabel,       squareCountUI.BuiltLabel,       top, CountWidth, ItemHeight, namePrefix + "Empty",       DefaultCountText)) return false;
            if (!CreateLabel(ref squareCountUI.TotalLabel,       squareCountUI.EmptyLabel,       top, CountWidth, ItemHeight, namePrefix + "Total",       DefaultCountText)) return false;
            squareCountUI.DescriptionLabel.textAlignment = UIHorizontalAlignment.Left;

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
                Debug.LogError($"Unable to add {name} label on {name}.");
                return false;
            }
            label.name = name;
            label.font = _defaultFont;
            label.text = text;
            label.textAlignment = UIHorizontalAlignment.Right;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textScale = TextScale;
            label.textColor = TextColor;
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
                Debug.LogError($"Unable to create line under [{label.name}] on [{name}].");
                return false;
            }
            line.name = label.name + "Line";
            line.autoSize = false;
            line.size = new Vector2(label.width, 1f);
            line.relativePosition = new Vector3(1f, label.size.y);
            line.spriteName = "EmptySprite";
            line.color = TextColor;
            line.isVisible = true;
            
            // success
            return true;
        }

        /// <summary>
        /// create a check box and corresponding label
        /// </summary>
        private bool CreateCheckbox(ref UISprite checkbox, ref UILabel label, string text, float x, float width)
        {
            // create check box
            checkbox = AddUIComponent<UISprite>();
            if (checkbox == null)
            {
                Debug.LogError($"Unable to create {text} checkbox sprite on {name}.");
                return false;
            }
            checkbox.name = text + "Checkbox";
            checkbox.autoSize = false;
            checkbox.size = _heading.Symbol.size;
            checkbox.relativePosition = new Vector3(x, _heading.Symbol.relativePosition.y);
            checkbox.isVisible = true;

            // create label for check box
            label = AddUIComponent<UILabel>();
            if (label == null)
            {
                Debug.LogError($"Unable to create {text} checkbox label on {name}.");
                return false;
            }
            label.name = text + "Label";
            label.font = _defaultFont;
            label.text = text;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textScale = TextScale;
            label.textColor = TextColor;
            label.autoSize = false;
            label.size = new Vector2(width, _heading.DescriptionLabel.size.y);
            label.relativePosition = new Vector3(checkbox.relativePosition.x + checkbox.size.x + 4f, _heading.DescriptionLabel.relativePosition.y);
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
            // when panel becomes visible, immediately count all
            if (value)
            {
                _countAll = true;
            }
        }

        /// <summary>
        /// handle click on Close button
        /// </summary>
        private void CloseButton_eventClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            // toggle panel visibility
            ZoneInfoLoading.TogglePanelVisibility();
        }

        /// <summary>
        /// handle clicks on check boxes
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
                if (!ZoneManager.exists || !DistrictManager.exists)
                {
                    return;
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

                // get DistrictManager
                DistrictManager instance = DistrictManager.instance;

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
                                    int countToIncrement    = -1;
                                    int subtotalToIncrement = -1;
                                    switch (zone)
                                    {
                                        case ItemClass.Zone.ResidentialLow:
                                        case ItemClass.Zone.ResidentialHigh:
                                            if      ((specialization & DistrictPolicies.Specialization.Selfsufficient) != 0) { countToIncrement = SquareType.ResidentialSelfSuff;    }
                                            else if (zone == ItemClass.Zone.ResidentialLow)                                  { countToIncrement = SquareType.ResidentialGenericLow;  }
                                            else if (zone == ItemClass.Zone.ResidentialHigh)                                 { countToIncrement = SquareType.ResidentialGenericHigh; }
                                            subtotalToIncrement = SquareType.ResidentialSubtotal;
                                            break;

                                        case ItemClass.Zone.CommercialLow:
                                        case ItemClass.Zone.CommercialHigh:
                                            if      ((specialization & DistrictPolicies.Specialization.Tourist) != 0) { countToIncrement = SquareType.CommercialTourism;     }
                                            else if ((specialization & DistrictPolicies.Specialization.Leisure) != 0) { countToIncrement = SquareType.CommercialLeisure;     }
                                            else if ((specialization & DistrictPolicies.Specialization.Organic) != 0) { countToIncrement = SquareType.CommercialOrganic;     }
                                            else if (zone == ItemClass.Zone.CommercialLow)                            { countToIncrement = SquareType.CommercialGenericLow;  }
                                            else if (zone == ItemClass.Zone.CommercialHigh)                           { countToIncrement = SquareType.CommercialGenericHigh; }
                                            subtotalToIncrement = SquareType.CommercialSubtotal;
                                            break;

                                        case ItemClass.Zone.Industrial:
                                            if      ((specialization & DistrictPolicies.Specialization.Forest ) != 0) { countToIncrement = SquareType.IndustrialForestry; }
                                            else if ((specialization & DistrictPolicies.Specialization.Farming) != 0) { countToIncrement = SquareType.IndustrialFarming;  }
                                            else if ((specialization & DistrictPolicies.Specialization.Ore    ) != 0) { countToIncrement = SquareType.IndustrialOre;      }
                                            else if ((specialization & DistrictPolicies.Specialization.Oil    ) != 0) { countToIncrement = SquareType.IndustrialOil;      }
                                            else                                                                      { countToIncrement = SquareType.IndustrialGeneric;  }
                                            subtotalToIncrement = SquareType.IndustrialSubtotal;
                                            break;

                                        case ItemClass.Zone.Office:
                                            if ((specialization & DistrictPolicies.Specialization.Hightech) != 0) { countToIncrement = SquareType.OfficeIT;      }
                                            else                                                                  { countToIncrement = SquareType.OfficeGeneric; }
                                            subtotalToIncrement = SquareType.OfficeSubtotal;
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
                                                        case ItemClass.SubService.ResidentialHighEco:   countToIncrement = SquareType.ResidentialSelfSuff;    subtotalToIncrement = SquareType.ResidentialSubtotal; break;
                                                        case ItemClass.SubService.ResidentialLow:       countToIncrement = SquareType.ResidentialGenericLow;  subtotalToIncrement = SquareType.ResidentialSubtotal; break;
                                                        case ItemClass.SubService.ResidentialHigh:      countToIncrement = SquareType.ResidentialGenericHigh; subtotalToIncrement = SquareType.ResidentialSubtotal; break;

                                                        case ItemClass.SubService.CommercialTourist:    countToIncrement = SquareType.CommercialTourism;      subtotalToIncrement = SquareType.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialLeisure:    countToIncrement = SquareType.CommercialLeisure;      subtotalToIncrement = SquareType.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialEco:        countToIncrement = SquareType.CommercialOrganic;      subtotalToIncrement = SquareType.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialLow:        countToIncrement = SquareType.CommercialGenericLow;   subtotalToIncrement = SquareType.CommercialSubtotal;  break;
                                                        case ItemClass.SubService.CommercialHigh:       countToIncrement = SquareType.CommercialGenericHigh;  subtotalToIncrement = SquareType.CommercialSubtotal;  break;

                                                        case ItemClass.SubService.PlayerIndustryForestry:
                                                        case ItemClass.SubService.IndustrialForestry:   countToIncrement = SquareType.IndustrialForestry;     subtotalToIncrement = SquareType.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryFarming:
                                                        case ItemClass.SubService.IndustrialFarming:    countToIncrement = SquareType.IndustrialFarming;      subtotalToIncrement = SquareType.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryOre:
                                                        case ItemClass.SubService.IndustrialOre:        countToIncrement = SquareType.IndustrialOre;          subtotalToIncrement = SquareType.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.PlayerIndustryOil:
                                                        case ItemClass.SubService.IndustrialOil:        countToIncrement = SquareType.IndustrialOil;          subtotalToIncrement = SquareType.IndustrialSubtotal;  break;
                                                        case ItemClass.SubService.IndustrialGeneric:    countToIncrement = SquareType.IndustrialGeneric;      subtotalToIncrement = SquareType.IndustrialSubtotal;  break;

                                                        case ItemClass.SubService.OfficeHightech:       countToIncrement = SquareType.OfficeIT;               subtotalToIncrement = SquareType.OfficeSubtotal;      break;
                                                        case ItemClass.SubService.OfficeGeneric:        countToIncrement = SquareType.OfficeGeneric;          subtotalToIncrement = SquareType.OfficeSubtotal;      break;

                                                        default:
                                                            // building is not a subservice being counted
                                                            // building could be a service building, park, or other structure that causes the square to be unzoned
                                                            // this is not an error, just count as unzoned
                                                            countToIncrement = SquareType.Unzoned;
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    // no building found even though zone block indicates occupied
                                                    // this is not an error, just count as unzoned
                                                    countToIncrement = SquareType.Unzoned;
                                                }
                                            }
                                            else
                                            {
                                                // unoccupied always gets counted as unzoned
                                                countToIncrement = SquareType.Unzoned;
                                            }
                                            break;

                                        case ItemClass.Zone.None:
                                        case ItemClass.Zone.Distant:
                                            // ignore, should never get here
                                            break;
                                    }

                                    // increment the square count (if valid), subtotal (if valid), and total (always)
                                    if (SquareType.IsValid(countToIncrement   )) _squareCountTemp[countToIncrement   ].Increment(districtID, occupied);
                                    if (SquareType.IsValid(subtotalToIncrement)) _squareCountTemp[subtotalToIncrement].Increment(districtID, occupied);
                                                                                 _squareCountTemp[SquareType.Total   ].Increment(districtID, occupied);
                                }
                            }
                        }
                    }
                }

                // check if went thru all blocks
                if (_blockCounter >= blocks.Length)
                {
                    // copy temp to final
                    for (int squareType = 0; squareType < SquareType.Count; squareType++)
                    {
                        _squareCountFinal[squareType].Copy(_squareCountTemp[squareType]);
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
                Debug.LogException(ex);
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
                        Debug.LogError("Invalid list detected!" + Environment.NewLine + Environment.StackTrace);
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
            // get whether or not to format as percent
            bool formatAsPercent = IsCheckBoxChecked(_percentCheckbox);

            // get totals for the selected district
            int selectedDistrict = _district.selectedDistrictID;
            SquareCount totalSquareCount = _squareCountFinal[SquareType.Total];
            int totalBuilt = totalSquareCount.Built[selectedDistrict];
            int totalEmpty = totalSquareCount.Empty[selectedDistrict];
            int totalTotal = totalSquareCount.Total[selectedDistrict];

            // display each square count for the selected district
            for (int squareType = 0; squareType < SquareType.Count; squareType++)
            {
                SquareCountUI countUI = _squareCountUI[squareType];
                SquareCount finalSquareCount = _squareCountFinal[squareType];
                if (countUI.BuiltLabel != null) countUI.BuiltLabel.text = FormatValue(formatAsPercent, finalSquareCount.Built[selectedDistrict], totalBuilt);
                if (countUI.EmptyLabel != null) countUI.EmptyLabel.text = FormatValue(formatAsPercent, finalSquareCount.Empty[selectedDistrict], totalEmpty);
                if (countUI.TotalLabel != null) countUI.TotalLabel.text = FormatValue(formatAsPercent, finalSquareCount.Total[selectedDistrict], totalTotal);
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
            for (int squareType = 0; squareType < SquareType.Count; squareType++)
            {
                _squareCountTemp[squareType].Reset();
            }
        }

    }
}
