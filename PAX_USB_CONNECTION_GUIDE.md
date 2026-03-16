# PAX USB Connection Guide

## Overview
The PAX POSLink SDK (Semi-Integration) only provides native support for TCP/IP connections. However, PAX terminals connected via USB can still be used through a localhost TCP connection.

## How USB Connections Work

When a PAX terminal is connected via USB:

1. **PAX USB Driver**: The terminal requires a PAX USB driver to be installed on the host computer
2. **Virtual TCP Bridge**: The driver creates a virtual TCP server listening on `localhost` (127.0.0.1)
3. **Default Port**: Typically uses port `10009` (standard PAX port)
4. **Communication**: All SDK communication still uses TCP/IP protocol, but routed through USB

## Implementation Details

### In the Application
When "USB" connection method is selected:
- The application uses the PAX SDK's `TcpSetting` with `localhost` (127.0.0.1)
- The serial port selection is informational only (shows which COM port the terminal is on)
- The actual communication uses TCP through the USB driver's virtual server

### Code Reference
```csharp
// USB mode in PaxService.ProcessCreditPayment
if (_connectionMethod == "USB")
{
    TcpSetting usbTcpSetting = new()
    {
        Ip = "127.0.0.1",  // USB terminals listen on localhost
        Port = _settings.Port > 0 ? _settings.Port : 10009,  // Default PAX port
        Timeout = _settings.Timeout
    };
    terminal = poslinkSemi.GetTerminal(usbTcpSetting);
}
```

## Testing USB Connection

### Test Connection Button
The "Test Connection" button for USB:
1. Sends a test transaction ($0.00) to the terminal
2. If the terminal responds (success or rejects the amount), it's verified as a PAX device
3. No response or timeout indicates the terminal is not accessible or not a PAX device

This verifies:
- ✅ PAX USB driver is installed and running
- ✅ Terminal is properly connected
- ✅ Terminal is responding to commands
- ✅ Communication pathway is working

### Requirements
For USB connection to work, ensure:
1. **PAX USB Driver** is installed (provided by PAX)
2. **Terminal** is physically connected via USB cable
3. **Driver Service** is running (typically auto-starts with Windows)
4. **Port 10009** is not blocked by firewall
5. **Terminal Configuration**: Terminal must be set to USB communication mode

## Troubleshooting

### Connection Test Fails
- **Verify driver installation**: Check Device Manager for PAX USB device
- **Check driver service**: Ensure PAX service is running in Windows Services
- **Try different USB port**: Some ports may not provide sufficient power
- **Restart terminal**: Power cycle the PAX terminal
- **Check port number**: Default is 10009, but may be configured differently

### "Device not responding"
- Terminal may be in a different mode (need to set to USB mode in terminal settings)
- USB cable may be faulty (try a different cable)
- Driver may need to be reinstalled

### "Port already in use"
- Another application is already communicating with the terminal
- Close other PAX software or point-of-sale applications

## Additional Notes

### Why Not DirectSerial Communication?
The PAX SDK does not include native serial/COM port support classes. The USB driver approach:
- ✅ Provides a standardized interface
- ✅ Handles PAX protocol complexities
- ✅ Maintains compatibility with TCP-based code
- ✅ Supports multiple applications accessing the terminal

### Alternative Approaches
If the PAX USB driver is not available:
1. Use PAX terminals with Ethernet/WiFi connection instead
2. Contact PAX support for USB driver package
3. Consider cloud-based PAX integration services

## References
- PAX POSLink SDK Documentation
- PAX Terminal Configuration Guide
- PAX USB Driver Installation Guide (from PAX support)
