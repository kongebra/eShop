# eShop — Agent Instructions

## Project
eShop is a reference microservices application built with .NET Aspire.
It demonstrates cloud-native patterns: event-driven communication (RabbitMQ), gRPC, Minimal APIs, and EF Core with PostgreSQL.

Services: Catalog.API · Basket.API (gRPC) · Ordering.API (CQRS/MediatR) · Identity.API · WebApp (Blazor)

## Commands

Always run the application from the solution root:

```bash
aspire run
```

Run all tests:

```bash
dotnet test
```

## Conventions

- All code in English. Comments may be in Norwegian.
- Follow .NET naming conventions and best practices.
- Use async/await for all I/O operations.
- Write clean, readable code with meaningful variable names.
- Catalog and Ordering APIs use .NET Minimal API — no MVC controllers.
- No hardcoded secrets, connection strings, or credentials in code. Use configuration or environment variables.

## Testing

Write tests for all new functionality. Use the existing test projects as reference.

## Agent principles

- Ask clarifying questions before writing code — do not guess requirements.
- Prefer the simplest solution.
- Touch only what the task requires. No unrequested refactoring.