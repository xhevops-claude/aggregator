# Collector

A multi-purpose ingestion platform in C# on .NET 10 / ASP.NET Core, with
RabbitMQ between stages and MongoDB as the store.

First project: sportswear shoes in North Macedonia, from five shops on two
platforms. Every shop exposes the brand's own article code as `sku`
(adidas `HP6005`, Nike `IB1895-006`), so one product can be matched across
shops and against the brands' own catalogs.

## Where to read

| Doc | What it holds |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | The design and the vocabulary |
| [docs/RESUME-HERE.md](docs/RESUME-HERE.md) | Current state and next steps |
| [docs/research/sources.md](docs/research/sources.md) | Every source, SKU levels, fetching defaults |
| [docs/research/nbshop-platform.md](docs/research/nbshop-platform.md) | Harvest and parse rules for Sport Vision, Sport Reality, Buzz |
| [docs/research/sport-m-platform.md](docs/research/sport-m-platform.md) | Harvest and parse rules for Sport M, Intersport |

## Build

```bash
dotnet build
dotnet test
```

- `dotnet test` needs the rig up: the Lake, Messaging and Storing tests use
  MongoDB and RabbitMQ, each in a throwaway database or name prefix.

## Local rig

RabbitMQ and MongoDB run in Docker, from `rig/compose.yaml`. On Windows
the engine lives in WSL. Keep the rig in a terminal of its own: WSL stops
its VM, and the containers with it, once no `wsl` session is open.

```bash
wsl docker compose -f rig/compose.yaml up
```

```bash
dotnet run --project src/Collector.Cli -- db init
```

```bash
dotnet run --project src/Collector.Cli -- mq init
```

```bash
dotnet run --project src/Collector.Cli -- seed
```

| Service | Address | Login |
|---|---|---|
| MongoDB, replica set `rs0` | `mongodb://localhost:27117/?replicaSet=rs0` | none |
| RabbitMQ | `amqp://localhost:5673` | `collector` / `collector` |
| RabbitMQ management | http://localhost:15673 | `collector` / `collector` |

- The ports are offset from the defaults, so the rig runs next to other
  stacks that use 27017 and 5672.
- `COLLECTOR_MONGO` overrides the MongoDB connection string.
- `db init` checks that MongoDB is a replica set and creates every
  index. It is safe to run again.
- `mq init` declares the exchanges and queues. It is safe to run again.
- `seed` creates the project `mk-shoes`: operations, providers, sources,
  jobs, the `dev` strategy. It is safe to run again.
- `COLLECTOR_RABBIT` overrides the RabbitMQ URI.
- `COLLECTOR_LAKE` overrides the lake folder; default `.lake/` in the repo
  root.

## Dev CLI

No worker consumes the queues yet. The CLI runs every stage inline, one
command at a time.

| Command | Does |
|---|---|
| `harvest <operation> <command>` | One harvest command into the lake. `--listing`, `--brand`, `--limit`, `--target <id>[=<url>]`, `--from-store` |
| `adapt <operation> [--key <lake key>]` | Latest lake items of the leaf into staging |
| `apply [--batch <n>]` | Pending staging documents into the store |
| `stats` | Counts per collection |

- `--from-store` picks listings of the leaf that have no details yet and
  whose product page was not harvested before; `--retry` drops the second
  condition.
- `--target <id>` always fetches, even a page already in the lake.
- `--limit` caps pages for listing walks and targets for product pages.

A full pass for Sport M, adidas, one catalog page then three product pages:

```bash
dotnet run --project src/Collector.Cli -- harvest clothing/shoes/sportm sportm.catalog-pages --brand adidas --limit 1
```

```bash
dotnet run --project src/Collector.Cli -- adapt clothing/shoes/sportm
```

```bash
dotnet run --project src/Collector.Cli -- apply
```

```bash
dotnet run --project src/Collector.Cli -- harvest clothing/shoes/sportm sportm.product-page --from-store --limit 3
```

Then `adapt` and `apply` again, and `stats`.

## Politeness

- One connection per host, 1 request per second by default.
- robots.txt is respected.
- Retries back off on 5xx and 429.
- No bot-wall evasion, ever.
