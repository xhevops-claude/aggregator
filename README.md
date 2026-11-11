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

The solution skeleton does not exist yet. See the build order in
ARCHITECTURE.md.

## Politeness

- One connection per host, 1 request per second by default.
- robots.txt is respected.
- Retries back off on 5xx and 429.
- No bot-wall evasion, ever.
