# connecto-db

A Firebase-inspired realtime database built with **ASP.NET Core**, **SignalR**, **EF Core**, and **SQLite**. Clients subscribe to named collections and receive live push notifications when records are created, updated, or deleted — without polling.

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| API / Realtime | ASP.NET Core + SignalR |
| Database | SQLite (via EF Core) |
| Auth | JWT Bearer tokens (BCrypt password hashing) |

---

## Server

### Prerequisites

- .NET 10 SDK (for building from source)

### Environment Variables

Create a `.env` file next to the server executable (see `server/.env.example`):

```
SECRET=your-jwt-signing-secret-min-32-chars
```

### Run from Source

```bash
cd server
dotnet run
```

The server starts on `http://localhost:5043`. The SQLite database (`connecto-core.db`) is created automatically.

### Build a Single Executable

```bash
cd server
dotnet publish -c Release -r linux-x64 --self-contained
# or: win-x64, osx-arm64, etc.
```

The output is a single file at `bin/Release/net10.0/<rid>/publish/connecto-server`. Copy it anywhere and run:

```bash
SECRET=my-secret ./connecto-server
```

Available runtime identifiers: `linux-x64`, `linux-arm64`, `win-x64`, `osx-x64`, `osx-arm64`.

### Run with Docker

```bash
# Build
docker build -t connecto-server ./server

# Run
docker run -p 5043:5043 -e SECRET=my-secret -v connecto-data:/app/data connecto-server
```

The `-v` flag persists the SQLite database across container restarts.

---

## Client Libraries

Official clients are available for five languages. Each handles authentication, hub connection, and realtime event subscriptions.

### TypeScript / JavaScript

```bash
npm install @connecto/client
```

```typescript
import { ConnectoClient } from "@connecto/client";

const client = new ConnectoClient("http://localhost:5043");

// Authenticate
await client.login({ username: "alice", "password": "s3cret" });

// Connect to SignalR hubs
await client.connect();

// Create a collection
await client.createTable("posts");

// Subscribe to realtime events
client.onEntityCreated((table, record) => {
  console.log(`New record in ${table}:`, record);
});

// Insert a record
const post = await client.upsert("posts", { title: "Hello", body: "World" });

// Fetch all records
const all = await client.getAllRecords("posts");

// Disconnect when done
await client.disconnect();
```

### .NET

```bash
dotnet add package Connecto.Client
```

```csharp
using Connecto.Client;

await using var client = new ConnectoClient("http://localhost:5043");

// Authenticate
await client.Login(new LoginReq("alice", "s3cret"));

// Connect
await client.Connect();

// Create a collection and insert a record
await client.CreateTable("posts");
await client.Upsert("posts", new FlexMap { ["title"] = "Hello", ["body"] = "World" });

// Fetch all records
var records = await client.GetAllRecords("posts");

// Realtime listeners
client.OnEntityCreated((table, record) => Console.WriteLine($"New: {table}"));
```

### Rust

```toml
# Cargo.toml
[dependencies]
connecto-client = "0.1"
tokio = { version = "1", features = ["macros", "rt-multi-thread"] }
```

```rust
use connecto_client::ConnectoClient;
use std::collections::HashMap;
use serde_json::json;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut client = ConnectoClient::new("http://localhost:5043");

    client.login("alice", "s3cret").await?;
    client.connect().await?;

    client.create_table("posts").await?;

    let mut data = HashMap::new();
    data.insert("title".to_string(), json!("Hello"));
    data.insert("body".to_string(), json!("World"));
    let post = client.upsert("posts", data).await?;

    let records = client.get_all_records("posts").await?;
    println!("{:?}", records);

    Ok(())
}
```

### Go

```bash
go get github.com/connecto/go-client/connecto
```

```go
package main

import (
    "fmt"
    "log"

    "github.com/connecto/go-client/connecto"
)

func main() {
    client := connecto.NewClient("http://localhost:5043")

    _, err := client.Login("alice", "s3cret")
    if err != nil {
        log.Fatal(err)
    }

    err = client.Connect()
    if err != nil {
        log.Fatal(err)
    }
    defer client.Disconnect()

    client.CreateTable("posts")

    data := connecto.FlexMap{"title": "Hello", "body": "World"}
    client.Upsert("posts", data)

    records, _ := client.GetAllRecords("posts")
    fmt.Println(records)

    client.OnEntityCreated(func(table string, record connecto.FlexMap) {
        fmt.Printf("New record in %s: %v\n", table, record)
    })

    select {} // keep alive for realtime events
}
```

