# PAX Transaction Cancellation - Technical Implementation

## Overview
The PAX transaction cancellation system has been redesigned to properly handle cancellation at both the HTTP request level and the terminal operation level.

## The Problem (Before)

The original implementation had a fundamental flaw:

```csharp
// POST /api/pax/credit - creates Terminal instance A, starts transaction
// POST /api/pax/cancel - creates Terminal instance B, calls Cancel()
// Problem: Cancel() on B doesn't affect the transaction running on A!
```

**Why it didn't work:**
- Each API call created a NEW terminal instance
- Calling `Cancel()` on a different terminal instance has no effect on the running transaction
- The terminal instances were not shared across requests

## The Solution (Now)

### Architecture Changes

#### 1. **Active Terminal Tracking**
```csharp
public class PaxService
{
    private Terminal? _activeTerminal;
    private readonly object _terminalLock = new object();
    
    // Store the terminal when starting a transaction
    lock (_terminalLock)
    {
        _activeTerminal = terminal;
    }
}
```

#### 2. **Async with CancellationToken**
```csharp
public async Task<PaxCreditResponse> ProcessCreditPaymentAsync(
    PaxCreditRequest paymentRequest, 
    CancellationToken cancellationToken = default)
{
    // Run blocking DoCredit in Task.Run
    var transactionTask = Task.Run(() =>
    {
        var execResult = terminal.Transaction.DoCredit(request, out DoCreditResponse response);
        return (execResult, response);
    }, cancellationToken);
    
    // Monitor for cancellation
    try
    {
        var taskResult = await transactionTask;
        // Process result...
    }
    catch (OperationCanceledException)
    {
        // User cancelled - call Cancel() on the ACTUAL terminal
        terminal.Cancel();
        return cancelled response;
    }
}
```

#### 3. **Controller Integration**
```csharp
[HttpPost("credit")]
public async Task<ActionResult<PaxCreditResponse>> ProcessCredit(
    [FromBody] PaxCreditRequest request, 
    CancellationToken cancellationToken)  // <-- ASP.NET Core provides this
{
    var response = await paxService.ProcessCreditPaymentAsync(request, cancellationToken);
    // ...
}
```

#### 4. **Cancel Endpoint Uses Active Terminal**
```csharp
public (bool Success, string Message) CancelCurrentOperation()
{
    Terminal? terminal;
    lock (_terminalLock)
    {
        terminal = _activeTerminal;  // Get the ACTUAL running terminal
    }
    
    if (terminal == null)
    {
        return (false, "No active operation to cancel");
    }
    
    terminal.Cancel();  // Cancel on the correct instance!
    return (true, "Cancel signal sent to terminal");
}
```

## How It Works

### Scenario 1: Client Aborts HTTP Request

```javascript
// JavaScript client
const controller = new AbortController();

// Start transaction
fetch('http://localhost:5000/api/pax/credit', {
    method: 'POST',
    signal: controller.signal,
    body: JSON.stringify({...})
});

// User cancels
controller.abort();  // <-- Triggers CancellationToken in API
```

**Flow:**
1. HTTP request is aborted
2. ASP.NET Core cancels the `CancellationToken`
3. `ProcessCreditPaymentAsync` catches `OperationCanceledException`
4. Calls `terminal.Cancel()` on the active terminal
5. PAX SDK signals terminal to abort operation
6. Transaction is cancelled on the terminal

### Scenario 2: Explicit Cancel Endpoint

```bash
# Terminal 1: Start transaction
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{"amount": "25.99", "ecrReferenceNumber": "ORDER-001", "transactionType": "Sale"}'

# Terminal 2: Cancel it
curl -X POST http://localhost:5000/api/pax/cancel
```

**Flow:**
1. First request stores `_activeTerminal`
2. Second request retrieves `_activeTerminal`
3. Calls `Cancel()` on the SAME terminal instance
4. Terminal operation is cancelled

## Threading Safety

### Terminal Access
```csharp
private readonly object _terminalLock = new object();

// Set active terminal (thread-safe)
lock (_terminalLock)
{
    _activeTerminal = terminal;
}

// Get active terminal (thread-safe)
lock (_terminalLock)
{
    terminal = _activeTerminal;
}

// Clear after transaction (thread-safe)
finally
{
    lock (_terminalLock)
    {
        _activeTerminal = null;
    }
}
```

### Why Locking Is Needed
- Multiple requests can arrive simultaneously
- One request might be processing while another tries to cancel
- Lock ensures we're always working with the correct terminal reference

## Cancellation Points

The PAX SDK's `DoCredit` is a **blocking synchronous call**. We can't cancel it mid-execution, but we can:

