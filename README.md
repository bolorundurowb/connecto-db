# connecto-db

A Firebase-inspired realtime database built with **ASP.NET Core**, **SignalR**, **EF Core**, and **SQLite**. Clients subscribe to named collections and receive live push notifications when records are created, updated, or deleted — without polling.


---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| API / Realtime | ASP.NET Core + SignalR |
| Database | SQLite (via EF Core) |
| Auth | JWT Bearer tokens (BCrypt password hashing) |

---

## Setup

### Prerequisites

- .NET 10 SDK

### Environment Variables

Create a `.env` file in the `server/` directory (see `.env.example`):

```
SECRET=your-jwt-signing-secret-min-32-chars
```

### Run

```bash
cd server
dotnet run
```

The server starts on `http://localhost:5000` by default. The SQLite database file (`connecto-core.db`) is created automatically on first run.

---

## Authentication

All hub connections and endpoints require a valid JWT. Obtain a token via the REST API, then pass it as the `access_token` query string when connecting to a hub.

### REST Endpoints

#### `POST /api/auth/register`

```json
{ "username": "alice", "password": "s3cret", "firstName": "Alice", "lastName": "Smith" }
```

#### `POST /api/auth/login`

```json
{ "username": "alice", "password": "s3cret" }
```

Both return:

```json
{
  "user":      { "id": "...", "username": "alice", "firstName": "Alice", "lastName": "Smith", "createdAt": "..." },
  "token":     "<jwt>",
  "expiresAt": "..."
}
```

Tokens expire after **14 days**.

---

## SignalR Hubs

Connect with the JWT token in the query string:

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/collection-stream?access_token=<token>")
  .build();
```

---

### `CollectionStreamHub` — `/collection-stream`

Manages collections (tables). Schema changes are broadcast to **all** connected clients.

| Client → Server | Arguments | Description |
|---|---|---|
| `ListTables` | — | Request the current list of collections |
| `CreateTable` | `tableName: string` | Create a new collection |
| `DeleteTable` | `tableName: string` | Drop an existing collection |

| Server → Client | Payload | Trigger |
|---|---|---|
| `TablesRequested` | `string[]` | Response to `ListTables` (caller only) |
| `TableCreated` | `tableName: string` | A new collection was created (all clients) |
| `TableDeleted` | `tableName: string` | A collection was dropped (all clients) |
| `HubError` | `message: string` | An operation failed (caller only) |

**Table name rules:** must match `^[a-zA-Z_][a-zA-Z0-9_]*$`. Reserved names (`users`, `sqlite_sequence`, etc.) are rejected.

---

### `DataStreamHub` — `/data-stream`

Manages records within collections. Uses a **subscribe/unsubscribe** model — clients only receive mutation events for tables they have explicitly subscribed to.

| Client → Server | Arguments | Description |
|---|---|---|
| `SubscribeToTable` | `tableName: string` | Start receiving realtime events for a collection |
| `UnsubscribeFromTable` | `tableName: string` | Stop receiving events for a collection |
| `GetAllRecords` | `tableName: string` | Fetch all records in a collection |
| `GetRecord` | `tableName: string`, `id: Guid` | Fetch a single record by ID |
| `UpsertDataRecord` | `tableName: string`, `data: object` | Create or update a record. Include `"id"` in `data` to update; omit to create |
| `DeleteRecord` | `tableName: string`, `id: Guid` | Delete a record by ID |

| Server → Client | Payload | Trigger |
|---|---|---|
| `EntitiesRequested` | `tableName`, `records[]` | Response to `GetAllRecords` (caller only) |
| `EntityRequested` | `tableName`, `record` | Response to `GetRecord` (caller only) |
| `EntityCreated` | `tableName`, `record` | A record was created (table subscribers only) |
| `EntityUpdated` | `tableName`, `record` | A record was updated (table subscribers only) |
| `EntityDeleted` | `tableName`, `id: Guid` | A record was deleted (table subscribers only) |
| `HubError` | `message: string` | An operation failed (caller only) |

#### Record format

Records are arbitrary JSON objects. When a record is returned from the server, an `"id"` field (UUID string) is always present:

```json
{ "id": "3fa85f64-...", "name": "Alice", "score": 42 }
```

To **update**, include the `"id"` in the payload. To **create**, omit it.

---

## Architecture

```
┌─────────────────────────────────────────────┐
│               ASP.NET Core                  │
│                                             │
│  REST                   SignalR             │
│  AuthController         CollectionStreamHub │
│                         DataStreamHub       │
│         ↓                       ↓           │
│  AuthService     CollectionService          │
│                  DataService                │
│                                             │
│         └────────────┬────────────┘         │
│                 AppDbContext                 │
│                 (EF Core + SQLite)           │
└─────────────────────────────────────────────┘
```

- **Auth** is handled by a standard REST controller returning JWTs.
- **Collections** (tables) and **records** are managed entirely through SignalR hubs.
- The SQLite database is a single file (`connecto-core.db`). The `users` table is managed by EF Core; all other tables are created dynamically by clients at runtime.
- Real-time fanout uses **SignalR Groups** — one group per collection name. Clients must explicitly subscribe to receive mutation events.
