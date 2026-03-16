# ShowItem Quick Reference

## Endpoint
```
POST /api/pax/showitem
```

## Basic Request
```json
{
  "items": [
    {
      "name": "Item Name",
      "price": "10.50",
      "quantity": 1,
      "sku": "OPTIONAL-SKU"
    }
  ],
  "ecrReferenceNumber": "ORDER-123"
}
```

## cURL Example
```bash
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {"name": "Coffee", "price": "3.50", "quantity": 2},
      {"name": "Muffin", "price": "2.75", "quantity": 1}
    ],
    "ecrReferenceNumber": "ORDER-001"
  }'
```

## Complete Checkout Flow
```bash
# 1. Show items
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{"items":[{"name":"Coffee","price":"3.50","quantity":2}],"ecrReferenceNumber":"ORDER-001"}'

# 2. Process payment
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{"amount":"7.00","ecrReferenceNumber":"ORDER-001","transactionType":"Sale"}'
```

## Response Codes
- **200 OK**: Items displayed successfully
- **400 Bad Request**: Invalid input (missing fields, invalid price format)
- **499 Client Closed**: Request cancelled
- **502 Bad Gateway**: Terminal error or SDK doesn't support ShowItem

## Common Errors

### "ShowItem feature not available in current PAX SDK version"
**Solution**: Update to latest PAX POSLink SDK or contact PAX support

### "At least one item is required"
**Solution**: Ensure items array is not empty

### "Invalid price format"
**Solution**: Use decimal string format (e.g., "10.50", not 10.5 or "ten dollars")

## Quick Tips
- ✅ Display items BEFORE initiating payment for best UX
- ✅ Use decimal strings for prices: "10.50"
- ✅ Keep item names concise for terminal display
- ✅ Typical limit: 10-20 items per terminal
- ✅ Use same ecrReferenceNumber for ShowItem and payment

## More Information
See **PAX_SHOWITEM_IMPLEMENTATION.md** for complete documentation
