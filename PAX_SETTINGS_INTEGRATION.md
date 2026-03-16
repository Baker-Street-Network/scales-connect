# PAX Terminal Settings Integration - Summary

## What Was Implemented

I've successfully integrated PAX terminal settings with both **TCP/IP** and **USB** connection support into your Windows Forms application.

### 1. **AppSettings.cs** - Settings Management
- Created a simple JSON-based settings management system
- Settings are stored in: `%APPDATA%\BakerScaleConnect\settings.json`
- Auto-loads on application startup
- Auto-saves when settings are changed
- Stores connection method (TCP or USB), serial port, IP, port, and timeout

### 2. **Form1.cs Updates** - UI Integration
Added the following functionality:
- **Connection Method Selection**: Choose between TCP/IP or USB
- **Dynamic Tab Switching**: Shows only the relevant tab based on connection method
- **Auto-Populate Serial Ports**: Automatically detects and lists available COM ports
- **Load Settings on Startup**: Automatically loads PAX terminal settings from file
- **Auto-Save on Change**: Any changes to settings are automatically saved
- **Test Connection Button**: Connection test supporting both TCP and USB
- **Run Transaction Button**: Process a real payment transaction with custom amount
- **Validation**: Validates all inputs based on connection method
- **Settings Service Integration**: Automatically updates the PaxService when settings change

### 3. **Connection Method Support**

#### TCP/IP Connection
- Requires: IP Address, Port, Timeout
- Test Connection: Simple TCP socket connection test (5 seconds)
- Default: 127.0.0.1:10009

#### USB Connection
- Requires: Serial Port (COM port), Timeout
- Auto-detects available COM ports
- Test Connection: Opens and closes serial port to verify access
- BaudRate: 115200 (PAX default)

### 4. **Test Connection Feature**
The "Test Connection" button:
- Tests connection based on selected method (TCP or USB)
- Fast validation (5-second timeout for TCP)
- Shows success/failure with method-specific troubleshooting tips
- No actual payment processing

### 5. **Run Transaction Feature**
The "Run Transaction" button:
- Processes a REAL payment transaction
- Works with both TCP and USB connections
- Accepts custom amount from the "Test Amount" field
- Requires confirmation before processing
- Shows detailed transaction result
- Disables all controls during processing

## Usage

### Setting Up PAX Terminal

#### For TCP/IP Connection:
1. Select "TCP" from the connection method dropdown
2. Enter the terminal IP address (default: 127.0.0.1)
3. Enter the port number (default: 10009)
4. Enter the timeout in milliseconds (default: 60000)
5. Click "Test Connection" to verify network connectivity

#### For USB Connection:
1. Select "USB" from the connection method dropdown
2. Connect PAX terminal via USB cable
3. Select the appropriate COM port from the dropdown (auto-populated)
4. Enter the timeout in milliseconds (default: 60000)
5. Click "Test Connection" to verify serial port access

### Running Test Transactions
1. Configure connection settings (TCP or USB)
2. Test the connection first
3. Enter a test amount (e.g., 1.00, 5.50, 10.00)
4. Click "Run Transaction"
5. Confirm you want to process the transaction
6. Complete the transaction on the terminal
7. View the detailed result

### Settings File Location
Settings are stored at:
```
C:\Users\[YourUsername]\AppData\Roaming\BakerScaleConnect\settings.json
```

### Settings Format
```json
{
  "PaxTerminal": {
    "ConnectionMethod": "TCP",
    "IpAddress": "127.0.0.1",
    "Port": 10009,
    "Timeout": 60000,
    "SerialPort": "COM3"
  }
}
```

## Technical Details

### Control Names
- `connectionMethodComboBox` (ComboBox) - Connection method selector (TCP/USB)
- `terminalIp` (TextBox) - Terminal IP address (TCP only)
- `portNumber` (TextBox) - Port number (TCP only)
- `timeoutTextBox` (TextBox) - Connection timeout in milliseconds
- `serialPortComboBox` (ComboBox) - Serial port selector (USB only)
- `testAmountTextbox` (TextBox) - Amount for test transactions
- `button4` (Button) - Test Connection button
- `btnTestTransaction` (Button) - Run Transaction button

### Event Handlers
- **BtnTestConnection_Click**: Handles connection test (TCP or USB)
- **BtnTestTransaction_Click**: Handles payment transaction processing
- **ConnectionMethod_Changed**: Updates UI when connection method changes
- **UpdateTabVisibility**: Shows/hides TCP or USB tab based on selection
- **PopulateSerialPorts**: Discovers and lists available COM ports
- **PaxSettings_Changed**: Auto-saves settings when any field changes
- **LoadPaxSettings**: Loads settings from file on startup
- **SavePaxSettings**: Saves settings to file
- **UpdatePaxService**: Updates the PaxService with current settings

### Validation Rules
- **Connection Method**: Must be "TCP" or "USB"
- **IP Address**: Required for TCP, must not be empty
- **Port**: Required for TCP, must be between 1 and 65535
- **Serial Port**: Required for USB, must be a valid COM port
- **Timeout**: Must be at least 1000ms (1 second)
- **Amount**: Must be a valid decimal greater than 0

## PaxService USB Implementation

The PaxService now supports both connection types:
- **TCP**: Uses `TcpSetting` with IP, Port, and Timeout
- **USB**: Uses `UsbSetting` with PortName (COM port) and Timeout
- Automatically selects the correct connection method based on settings
- Validates serial port is configured before attempting USB connection

## Build Status
✅ Build successful - all changes compile without errors

## Dependencies Added
- `System.IO.Ports` (v8.0.0) - For serial port communication
