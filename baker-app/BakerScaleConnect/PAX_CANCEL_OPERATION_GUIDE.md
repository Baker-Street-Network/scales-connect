# PAX Terminal - Cancel Operation Feature

## Overview
The cancel operation feature allows you to immediately cancel any in-progress operation on the PAX terminal. This is useful for handling situations where a transaction needs to be aborted or a terminal is stuck waiting for input.

## When to Use Cancel

### Common Scenarios
1. **Customer Changes Mind**: Customer decides not to proceed with payment
2. **Wrong Amount Entered**: Transaction initiated with incorrect amount
3. **Terminal Stuck**: Terminal is waiting for card input but customer has left
4. **Timeout Issues**: Operation is taking too long and needs to be stopped
5. **Emergency Stop**: Any situation requiring immediate operation cancellation

### What Can Be Canceled
- Credit card transactions in progress
- Terminal prompts waiting for input
- Card reader operations
- Any other terminal operation

## How It Works

### Technical Details
The cancel operation uses the PAX SDK's `Terminal.Cancel()` method, which:
1. Sets a cancel flag in the communication layer
2. Signals the terminal to abort the current operation
3. Returns the terminal to ready state

**Important Notes:**
- This cancels the **terminal operation**, not the function call itself
- The terminal must be connected and responsive
- Some operations may take a moment to fully cancel
- The cancel is a "best effort" - if the transaction has already completed on the terminal side, it cannot be reversed

## API Usage

### REST API Endpoint
**POST** `/api/pax/cancel`

**Request:**
```bash
curl -X POST http://localhost:5000/api/pax/cancel
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "message": "Operation canceled successfully",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Error Response (502 Bad Gateway):**
```json
{
  "success": false,
  "error": "Failed to cancel operation: Connection timeout",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Service Method
If calling from C# code within the application:

```csharp
var paxService = serviceProvider.GetRequiredService<PaxService>();
var (success, message) = paxService.CancelCurrentOperation();

if (success)
{
    Console.WriteLine("Operation canceled successfully");
}
else
{
    Console.WriteLine($"Failed to cancel: {message}");
}
```

## Integration Examples

### JavaScript/Web Application
```javascript
async function cancelPayment() {
    try {
        const response = await fetch('http://localhost:5000/api/pax/cancel', {
            method: 'POST'
        });
        
        const result = await response.json();
        
        if (result.success) {
            alert('Payment canceled successfully');
        } else {
            alert(`Cancel failed: ${result.error}`);
        }
    } catch (error) {
        console.error('Error canceling payment:', error);
    }
}
```

### Python Application
```python
import requests

def cancel_pax_operation():
    try:
        response = requests.post('http://localhost:5000/api/pax/cancel')
        result = response.json()
        
        if result['success']:
            print("Operation canceled successfully")
        else:
            print(f"Cancel failed: {result['error']}")
    except Exception as e:
        print(f"Error: {e}")
```

### C# Application (External)
```csharp
using System.Net.Http;
using System.Text.Json;

public class PaxClient
{
    private readonly HttpClient _httpClient;
    
    public PaxClient(string baseUrl = "http://localhost:5000")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }
    
    public async Task<bool> CancelOperationAsync()
    {
        var response = await _httpClient.PostAsync("/api/pax/cancel", null);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CancelResponse>(json);
        return result?.Success ?? false;
    }
}

public class CancelResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Error { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## Best Practices

### 1. User Confirmation
Before canceling a transaction, especially if initiated by customer action:
```javascript
if (confirm('Are you sure you want to cancel this payment?')) {
    await cancelPayment();
}
```

### 2. Error Handling
Always handle potential errors when canceling:
```csharp
try
{
    var (success, message) = paxService.CancelCurrentOperation();
    if (!success)
    {
        // Log error, notify user, retry if appropriate
        _logger.LogError("Cancel failed: {Message}", message);
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Exception during cancel operation");
}
```

### 3. UI Feedback
Provide clear feedback to users:
- Show a "Canceling..." indicator
- Display confirmation when successful
- Show error message if cancel fails
- Re-enable controls after operation completes

### 4. Timeout Handling
Set appropriate timeouts for cancel operations:
```csharp
var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
// The cancel operation itself should be quick
```

## Connection Method Support

The cancel operation works with both connection methods:
- ✅ **TCP/IP**: Direct network connection to terminal
- ✅ **USB**: Via PAX USB driver (localhost TCP bridge)

Both use the same cancel mechanism through the PAX SDK.

## Troubleshooting

### Cancel Doesn't Work
**Symptoms:** Cancel request succeeds but terminal still shows prompt

**Possible Causes:**
1. Transaction already completed on terminal
2. Terminal firmware doesn't support cancel at current stage
3. Communication delay

**Solutions:**
- Try canceling again
- Power cycle the terminal
- Check terminal firmware version

### Cancel Request Times Out
**Symptoms:** API call takes long time and eventually fails

**Possible Causes:**
1. Terminal not connected
2. Network issues (TCP mode)
3. USB driver not running (USB mode)

**Solutions:**
- Verify terminal connection
- Check connection settings
- Test with `/api/pax/health` endpoint first

### Repeated Cancel Needed
**Symptoms:** First cancel doesn't work, need to call multiple times

**Possible Causes:**
1. Terminal state transition in progress
2. Multiple operations queued

**Solutions:**
- Wait a moment between cancel attempts
- Check terminal status
- Consider terminal reset if persists

## Logging

The service logs cancel operations for troubleshooting:

```
[Information] Canceling current PAX terminal operation: Method=TCP
[Information] PAX terminal operation canceled successfully
```

Or on error:
```
[Error] Error canceling PAX terminal operation: Connection timeout
```

Check application logs if cancel operations are not working as expected.

## Security Considerations

1. **Access Control**: Consider adding authentication to the cancel endpoint if exposed externally
2. **Rate Limiting**: Prevent abuse by limiting cancel requests per time period
3. **Audit Logging**: Log who initiated cancel operations for compliance

## Related Documentation
- [PAX API Documentation](PAX_API_DOCUMENTATION.md)
- [PAX USB Connection Guide](PAX_USB_CONNECTION_GUIDE.md)
- [PAX Settings Integration](PAX_SETTINGS_INTEGRATION.md)
