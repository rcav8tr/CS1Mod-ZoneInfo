using ColossalFramework.UI;
using System;
using UnityEngine;

namespace ZoneInfo
{
    /// <summary>
    /// a district dropdown selector
    /// </summary>
    public class DistrictDropdown : UIPanel
    {
        // custom event
        public event EventHandler<SelectedDistrictChangedEventArgs> eventSelectedDistrictChanged;

        // UI elements
        private UIPanel _panel;
        private UILabel _label;
        private UIDropDown _dropdown;

        // district data
        private byte[] _districtIDs;
        private int _districtCount;
        private byte _selectedDistrictID;

        // define special district IDs
        // in many parts of the game logic, data for district 0 means data for the entire city
        // here, district 0 means no district, which is different than entire city
        public const byte DistrictIDNoDistrict = 0;
        public const byte DistrictIDEntireCity = DistrictManager.MAX_DISTRICT_COUNT;

        // controls for Update
        private uint _framecounter = 0;
        private bool _updateNow = false;

        /// <summary>
        /// initialize the district dropdown
        /// </summary>
        public DistrictDropdown()
        {
            // create a new dropdown from the template
            // the template is a UIPanel that contains a UILabel and a UIDropdown
            _panel = AttachUIComponent(UITemplateManager.GetAsGameObject("OptionsDropdownTemplate")) as UIPanel;
            if (_panel == null)
            {
                throw new TypeLoadException("Unable to attach component from template [OptionsDropdownTemplate].");
            }
            _panel.relativePosition = Vector3.zero;
            _panel.size = size;

            // get label on the panel
            _label = _panel.Find<UILabel>("Label");
            if (_label == null)
            {
                throw new TypeLoadException($"Unable to find component [Label] on panel [{_panel.name}].");
            }

            // get dropdown on the panel
            _dropdown = _panel.Find<UIDropDown>("Dropdown");
            if (_dropdown == null)
            {
                throw new TypeLoadException($"Unable to find component [Dropdown] on panel [{_panel.name}]");
            }
            _dropdown.autoSize = false;
            _dropdown.width = width;
            _dropdown.eventSelectedIndexChanged += _dropdown_eventSelectedIndexChanged;

            // populate dropdown with initial districts
            GetDistricts(out _districtIDs, out string[] names, out _districtCount);
            UpdateDropdownList(names, _districtCount);

            // by default, first entry (Entire City) is selected
            _selectedDistrictID = DistrictIDEntireCity;

            // successfully initialized
            _initialized = true;
        }

        // expose some properties that access the underlying components
        public new bool builtinKeyNavigation
        {
            get { return _dropdown.builtinKeyNavigation; }
            set { _dropdown.builtinKeyNavigation = value; }
        }

        public UIDropDown dropdown
        {
            get { return _dropdown; }
        }

        public float dropdownHeight
        {
            get { return _dropdown.height; }
            set { _dropdown.height = value; }
        }

        public UIFont font
        {
            get { return _label.font; }
            set { _label.font = value; _dropdown.font = value; }
        }

        /// <summary>
        /// this flag must be checked after instantiation to ensure the dropdown was successfully initialized
        /// </summary>
        private bool _initialized = false;
        public bool initialized
        {
            get { return _initialized; }
        }

        public int itemHeight
        {
            get { return _dropdown.itemHeight; }
            set { _dropdown.itemHeight = value; }
        }

        public string[] items
        {
            get { return _dropdown.items; }
            set { _dropdown.items = value; }
        }

        public UILabel label
        {
            get { return _label; }
        }

        public int listHeight
        {
            get { return _dropdown.listHeight; }
            set { _dropdown.listHeight = value; }
        }

        public string text
        {
            get { return _label.text;  }
            set { _label.text = value; }
        }

        public byte selectedDistrictID
        {
            get { return _selectedDistrictID; }
            set
            {
                // find district ID and select that entry in the dropdown
                for (int i = 0; i < _districtCount; i++)
                {
                    if (_districtIDs[i] == value)
                    {
                        _dropdown.selectedIndex = i;
                        return;
                    }
                }

                // if got here then district ID was not found
                throw new ArgumentOutOfRangeException("selectedDistrictID", $"District ID [{value}] is not defined.");
            }
        }

        public string selectedDistrictName
        {
            get { return _dropdown.selectedValue; }
            set { _dropdown.selectedValue = value; }
        }

        public Color32 textColor
        {
            get { return _label.textColor; }
            set { _label.textColor = value; _dropdown.textColor = value; }
        }

        public float textScale
        {
            get { return _label.textScale; }
            set { _label.textScale = value; _dropdown.textScale = value; }
        }

        /// <summary>
        /// when this component is resized:  resize child components
        /// </summary>
        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            if (_initialized)
            {
                _panel.size = size;
                _label.width = size.x;
                _dropdown.width = size.x;
            }
        }

        /// <summary>
        /// when this component is made visible:  update the dropdown immediately
        /// </summary>
        protected override void OnVisibilityChanged()
        {
            base.OnVisibilityChanged();

            if (isVisible)
            {
                _updateNow = true;
            }
        }

        /// <summary>
        /// convert dropdown index changed event to a possible SelectedDistrictChanged event
        /// </summary>
        private void _dropdown_eventSelectedIndexChanged(UIComponent component, int value)
        {
            // check if district ID changed
            byte newDistrictID = _districtIDs[value];
            if (newDistrictID != _selectedDistrictID)
            {
                // save selected district ID
                _selectedDistrictID = newDistrictID;

                // raise SelectedDistrictChanged event
                OnSelectedDistrictChanged(new SelectedDistrictChangedEventArgs(newDistrictID));
            }
        }