### Java

```xml
<!-- pom.xml -->
<dependency>
    <groupId>com.connecto</groupId>
    <artifactId>connecto-client</artifactId>
    <version>0.1.0</version>
</dependency>
```

```java
import com.connecto.client.ConnectoClient;
import java.util.Map;

public class App {
    public static void main(String[] args) throws Exception {
        var client = new ConnectoClient("http://localhost:5043");

        client.login("alice", "s3cret");
        client.connect().get();

        client.createTable("posts").get();

        var data = Map.of("title", "Hello", "body", "World");
        client.upsert("posts", data).get();

        var records = client.getAllRecords("posts").get();
        System.out.println(records);

        client.onEntityCreated((table, record) ->
            System.out.printf("New record in %s: %s%n", table, record));

        Thread.currentThread().join(); // keep alive for realtime events
    }
}
```

---

## API Reference

### Authentication (REST)

All hub connections require a JWT. Tokens expire after 14 days.

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
  "user": { "id": "...", "username": "alice", "firstName": "Alice", "lastName": "Smith", "createdAt": "..." },
  "token": "<jwt>",
  "expiresAt": "..."
}
```

### SignalR Hubs

Pass the JWT as `?access_token=<token>` when connecting.

#### `CollectionStreamHub` — `/collection-stream`

| Client -> Server | Arguments | Description |
|---|---|---|
| `ListTables` | — | List all collections |
| `CreateTable` | `tableName: string` | Create a collection |
| `DeleteTable` | `tableName: string` | Drop a collection |

| Server -> Client | Payload | Trigger |
|---|---|---|
| `TablesRequested` | `string[]` | Response to `ListTables` (caller) |
| `TableCreated` | `tableName` | Collection created (all clients) |
| `TableDeleted` | `tableName` | Collection dropped (all clients) |
| `HubError` | `message` | Operation failed (caller) |

**Table names** must match `^[a-zA-Z_][a-zA-Z0-9_]*$` (max 128 chars). Reserved: `users`, `sqlite_sequence`, etc.

#### `DataStreamHub` — `/data-stream`

| Client -> Server | Arguments | Description |
|---|---|---|
| `SubscribeToTable` | `tableName` | Start receiving events |
| `UnsubscribeFromTable` | `tableName` | Stop receiving events |
| `GetAllRecords` | `tableName` | Fetch all records |
| `GetRecord` | `tableName`, `id` | Fetch one record |
| `UpsertDataRecord` | `tableName`, `data` | Create (no `id`) or update (with `id`) |
| `DeleteRecord` | `tableName`, `id` | Delete a record |

| Server -> Client | Payload | Trigger |
|---|---|---|
| `EntitiesRequested` | `tableName`, `records[]` | Response to `GetAllRecords` (caller) |
| `EntityRequested` | `tableName`, `record` | Response to `GetRecord` (caller) |
| `EntityCreated` | `tableName`, `record` | Record created (subscribers) |
| `EntityUpdated` | `tableName`, `record` | Record updated (subscribers) |
| `EntityDeleted` | `tableName`, `id` | Record deleted (subscribers) |
| `HubError` | `message` | Operation failed (caller) |

Records are arbitrary JSON objects. The server always includes an `"id"` field (UUID string):

```json
{ "id": "3fa85f64-...", "name": "Alice", "score": 42 }
```

---

## CI/CD

Each client library has a GitHub Actions workflow that builds on every push and publishes on tag push:

| Client | Tag Pattern | Publishes To | Secret |
|---|---|---|---|
| TypeScript | `typescript/v*` | npm | `NPM_TOKEN` |
| .NET | `dotnet/v*` | NuGet | `NUGET_TOKEN` |
| Rust | `rust/v*` | crates.io | `CRATES_IO_TOKEN` |
| Go | `go/v*` | GitHub Release | — |
| Java | `java/v*` | GitHub Packages | `GITHUB_TOKEN` |
| Server Docker | `server/v*` | ghcr.io | `GITHUB_TOKEN` |

Example release:

```bash
git tag typescript/v0.1.0 && git push origin typescript/v0.1.0
```

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

- **Auth** is a standard REST controller returning JWTs.
- **Collections** and **records** are managed entirely through SignalR hubs.
- The SQLite database is a single file. The `users` table is managed by EF Core; all other tables are created dynamically by clients at runtime.
- Real-time fanout uses **SignalR Groups** — one group per collection name.

## License

MIT