1. **Before it starts**: Check `cancellationToken.IsCancellationRequested`
2. **During execution**: Call `terminal.Cancel()` which sets a cancel flag in the SDK
3. **After it completes**: Transaction is already done (can't cancel completed transactions)

### Terminal.Cancel() Behavior

From PAX SDK documentation:
```csharp
/// <summary>
/// Cancel the operation currently being performed on the terminal
/// Please note: It not cancel function call.
/// </summary>
public override void Cancel()
{
    _communication.SetCancelFlag(cancelFlag: true);
    base.Cancel();
}
```

**What it does:**
- Sets a cancel flag in the communication layer
- Tells the terminal to abort the current operation
- Terminal returns to ready state

**What it doesn't do:**
- Stop the `DoCredit` method call itself
- Reverse a completed transaction (use Void/Return for that)

## Testing Cancellation

### Test 1: HTTP Request Abort (JavaScript)
```javascript
const controller = new AbortController();

fetch('http://localhost:5000/api/pax/credit', {
    method: 'POST',
    signal: controller.signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
        amount: "10.00",
        ecrReferenceNumber: "TEST-001",
        transactionType: "Sale"
    })
})
.then(r => r.json())
.then(data => console.log('Success:', data))
.catch(err => console.log('Cancelled:', err));

// Abort after 2 seconds
setTimeout(() => controller.abort(), 2000);
```

### Test 2: Cancel Endpoint
```bash
# Terminal 1 (start transaction)
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "10.00",
    "ecrReferenceNumber": "TEST-001",
    "transactionType": "Sale"
  }' &

# Terminal 2 (cancel it immediately)
sleep 1
curl -X POST http://localhost:5000/api/pax/cancel
```

### Test 3: Windows Forms Cancel Button
You can add a cancel button to Form1:
```csharp
private CancellationTokenSource? _transactionCts;

private async void BtnTestTransaction_Click(object? sender, EventArgs e)
{
    _transactionCts = new CancellationTokenSource();
    btnCancelTransaction.Enabled = true;  // Enable cancel button
    
    try
    {
        var response = await _paxService.ProcessCreditPaymentAsync(
            request, 
            _transactionCts.Token);
        // Handle response...
    }
    finally
    {
        btnCancelTransaction.Enabled = false;  // Disable cancel button
        _transactionCts?.Dispose();
        _transactionCts = null;
    }
}

private void BtnCancelTransaction_Click(object? sender, EventArgs e)
{
    _transactionCts?.Cancel();  // Cancel the current transaction
}
```

## Limitations

### 1. **Timing Window**
If the transaction completes on the terminal before `Cancel()` is called, it cannot be reversed.
- Use Void transaction to reverse completed sales
- Use Return transaction for refunds

### 2. **Terminal State**
Some terminal states cannot be cancelled:
- Final approval/decline received
- Signature being printed
- Receipt printing

### 3. **Network Delays**
Cancel signal takes time to reach terminal:
- TCP: Network latency
- USB: Driver processing time

## Best Practices

### 1. **Provide UI Feedback**
```csharp
// Show cancel button when transaction starts
btnCancel.Visible = true;
btnCancel.Enabled = true;

// Hide when transaction completes
btnCancel.Visible = false;
```

### 2. **Handle Timeouts**
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
var response = await paxService.ProcessCreditPaymentAsync(request, cts.Token);
```

### 3. **Log Cancellations**
```csharp
catch (OperationCanceledException)
{
    _logger.LogWarning("Transaction {EcrRef} cancelled by user", 
        request.EcrReferenceNumber);
}
```

### 4. **Inform Users**
```csharp
MessageBox.Show(
    "Transaction cancelled. No charges were made.",
    "Transaction Cancelled",
    MessageBoxButtons.OK,
    MessageBoxIcon.Information);
```

## Troubleshooting

### Cancel Doesn't Work Immediately
**Cause**: Terminal is in a state that can't be interrupted
**Solution**: Wait a moment, terminal will return to ready state

### "No active operation to cancel"
**Cause**: Transaction already completed or hasn't started yet
**Solution**: Check timing, add validation before allowing cancel

### Multiple Transactions at Once
**Cause**: Calling ProcessCredit while another is running
**Solution**: Current design only tracks ONE active terminal. For multiple simultaneous transactions, you'd need a dictionary of active terminals keyed by ECR reference number.

## Future Enhancements

### Support Multiple Concurrent Transactions
```csharp
private readonly ConcurrentDictionary<string, Terminal> _activeTerminals = new();

// Store by ECR reference number
_activeTerminals.TryAdd(paymentRequest.EcrReferenceNumber, terminal);

// Cancel specific transaction
public (bool Success, string Message) CancelTransaction(string ecrRefNumber)
{
    if (_activeTerminals.TryRemove(ecrRefNumber, out var terminal))
    {
        terminal.Cancel();
        return (true, "Cancelled");
    }
    return (false, "Not found");
}
```

### Add Cancel Confirmation
```csharp
[HttpPost("cancel")]
public ActionResult CancelOperation([FromQuery] bool confirm = false)
{
    if (!confirm)
    {
        return Ok(new { 
            message = "Add ?confirm=true to confirm cancellation",
            requiresConfirmation = true 
        });
    }
    
    var (success, message) = paxService.CancelCurrentOperation();
    // ...
}
```

## Summary

The new cancellation system:
- ✅ Tracks the active terminal instance
- ✅ Accepts CancellationToken for HTTP request aborts
- ✅ Calls Cancel() on the CORRECT terminal
- ✅ Thread-safe with locking
- ✅ Handles both direct cancellation and request aborts
- ✅ Provides proper error messages
- ✅ Logs all cancellation events

**Key Insight:** You were absolutely right - we needed to tie the cancellation to the same request pipeline. The solution was to:
1. Accept `CancellationToken` in the processing method
2. Track the active terminal instance
3. Call `Cancel()` on that specific instance when cancellation occurs
