# murmur-api

Murmur is a self-hosted, Discord-style chat backend. You run it yourself: people sign up, create servers, talk in text channels and private chats, and see messages, presence and typing live.

**Milestone 1** covers accounts, servers, text channels, real-time messages, direct messages, presence and typing.

This repository is the API. The web client lives in [murmur-web](https://github.com/xavidiaz/murmur-web).

## Project status

**Milestone 1: 5 / 47 API stories done**

Legend: ✅ done (links to the merged PR) · 🚧 in progress (links to the issue) · ⬜ not started

Each story's PR updates its row here and the progress line above.

### Accounts and sign-in

| Story | Tag | Status |
|---|---|---|
| Sign up | `API-AUTH-01` | ✅ [#2](https://github.com/xavidiaz/murmur-api/pull/2) |
| Sign-up mistakes are explained | `API-AUTH-02` | ✅ [#4](https://github.com/xavidiaz/murmur-api/pull/4) |
| Sign in | `API-AUTH-03` | ✅ [#6](https://github.com/xavidiaz/murmur-api/pull/6) |
| Who am I | `API-AUTH-04` | ⬜ |
| Staying signed in | `API-AUTH-05` | ✅ [#8](https://github.com/xavidiaz/murmur-api/pull/8) |
| A stolen refresh token is useless | `API-AUTH-06` | ✅ [#11](https://github.com/xavidiaz/murmur-api/pull/11) |
| Sign out | `API-AUTH-07` | ⬜ |
| Tokens expire | `API-AUTH-08` | ⬜ |

### Servers and members

| Story | Tag | Status |
|---|---|---|
| A new server starts in a sensible state | `API-SRV-01` | ⬜ |
| Create a server | `API-SRV-02` | ⬜ |
| See my servers | `API-SRV-03` | ⬜ |
| Edit a server | `API-SRV-04` | ⬜ |
| Delete a server | `API-SRV-05` | ⬜ |
| See who's in a server | `API-SRV-06` | ⬜ |
| Remove a member | `API-SRV-07` | ⬜ |
| Leave a server | `API-SRV-08` | ⬜ |

### Channels

| Story | Tag | Status |
|---|---|---|
| Create a channel | `API-CH-01` | ⬜ |
| See and edit channels | `API-CH-02` | ⬜ |
| Delete a channel | `API-CH-03` | ⬜ |
| Reorder channels | `API-CH-04` | ⬜ |

### Invites

| Story | Tag | Status |
|---|---|---|
| Invite rules | `API-INV-01` | ⬜ |
| Create an invite | `API-INV-02` | ⬜ |
| Preview an invite | `API-INV-03` | ⬜ |
| Join with an invite | `API-INV-04` | ⬜ |
| A single-use invite really is single-use | `API-INV-05` | ⬜ |

### Roles and permissions

| Story | Tag | Status |
|---|---|---|
| Working out what someone may do | `API-ROLE-01` | ⬜ |
| Manage roles | `API-ROLE-02` | ⬜ |
| Give members roles | `API-ROLE-03` | ⬜ |
| Moderators can't promote themselves | `API-ROLE-04` | ⬜ |
| Permissions actually block things | `API-ROLE-05` | ⬜ |

### Messages in channels

| Story | Tag | Status |
|---|---|---|
| Message rules | `API-MSG-01` | ⬜ |
| Post a message | `API-MSG-02` | ⬜ |
| Scroll back through history | `API-MSG-03` | ⬜ |
| Edit my message | `API-MSG-04` | ⬜ |
| Delete a message | `API-MSG-05` | ⬜ |

### Direct messages

| Story | Tag | Status |
|---|---|---|
| Start a private chat | `API-DM-01` | ⬜ |
| See my private chats | `API-DM-02` | ⬜ |
| Chat privately | `API-DM-03` | ⬜ |

### Live updates

| Story | Tag | Status |
|---|---|---|
| Connecting needs a valid sign-in | `API-RT-01` | ⬜ |
| New, edited and deleted messages appear live | `API-RT-02` | ⬜ |
| You can only watch channels you may see | `API-RT-03` | ⬜ |
| Server changes appear live for every member | `API-RT-04` | ⬜ |
| Joining and leaving appear live | `API-RT-05` | ⬜ |
| See who's online | `API-RT-06` | ⬜ |
| Who's online right now | `API-RT-07` | ⬜ |
| "Is typing…" in channels | `API-RT-08` | ⬜ |
| Private chats are live too | `API-RT-09` | ⬜ |

## Stack

- **.NET 10** minimal APIs, with **SignalR** for live updates
- **EF Core** with **Npgsql** on **PostgreSQL**
- **ASP.NET Identity** for accounts. Access tokens are **JWTs** that last 15 minutes. Refresh tokens rotate and are single-use: each refresh replaces the token, and reusing an old one ends that session.
- **xUnit v3** for unit tests. Acceptance tests use `WebApplicationFactory` and run against a real PostgreSQL.

## Repository layout

```
api.slnx
Directory.Build.props        # shared build settings (net10.0, nullable, warnings as errors)
Directory.Packages.props     # central package versions
src/
  Contracts/        # wire DTOs, routes, error codes, hub interfaces (BCL only; shared with the web client)
  Domain/           # entities and business rules; references nothing
  Application/      # use cases and ports (interfaces) → Domain, Contracts
  Infrastructure/   # EF Core DbContext, migrations, Identity, token service → Application, Domain
  Api/              # Program.cs, endpoints, SignalR hub → Application, Infrastructure, Contracts
tests/
  Domain.Tests/          # unit tests
  Application.Tests/     # unit tests (ports faked)
  Infrastructure.Tests/  # persistence tests against a throwaway PostgreSQL schema
  Api.AcceptanceTests/   # black-box HTTP + SignalR tests against the real API
```

The layers follow Clean Architecture. Dependencies point inward: **Domain ← Application ← Infrastructure ← Api**.
- Domain knows nothing about EF Core, Identity, ASP.NET or the wire contracts.
- Application talks to the outside world only through its own interfaces.
- Infrastructure and Api plug in the implementations.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A PostgreSQL server you control

### Configure

Settings for local development are kept in user secrets, never in the repository:

```bash
cd src/Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=murmur;Username=murmur;Password=<your password>"
dotnet user-secrets set "Jwt:SigningKey" "<a random string of at least 32 bytes>"
```

`Jwt:Issuer` and `Jwt:Audience` default to `murmur`.

### Database migrations

```bash
dotnet tool restore
# apply migrations at startup:
MIGRATE_ON_START=true dotnet run --project src/Api
# or add a new migration after changing the model:
dotnet ef migrations add <Name> -p src/Infrastructure -s src/Api -o Persistence/Migrations
```

When `MIGRATE_ON_START` is `true`, the API applies pending migrations before it starts serving. It is safe when several instances start together.

### Run

```bash
dotnet run --project src/Api
```

### Test

```bash
dotnet build api.slnx                               # must report 0 warnings
export TEST_PG="<connection string to a PostgreSQL you control>"
dotnet test api.slnx                                # everything
dotnet test api.slnx --filter "Story=API-AUTH-01"   # one story
```

The tests that need a database read `TEST_PG`.
- Each run creates its own throwaway database, or a `test_*` schema if the account may not create databases, and drops it at the end.
- Without `TEST_PG`, those tests are skipped and the unit tests still run.

## How work is done

1. Each feature is a **story**: a short user story with scenarios, tagged like `API-AUTH-01`.
2. Each story gets a GitHub **issue** labelled `story`.
3. Work happens on a branch named `story/<issue>-<slug>`, for example `story/1-sign-up`.
4. **Failing tests come first.** Acceptance tests are written from the scenarios before any code exists, along with unit tests for domain and application rules. Every test carries `[Trait("Story", "<tag>")]`.
5. The implementation makes them pass with zero build warnings.
6. A **pull request** closes the issue and updates the story's row in [Project status](#project-status).
7. It is merged after review.