        /// <summary>
        /// raise the SelectedDistrictChanged event
        /// </summary>
        protected virtual void OnSelectedDistrictChanged(SelectedDistrictChangedEventArgs args)
        {
            eventSelectedDistrictChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Update is called every frame
        /// check for and handle changes in the districts
        /// </summary>
        public override void Update()
        {
            // do base processing
            base.Update();

            // must be initialized
            if (!_initialized)
                return;

            // must be visible and enabled (performance enhancement)
            if (!isVisible || !enabled)
                return;

            // manager must be ready
            if (!DistrictManager.exists)
                return;

            // update only every 5th frame (performance enhancement) unless need to update now
            ++_framecounter;
            if (!(_framecounter % 5 == 0 || _updateNow))
                return;
            _updateNow = false;

            // check for a change in districts
            GetDistricts(out byte[] IDs, out string[] names, out int count);
            bool districtsChanged = false;
            if (count != _districtCount)
            {
                // a change in count is obviously a change (i.e. a district was added or removed)
                districtsChanged = true;
            }
            else
            {
                // arrays have same count
                // check for a change in district ID at each index (e.g. one district was replaced with another district)
                for (int i = 0; i < count; i++)
                {
                    if (IDs[i] != _districtIDs[i])
                    {
                        districtsChanged = true;
                        break;
                    }
                }
            }

            // check if districts changed
            if (districtsChanged)
            {
                // update dropdown list
                UpdateDropdownList(names, count);

                // save new district info
                _districtIDs = IDs;
                _districtCount = count;

                // find index of last selected district ID in the new list
                int newSelectedIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    if (_selectedDistrictID == IDs[i])
                    {
                        newSelectedIndex = i;
                        break;
                    }
                }

                // check if last selected district ID is in the new list
                if (newSelectedIndex == -1)
                {
                    // last selected district ID is not in the new list (i.e. selected district was removed)
                    // select the first entry in the list
                    // this will trigger the index changed event which will raise the SelectedDistrictChanged event (as desired)
                    _dropdown.selectedIndex = 0;
                }
                else
                {
                    // last selected district ID is in the new list
                    // check if index changed
                    if (_dropdown.selectedIndex != newSelectedIndex)
                    {
                        // set the new dropdown index so the same district ID remains selected
                        // this will trigger the index changed event, but the SelectedDistrictChanged event will not be raised (as desired) because the selected district ID did not change
                        _dropdown.selectedIndex = newSelectedIndex;
                    }
                }
            }
            else
            {
                // no district ID change

                // update changed names in dropdown list
                // this also automatically updates selectedValue in the dropdown
                bool nameChanged = false;
                for (int i = 0; i < count; i++)
                {
                    if (_dropdown.items[i] != names[i])
                    {
                        _dropdown.items[i] = names[i];
                        nameChanged = true;
                    }
                }

                // if a name changed, refresh the dropdown
                if (nameChanged)
                {
                    _dropdown.Invalidate();
                }
            }
        }

        /// <summary>
        /// get the current district IDs and names
        /// </summary>
        private void GetDistricts(out byte[] IDs, out string[] names, out int count)
        {
            // create return arrays
            // index into arrays is dropdown selected index
            // +1 to make room for Entire City entry
            IDs   = new byte  [DistrictManager.MAX_DISTRICT_COUNT + 1];
            names = new string[DistrictManager.MAX_DISTRICT_COUNT + 1];
            count = 0;

            // always include first entry for Entire City
            IDs[count] = DistrictIDEntireCity;
            names[count] = "Entire City";
            count++;

            // loop over each district
            // skip district ID 0, which represents No District and is added only if there is another district
            DistrictManager instance = DistrictManager.instance;
            bool entryNoDistrictAdded = false;
            for (byte districtID = 1; districtID < DistrictManager.MAX_DISTRICT_COUNT; districtID++)
            {
                // district must be created
                District district = instance.m_districts.m_buffer[districtID];
                if ((district.m_flags & District.Flags.Created) != 0)
                {
                    // if entry for No Distrct was not yet added, add it now
                    if (!entryNoDistrictAdded)
                    {
                        IDs[count] = DistrictIDNoDistrict;
                        names[count] = "No District";
                        count++;
                        entryNoDistrictAdded = true;
                    }

                    // add the district
                    IDs[count] = districtID;
                    names[count] = instance.GetDistrictName(districtID);
                    count++;
                }
            }

            // alphabetize by name, except for Entire City entry and No District entry (if present)
            for (int i = (entryNoDistrictAdded ? 2 : 1); i < count - 1; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (names[j].CompareTo(names[i]) < 0)
                    {
                        // swap IDs and names
                        byte tempID = IDs[i];
                        IDs[i] = IDs[j];
                        IDs[j] = tempID;
                        string tempName = names[i];
                        names[i] = names[j];
                        names[j] = tempName;
                    }
                }
            }
        }

        /// <summary>
        /// update the district names in the dropdown list
        /// </summary>
        private void UpdateDropdownList(string[] names, int count)
        {
            // construct a new array of the exact needed size
            string[] newItems = new string[count];
            for (int i = 0; i < count; i++)
            {
                newItems[i] = names[i];
            }

            // use the new array
            _dropdown.items = newItems;
            _dropdown.Invalidate();
        }
    }

    /// <summary>
    /// arguments for SelectedDistrictChanged event
    /// </summary>
    public class SelectedDistrictChangedEventArgs : EventArgs
    {
        public byte districtID { get; }
        private SelectedDistrictChangedEventArgs() { }
        public SelectedDistrictChangedEventArgs(byte districtID)
        {
            this.districtID = districtID;
        }
    }
}
