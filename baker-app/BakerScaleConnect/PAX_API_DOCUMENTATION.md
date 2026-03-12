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

## Error Handling

The API returns appropriate HTTP status codes:
- **200 OK**: Transaction successful
- **400 Bad Request**: Invalid request (missing or invalid parameters)
- **500 Internal Server Error**: Server-side error
- **502 Bad Gateway**: PAX terminal communication error

All error responses include an `errorMessage` field with details.
