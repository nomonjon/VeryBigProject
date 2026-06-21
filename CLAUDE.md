# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution Overview

`intership.sln` — a .NET 8 microservices solution with two Angular frontends.

| Project | Type | Role |
|---|---|---|
| **GrpcServer** | ASP.NET Core Web | Product catalog exposed via gRPC |
| **TaskTracker** | ASP.NET Core Web | Task/project management — REST + GraphQL |
| **NotificationService** | Worker Service | Consumes RabbitMQ events, sends notifications |
| **LogPlatform** | ASP.NET Core Web | Centralized log viewer (MongoDB backend) |
| **Client** | Console | Manual gRPC test client |
| **GrpcServer.Tests** | xUnit (.NET 10) | Unit tests for GrpcServer |
| **TaskTracker.Tests** | xUnit (.NET 10) | Unit tests for TaskTracker |
| **TaskTrackerFrontend** | Angular 21 | Main UI for task management |
| **LogsFrontend** | Angular 17 | Logs dashboard UI |

## Commands

### Build & Run

```bash
# Build entire solution
dotnet build intership.sln

# Run individual backend services
dotnet run --project GrpcServer        # gRPC :5000
dotnet run --project TaskTracker       # REST+GraphQL :5100
dotnet run --project LogPlatform       # Logs API :5080
dotnet run --project NotificationService

# Run with Docker Compose (recommended — starts all services + infra)
docker-compose up
```

### Tests

```bash
dotnet test GrpcServer.Tests
dotnet test TaskTracker.Tests

# Single test
dotnet test TaskTracker.Tests --filter "FullyQualifiedName~MethodName"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Frontend

```bash
cd TaskTrackerFrontend && npm install && npm start   # :4200
cd LogsFrontend && npm install && npm start          # :4200
ng test   # Vitest-based unit tests
```

## Infrastructure Dependencies

All services depend on infrastructure started by `docker-compose.yml`:

| Service | Image | Port |
|---|---|---|
| PostgreSQL | postgres | 5432 |
| MongoDB | mongo | 27017 |
| RabbitMQ | rabbitmq:3-management | 5672 / 15672 |

Services connect via Docker network `my-shared-network`. Connection strings reference Docker service hostnames (e.g., `host=postgres`, `rabbitmq`).

## Architecture

### Inter-Service Communication

- **gRPC** — GrpcServer exposes `ProductService` (proto at `GrpcServer/Protos/product.proto`). TaskTracker calls it as a gRPC client to enrich task data with product info.
- **RabbitMQ events** — TaskTracker publishes `TaskStatusChangedEvent` to `task.status.changed` queue when a task's status changes. NotificationService consumes this queue. Shared contract lives in `NotificationService/Contracts/` and `TaskTracker/Contracts/`.
- **Structured logging pipeline** — All services use Serilog with a RabbitMQ sink. Each service writes to its own queue (`logs.GrpcServer`, `logs.TaskTracker`, etc.). LogPlatform's `LogConsumerWorker` consumes these and stores entries in MongoDB.

### TaskTracker API Surface

- **REST** — `ControllersFolder` handles users, projects, tasks, auth
- **GraphQL** (HotChocolate 15) — `/graphql` endpoint with:
  - `Query`: Projects, WorkTasks, Users (filtering, sorting, projections)
  - `Mutation`: Create/update users, projects, tasks; AddComment
  - `Subscription`: Real-time user creation/update events
  - `DataLoader`: `TasksByUserDataLoader` for batched task loading
- **JWT auth** — Bearer tokens, roles: `Admin`, `Manager`, `User`. `TokenService` issues tokens; BCrypt hashes passwords.

### Data Access

- **EF Core 8** + PostgreSQL — GrpcServer and TaskTracker use a generic `BaseRepository<T>` pattern with `DbContext`. Migrations live in each project's `Migrations/` folder.
- **MongoDB** — LogPlatform uses `MongoDB.Driver` directly for `LogEntry` documents.

### Standard Project Layout

```
/Controllers    REST endpoints
/Services       Business logic
/Repository     Data access (BaseRepository pattern)
/Models         Domain entities
/Dtos           Data transfer objects
/Mappers        Dto ↔ Model mapping
/GraphQL        HotChocolate resolvers (TaskTracker only)
/Protos         Proto3 definitions (GrpcServer only)
/Migrations     EF Core migrations
/Workers        Background/hosted services
```

### Testing Stack

xUnit 2.9.3 + AutoFixture + Moq. Tests mock service dependencies with Moq; AutoFixture generates test data. No integration test projects — tests are unit-level only.

## Key External Call

GrpcServer calls `http://randomprice:5059/api/Random/random-price` (a Docker-compose service) when creating products to assign a random price. This hostname only resolves inside Docker; running GrpcServer locally requires mocking or adjusting this URL.
