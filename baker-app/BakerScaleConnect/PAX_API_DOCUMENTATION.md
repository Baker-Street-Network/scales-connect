# PAX Credit Card Terminal Integration

This application now supports PAX credit card terminals using the POSLink Semi-Integration SDK.

## API Endpoints

### Process Credit Card Payment
**POST** `/api/pax/credit`

Processes a credit card payment through the PAX terminal.

**Request Body:**
```json
{
  "amount": "10.50",
  "ecrReferenceNumber": "REF123456",
  "transactionType": "Sale"
}
```

**Fields:**
- `amount` (required): Transaction amount as a decimal string
- `ecrReferenceNumber` (required): Unique reference number for tracking
- `transactionType` (optional): Transaction type - "Sale" (default), "Return", or "Void"

**Response (200 OK):**
```json
{
  "success": true,
  "responseCode": "000000",
  "responseMessage": "APPROVAL",
  "ecrReferenceNumber": "REF123456",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (502 Bad Gateway - Terminal Error):**
```json
{
  "success": false,
  "responseCode": "",
  "responseMessage": "",
  "ecrReferenceNumber": "REF123456",
  "errorMessage": "Transaction failed with error code: ...",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Get Terminal Settings
**GET** `/api/pax/settings`

Retrieves current PAX terminal connection settings.

**Response (200 OK):**
```json
{
  "ip": "127.0.0.1",
  "port": 10009,
  "timeout": 60000
}
```

### Update Terminal Settings
**POST** `/api/pax/settings`

Updates PAX terminal connection settings.

**Request Body:**
```json
{
  "ip": "192.168.1.100",
  "port": 10009,
  "timeout": 60000
}
```

**Response (200 OK):**
```json
{
  "ip": "192.168.1.100",
  "port": 10009,
  "timeout": 60000
}
```

### Health Check
**GET** `/api/pax/health`

Simple health check endpoint.

**Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "pax",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Cancel Current Operation
**POST** `/api/pax/cancel`

Cancels the current operation on the PAX terminal. This will cancel any in-progress transaction, prompt, or other operation currently being performed on the terminal.

**Use Cases:**
- Cancel a transaction that's waiting for card input
- Cancel a transaction that's processing
- Cancel any terminal prompt or operation
- Emergency stop for stuck operations

**Request:** No request body required

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Operation canceled successfully",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (502 Bad Gateway - Terminal Error):**
```json
{
  "success": false,
  "error": "Failed to cancel operation: Connection timeout",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Display Items on Terminal
**POST** `/api/pax/showitem`

Display ordered items on the PAX terminal screen. This uses the ShowItemRequest command to communicate with the BroadPOS app on the payment terminal, allowing customers to see their order details during payment.

**Use Cases:**
- Show order items before payment
- Display shopping cart on terminal screen
- Provide order transparency to customers
- Show itemized list during checkout

**Request Body:**
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
  - `price` (required): Item price as a decimal string (e.g., "10.50")
  - `quantity` (required): Item quantity
  - `sku` (optional): Item SKU or product code
- `ecrReferenceNumber` (optional): Unique reference number for tracking

**Response (200 OK):**
```json
{
  "success": true,
  "responseCode": "000000",
  "responseMessage": "OK",
  "ecrReferenceNumber": "ORDER-2024-001",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (400 Bad Request - Invalid Input):**
```json
{
  "success": false,
  "errorMessage": "At least one item is required",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (502 Bad Gateway - Terminal Error):**
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

## Configuration

Default terminal settings:
- **IP Address**: 127.0.0.1 (localhost)
- **Port**: 10009
- **Timeout**: 60000ms (60 seconds)

Settings can be updated at runtime using the `/api/pax/settings` endpoint.

## Transaction Types

The following transaction types are supported:
- **Sale** (default): Standard payment transaction
- **Return**: Refund transaction
- **Void**: Cancel a previous transaction

## Example Usage

### Process a Sale (using curl):
```bash
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "25.99",
    "ecrReferenceNumber": "ORDER-2024-001",
    "transactionType": "Sale"
  }'
```

### Process a Return:
```bash
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "25.99",
    "ecrReferenceNumber": "ORDER-2024-001",
    "transactionType": "Return"
  }'
