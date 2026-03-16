# PAX Terminal ShowItem Implementation

## Overview

This document describes the implementation of the ShowItem feature for displaying items on PAX credit card terminals. This feature allows displaying ordered items on the terminal screen before or during payment, communicating with the BroadPOS app on the PAX terminal.

## Feature Description

The ShowItem functionality enables your point-of-sale system to send item details (name, price, quantity, SKU) to the PAX terminal for display to the customer. This provides transparency and allows customers to review their order before completing payment.

## Implementation Details

### API Endpoint

**POST** `/api/pax/showitem`

Display items on the PAX terminal screen.

### Request Format

```json
{
  "items": [
    {
      "name": "Coffee",
      "price": "3.50",
      "quantity": 2,
      "sku": "ITEM001"
    },
    {
      "name": "Muffin",
      "price": "2.75",
      "quantity": 1,
      "sku": "ITEM002"
    }
  ],
  "ecrReferenceNumber": "ORDER-2024-001"
}
```

**Fields:**
- `items` (required): Array of items to display
  - `name` (required): Item name/description
  - `price` (required): Item price as decimal string (e.g., "10.50")
  - `quantity` (required): Item quantity (integer)
  - `sku` (optional): Item SKU or product code
- `ecrReferenceNumber` (optional): Unique reference number for tracking

### Response Format

**Success (200 OK):**
```json
{
  "success": true,
  "responseCode": "000000",
  "responseMessage": "OK",
  "ecrReferenceNumber": "ORDER-2024-001",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Error (502 Bad Gateway):**
```json
{
  "success": false,
  "responseCode": "",
  "responseMessage": "",
  "ecrReferenceNumber": "ORDER-2024-001",
  "errorMessage": "Show item failed with error code: ...",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## Usage Examples

### Basic Usage (cURL)

```bash
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {
        "name": "Coffee",
        "price": "3.50",
        "quantity": 2
      },
      {
        "name": "Muffin",
        "price": "2.75",
        "quantity": 1
      }
    ],
    "ecrReferenceNumber": "ORDER-2024-001"
  }'
```

### Complete Checkout Workflow

```bash
# Step 1: Show items on terminal before payment
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {"name": "Coffee", "price": "3.50", "quantity": 2},
      {"name": "Muffin", "price": "2.75", "quantity": 1}
    ],
    "ecrReferenceNumber": "ORDER-2024-001"
  }'

# Step 2: Process payment
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "9.75",
    "ecrReferenceNumber": "ORDER-2024-001",
    "transactionType": "Sale"
  }'
```

### C# Usage Example

```csharp
using BakerScaleConnect.Controllers.Models;

// Create items list
var showItemRequest = new PaxShowItemRequest
{
    Items = new List<PaxDisplayItem>
    {
        new PaxDisplayItem
        {
            Name = "Coffee",
            Price = "3.50",
            Quantity = 2,
            Sku = "ITEM001"
        },
        new PaxDisplayItem
        {
            Name = "Muffin",
            Price = "2.75",
            Quantity = 1,
            Sku = "ITEM002"
        }
    },
    EcrReferenceNumber = "ORDER-2024-001"
};

// Send to terminal
var response = await paxService.ShowItemsAsync(showItemRequest);

if (response.Success)
{
    Console.WriteLine("Items displayed successfully on terminal");
}
else
{
    Console.WriteLine($"Failed to display items: {response.ErrorMessage}");
}
```

## Technical Implementation

### SDK Compatibility

The ShowItem feature uses the PAX POSLink SDK's ShowItemRequest command. The implementation includes:

1. **Reflection-based approach**: The code uses reflection to dynamically discover and invoke the ShowItem method, making it compatible with different SDK versions.

2. **Graceful degradation**: If the ShowItem method is not available in your SDK version, the API will return a clear error message indicating that the feature requires an updated SDK.

3. **Cancellation support**: The operation can be cancelled using the standard cancellation endpoint.

### Requirements

- PAX POSLink SDK with ShowItemRequest support
- BroadPOS app running on the PAX terminal
- Terminal configured for semi-integration

### SDK Version Notes

If you receive an error message about ShowItem not being available:

1. **Update the PAX SDK**: Contact PAX support or download the latest POSLink SDK version that includes ShowItemRequest support.

2. **Verify BroadPOS**: Ensure the BroadPOS app is installed and running on your PAX terminal.

3. **Check API Guide**: Refer to the PAX API Guide for ShowItemRequest documentation specific to your terminal model.

## Files Modified

### New Files Created

1. **Controllers/Models/PaxShowItemRequest.cs**
   - Request model for showing items
   - Contains PaxDisplayItem class

2. **Controllers/Models/PaxShowItemResponse.cs**
   - Response model for show item operations

### Modified Files

1. **Services/PaxService.cs**
   - Added `ShowItemsAsync` method
   - Implements reflection-based SDK method discovery
   - Handles item display logic

2. **Controllers/PaxController.cs**
   - Added `/api/pax/showitem` endpoint
   - Request validation
   - Price conversion to cents format

3. **PAX_API_DOCUMENTATION.md**
   - Added ShowItem endpoint documentation
   - Added usage examples

## Error Handling

The implementation includes comprehensive error handling:

1. **Validation Errors (400)**:
   - Missing items array
   - Empty items array
   - Missing item name
   - Missing item price
   - Invalid price format

2. **SDK Errors (502)**:
   - ShowItem method not found in SDK
   - Terminal communication failures
   - Execution errors

3. **Cancellation (499)**:
   - Operation cancelled by client

## Best Practices

1. **Display items before payment**: Call ShowItem before initiating the payment transaction for best user experience.

2. **Price format**: Provide prices as decimal strings (e.g., "10.50"). The API automatically converts to cents format.

3. **Item limits**: Be mindful of terminal screen limitations - typically 10-20 items can be displayed depending on terminal model.

4. **Error handling**: Always check the response.Success flag and handle errors gracefully.

5. **Reference numbers**: Use consistent ECR reference numbers between ShowItem and payment calls for tracking.

## Troubleshooting

### ShowItem not available error

**Problem**: Error message "ShowItem feature not available in current PAX SDK version"

**Solution**:
- Update to the latest PAX POSLink SDK
- Contact PAX support for ShowItemRequest documentation
- Verify your terminal supports the BroadPOS app

### Items not displaying on terminal

**Problem**: API returns success but items don't appear on terminal

**Solution**:
- Verify BroadPOS app is running on the terminal
- Check terminal configuration for semi-integration
- Ensure terminal is not in a different mode (standalone, etc.)
- Try sending fewer items if screen is full

### Connection timeout

**Problem**: Operation times out before completing

**Solution**:
- Increase timeout in terminal settings
- Check network connectivity
- Verify terminal is powered on and responsive

## Support

For additional support:

1. **PAX API Documentation**: Refer to the official PAX POSLink API Guide
2. **PAX Support**: Contact PAX technical support for SDK-related questions
3. **BroadPOS Documentation**: Check BroadPOS app documentation for display capabilities

## Future Enhancements

Potential improvements for future versions:

1. Support for item images/icons
2. Display subtotals and taxes
3. Item modifiers and special instructions
4. Color coding or highlighting
5. Custom branding on item display
