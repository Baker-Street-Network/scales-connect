# PAX COM Port Persistence Fix

## Issue
The selected COM port in the serial port dropdown was not being saved/restored between application restarts. Each time the app started, it would default to the first available COM port instead of remembering the user's previous selection.

## Root Cause

The issue was in the `LoadPaxSettings()` method's interaction with `PopulateSerialPorts()`:

**Before Fix:**
```csharp
private void LoadPaxSettings()
{
    // ... load other settings ...
    
    // Populate serial ports
    PopulateSerialPorts();  // <-- Called with empty combo box
    
    if (!string.IsNullOrEmpty(_settings.PaxTerminal.SerialPort))
    {
        serialPortComboBox.Text = _settings.PaxTerminal.SerialPort;  // <-- Too late!
    }
}

private void PopulateSerialPorts()
{
    string currentSelection = serialPortComboBox.Text;  // <-- Empty at this point!
    serialPortComboBox.Items.Clear();
    // ... populate ports ...
    
    if (!string.IsNullOrEmpty(currentSelection))  // <-- Always false
    {
        serialPortComboBox.Text = currentSelection;
    }
    else
    {
        serialPortComboBox.SelectedIndex = 0;  // <-- Always defaults to first port
    }
}
```

**Sequence of events:**
1. `LoadPaxSettings()` called
2. `PopulateSerialPorts()` called with combo box text still empty
3. Inside `PopulateSerialPorts()`, `currentSelection` = "" (empty)
4. Ports added to combo box
5. Since `currentSelection` is empty, defaults to index 0
6. Back in `LoadPaxSettings()`, tries to set saved port - but it's too late, the SelectedIndexChanged event has already fired and saved the wrong value

## Solution

Modified `PopulateSerialPorts()` to accept an optional parameter for the saved selection, and pass it from `LoadPaxSettings()`:

**After Fix:**
```csharp
private void LoadPaxSettings()
{
    // ... load other settings ...
    
    // Populate serial ports with saved selection
    PopulateSerialPorts(_settings.PaxTerminal.SerialPort);  // <-- Pass saved value
}

private void PopulateSerialPorts(string? savedSelection = null)
{
    // Use saved selection if provided, otherwise use current selection
    string currentSelection = savedSelection ?? serialPortComboBox.Text;
    
    serialPortComboBox.Items.Clear();
    // ... populate ports ...
    
    if (!string.IsNullOrEmpty(currentSelection) && serialPortComboBox.Items.Contains(currentSelection))
    {
        serialPortComboBox.Text = currentSelection;  // <-- Now uses saved value!
    }
    else if (serialPortComboBox.Items.Count > 0)
    {
        serialPortComboBox.SelectedIndex = 0;  // <-- Fallback if saved port not found
    }
}
```

## Changes Made

### Form1.cs

1. **LoadPaxSettings() method:**
   - Changed to pass the saved serial port value to `PopulateSerialPorts()`
   - Removed the redundant code that tried to set the serial port after population
   - Added debug logging to track loaded values

2. **PopulateSerialPorts() method:**
   - Added optional `savedSelection` parameter
   - Uses `savedSelection` if provided, otherwise preserves current selection
   - Added debug logging to track restoration process

3. **SavePaxSettings() method:**
   - Added debug logging to track saved values

## Behavior

### On First Launch (No Settings File)
1. Settings file doesn't exist
2. Defaults are used (empty SerialPort)
3. `PopulateSerialPorts("")` called with empty string
4. Defaults to first available COM port
5. User selects their desired port
6. Selection is saved to settings file

### On Subsequent Launches (Settings File Exists)
1. Settings loaded from file (e.g., SerialPort = "COM5")
2. `PopulateSerialPorts("COM5")` called
3. Available ports enumerated
4. If "COM5" exists in list, it's selected
5. If "COM5" doesn't exist (device unplugged), defaults to first port

### When Refreshing Port List (Button Click)
1. User clicks refresh button
2. `PopulateSerialPorts()` called with no parameter
3. Uses current combo box text (preserves current selection if still available)

### When Switching Connection Methods
1. User changes from TCP to USB
2. `PopulateSerialPorts()` called with no parameter  
3. Uses current combo box text (preserves selection)

## Testing

### Manual Test Steps

1. **Initial Setup:**
   - Launch application
   - Select USB connection method
   - Note the available COM ports
   - Select a specific COM port (not the first one)
   - Close the application

2. **Verify Persistence:**
   - Relaunch the application
   - Navigate to USB connection settings
   - **Expected:** The COM port you selected should still be selected
   - **Before fix:** Would always show the first COM port

3. **Test Missing Port:**
   - Note your selected COM port
   - Unplug the USB device (or edit settings file to use a non-existent port)
   - Launch application
   - **Expected:** Should default to first available port
   - Check logs for: "PopulateSerialPorts: Set to default index 0"

4. **Test Refresh Button:**
   - Launch application
   - Select a COM port
   - Click the refresh/reload button for ports
   - **Expected:** Your selection should be preserved if port still exists

### Debug Output

When running the application, check the Output window (Debug) for these log messages:

**On Load:**
```
LoadPaxSettings: Loading settings...
  ConnectionMethod: USB
  IpAddress: 127.0.0.1
  Port: 10009
  Timeout: 60000
  SerialPort: 'COM5'
PopulateSerialPorts: savedSelection=COM5, currentSelection=COM5
PopulateSerialPorts: Restored selection to COM5
```

**On Save:**
```
SavePaxSettings: Saving SerialPort='COM5'
```

## Settings File Location

The serial port setting is stored in:
```
%APPDATA%\BakerScaleConnect\settings.json
```

Example content:
```json
{
  "PaxTerminal": {
    "ConnectionMethod": "USB",
    "IpAddress": "127.0.0.1",
    "Port": 10009,
    "Timeout": 60000,
    "SerialPort": "COM5"
  }
}
```

## Backwards Compatibility

✅ **Fully backwards compatible**
- Existing settings files will work correctly
- Empty or missing SerialPort values handled gracefully
- No database migrations or data conversions needed

## Related Files

- `Form1.cs` - Fixed the loading and population logic
- `AppSettings.cs` - No changes needed (already had SerialPort property)
- Settings file persists correctly (no changes needed)

## Future Improvements

Optional enhancements that could be made:

1. **Remember last N ports:** Store a list of recently used ports
2. **Auto-detect PAX terminal:** Scan all COM ports and identify which has a PAX terminal
3. **Port change detection:** Detect when COM ports are added/removed and update UI automatically
4. **Validation:** Verify the selected port is actually a PAX terminal before saving
5. **Port preferences:** Allow users to favorite or alias specific ports

## Troubleshooting

### Port still not saving
1. Check Debug output for save/load messages
2. Verify settings file exists at `%APPDATA%\BakerScaleConnect\settings.json`
3. Check file permissions on the settings directory
4. Look for exceptions in Debug output

### Wrong port selected on startup
1. Check the settings.json file - is the correct port stored?
2. Is that COM port still available on the system?
3. Check Debug output: "Restored selection" vs "Set to default index 0"

### Ports not appearing
1. Verify COM devices are connected
2. Check Device Manager for COM ports
3. Look for "Error loading ports" in Debug output
