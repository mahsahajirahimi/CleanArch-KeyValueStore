# KeyValueStore

A key-value storage service built with **C# 12** and **.NET 10**, following **Clean Architecture** and **Domain-Driven Design (DDD)** principles.

## Architecture

- **Domain:** Business entities, value objects, and domain rules.
- **Application:** Use cases and interfaces.
- **Infrastructure:** Storage, caching, and security implementations.
- **Api:** HTTP endpoints and dependency injection.

Dependencies follow the Clean Architecture dependency rule, pointing toward the inner layers.



## Build

`dotnet build KeyValueStore.sln`

**Status:** Initial project scaffolding.