```

### Update Terminal Settings:
```bash
curl -X POST http://localhost:5000/api/pax/settings \
  -H "Content-Type: application/json" \
  -d '{
    "ip": "192.168.1.100",
    "port": 10009,
    "timeout": 60000
  }'
```

### Cancel Current Operation:
```bash
curl -X POST http://localhost:5000/api/pax/cancel
```

**Use case example:**
If a transaction is stuck waiting for card input or the customer wants to abort:
```bash
# Start a transaction
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "25.99",
    "ecrReferenceNumber": "ORDER-2024-001",
    "transactionType": "Sale"
  }' &

# Cancel it if needed
curl -X POST http://localhost:5000/api/pax/cancel
```

### Display Items on Terminal:
```bash
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{
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
  }'
```

**Complete checkout workflow example:**
```bash
# 1. Show items on terminal before payment
curl -X POST http://localhost:5000/api/pax/showitem \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      {"name": "Coffee", "price": "3.50", "quantity": 2},
      {"name": "Muffin", "price": "2.75", "quantity": 1}
    ],
    "ecrReferenceNumber": "ORDER-2024-001"
  }'

# 2. Process payment
curl -X POST http://localhost:5000/api/pax/credit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": "9.75",
    "ecrReferenceNumber": "ORDER-2024-001",
    "transactionType": "Sale"
  }'
```

## Batch Closing Operations

### Process Batch Close
**POST** `/api/closing/batch`

Closes the current batch and settles all transactions on the PAX terminal. This is typically done at the end of the business day.

**Request Body:**
```json
{
  "ecrReferenceNumber": "BATCH-2024-001"
}
```

**Fields:**
- `ecrReferenceNumber` (required): Unique reference number for tracking the batch close operation

**Response (200 OK):**
```json
{
  "success": true,
  "responseCode": "000000",
  "responseMessage": "BATCH CLOSE APPROVED",
  "ecrReferenceNumber": "BATCH-2024-001",
  "batchNumber": "123",
  "hostResponse": "APPROVED",
  "timestamp": "2024-01-15T22:00:00Z"
}
```

**Response (502 Bad Gateway - Terminal Error):**
```json
{
  "success": false,
  "responseCode": "",
  "responseMessage": "",
  "ecrReferenceNumber": "BATCH-2024-001",
  "errorMessage": "Batch close failed with error code: ...",
  "timestamp": "2024-01-15T22:00:00Z"
}
```

### Batch Close Health Check
**GET** `/api/closing/health`

Health check endpoint for the closing service.

**Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "closing",
  "timestamp": "2024-01-15T22:00:00Z"
}
```

### Example Usage

**Process End-of-Day Batch Close:**
```bash
curl -X POST http://localhost:5000/api/closing/batch \
  -H "Content-Type: application/json" \
  -d '{
    "ecrReferenceNumber": "EOD-2024-01-15"
  }'
```

**Typical Daily Workflow:**
```bash
# Run all day's transactions...
# At end of day, close the batch
curl -X POST http://localhost:5000/api/closing/batch \
  -H "Content-Type: application/json" \
  -d '{
    "ecrReferenceNumber": "EOD-'$(date +%Y-%m-%d)'"
  }'
```

## Error Handling

The API returns appropriate HTTP status codes:
- **200 OK**: Transaction successful
- **400 Bad Request**: Invalid request (missing or invalid parameters)
- **499 Client Closed Request**: Request cancelled by client
- **500 Internal Server Error**: Server-side error
- **502 Bad Gateway**: PAX terminal communication error

All error responses include an `errorMessage` field with details.
