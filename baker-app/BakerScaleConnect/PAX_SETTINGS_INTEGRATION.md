# PAX Terminal Settings Integration - Summary

## What Was Implemented

I've successfully integrated PAX terminal settings into your Windows Forms application with the following features:

### 1. **AppSettings.cs** - Settings Management
- Created a simple JSON-based settings management system
- Settings are stored in: `%APPDATA%\BakerScaleConnect\settings.json`
- Auto-loads on application startup
- Auto-saves when settings are changed

### 2. **Form1.cs Updates** - UI Integration
Added the following functionality:
- **Load Settings on Startup**: Automatically loads PAX terminal settings from file
- **Auto-Save on Change**: Any changes to IP, Port, or Timeout are automatically saved
- **Test Connection Button**: Simple TCP connection test to verify terminal is reachable
- **Run Transaction Button**: Process a real payment transaction with custom amount
- **Validation**: Validates all inputs before testing or processing
- **Settings Service Integration**: Automatically updates the PaxService when settings change

### 3. **Test Connection Feature**
The "Test Connection" button:
- Simple TCP socket connection test (no transaction)
- Fast 5-second timeout
- Validates IP address and port are correct
- Shows success/failure with helpful troubleshooting tips
- No actual payment processing

### 4. **Run Transaction Feature**
The "Run Transaction" button:
- Processes a REAL payment transaction
- Accepts custom amount from the "Test Amount" field
- Requires confirmation before processing
- Shows detailed transaction result (response code, message, timestamp)
- Disables all controls during processing to prevent interference
- Generates unique ECR reference numbers (TEST-yyyyMMddHHmmss format)

## Usage

### Setting Up PAX Terminal
1. Enter the terminal IP address (default: 127.0.0.1)
2. Enter the port number (default: 10009)
3. Enter the timeout in milliseconds (default: 60000 = 60 seconds)
4. Click "Test Connection" to verify network connectivity
5. Settings are automatically saved as you type

### Running Test Transactions
1. Enter the desired amount in the "Test Amount" field (default: 1.00)
2. Click "Run Transaction"
3. Confirm you want to process the transaction
4. Wait for the terminal to prompt for card
5. Complete the transaction on the terminal
6. View the result message with response details

### Settings File Location
Settings are stored at:
```
C:\Users\[YourUsername]\AppData\Roaming\BakerScaleConnect\settings.json
```

### Settings Format
```json
{
  "PaxTerminal": {
    "IpAddress": "127.0.0.1",
    "Port": 10009,
    "Timeout": 60000
  }
}
```

## Technical Details

### Control Names
- `terminalIp` (TextBox) - Terminal IP address
- `portNumber` (TextBox) - Port number
- `timeoutTextBox` (TextBox) - Connection timeout in milliseconds
- `testAmountTextBox` (TextBox) - Amount for test transactions
- `button3` (Button) - Test Connection button (simple TCP test)
- `btnTestTransaction` (Button) - Run Transaction button (real payment)

### Event Handlers
- **BtnTestConnection_Click**: Handles simple TCP connection test
- **BtnTestTransaction_Click**: Handles actual payment transaction processing
- **PaxSettings_Changed**: Auto-saves settings when any field changes
- **LoadPaxSettings**: Loads settings from file on startup
- **SavePaxSettings**: Saves settings to file
- **UpdatePaxService**: Updates the PaxService with current settings

### Validation Rules
- **IP Address**: Must not be empty
- **Port**: Must be between 1 and 65535
- **Timeout**: Must be at least 1000ms (1 second)
- **Amount**: Must be a valid decimal greater than 0

## Integration with PaxService

The Form automatically:
1. Injects the `PaxService` via dependency injection
2. Updates the service with loaded settings on startup
3. Updates the service whenever settings change
4. Uses the service for both connection testing and transaction processing

## Build Status
✅ Build successful - all changes compile without errors
