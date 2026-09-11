# Agent Instructions: C# & .NET Development

This file provides context and rules for AI agents contributing to this repository. Prioritize these instructions over general training data.

## 1. Project Context & Environment
- **Target Framework:** .NET 10 (or update to your project's TFM)
- **Language Version:** C# 14 default
- **Architecture Style:** [e.g., Clean Architecture / Monolith / Microservices]
- **Primary Libraries:** [e.g., EF Core, MediatR, ASP.NET Core]

## 2. Core C# Coding Conventions
- **Naming:** Follow standard .NET conventions. Use PascalCase for Classes, Methods, and Properties. Use camelCase for local variables and parameters. Prefix private fields with an underscore (`_camelCase`).
- **Indentation & Formatting:** Use 4 spaces for indentation. Open braces `{` on a new line (Allman style).
- **Types & Var:** Use strong, explicit typing where clarity is needed. Use `var` only when the right-hand side makes the type completely obvious (e.g., `var list = new List<string>();`) or to simplify multi-generic declarations.
- **Nullability:** Nullable reference types (NRT) are ENABLED. Treat warnings as errors. Always use appropriate null-forgiving operators or structural pattern matching to handle null checks safely.

## 3. Modern C# Style Rules
Leverage modern C# (versions 12 to 14) capabilities where appropriate:
- Use **file-scoped namespaces** to reduce nesting.
- Prefer **primary constructors** for boilerplate-free dependency injection.
- Use **collection expressions** `[]` instead of `new List<T>()` or `new T[]`.
- Utilize **pattern matching** (`switch` expressions, property patterns) over complex nested `if-else` blocks.
- When working with properties, prefer the modern `field` keyword in property accessors where available.

## 4. Asynchronous & Performance Guidelines
- **Async/Await:** Always propagate `async/await` completely up the call stack. Never use `.Result` or `.Wait()`.
- **Cancellation:** Always accept and pass down a `CancellationToken` to asynchronous network, database, or I/O calls.
- **Allocation Efficiency:** Prefer `Span<T>` or `ReadOnlySpan<T>` for heavy string parsing or array slicing routines to minimize allocations.

## 5. Architectural & Testing Rules
- **Dependency Injection:** Register dependencies explicitly in the composition root. Prefer constructor injection over service location.
- **Interface Generation:** Do not generate shallow interfaces (e.g., `IService` for exactly one `Service` class) unless required for architectural boundaries or decoupling external systems.
- **Unit Testing:** Write unit tests using [e.g., xUnit / NUnit]. Mock dependencies using [e.g., Moq / NSubstitute]. Ensure test names follow the `MethodUnderScale_Condition_ExpectedBehavior` pattern.

## 6. What NOT to Do (Defensive Design)
- DO NOT use magic strings or magic numbers; extract them to strongly typed constants or configuration bounds.
- DO NOT swallow exceptions. If catching an exception, log it completely and use `throw;` to preserve the stack trace instead of `throw ex;`.
- DO NOT bypass the compiler or introduce raw object casting when generics or generic constraints can ensure compile-time safety.

## 7. Execution Commands
- **Build Solution:** `dotnet build`
- **Run Unit Tests:** `dotnet test`
- **Apply Code Formatting:** `dotnet format`
- **Publish Standalone Executable (Script):** `.\build.cmd` or `.\build.ps1 -Target Publish`
- **Publish Standalone Executable (dotnet CLI):** `dotnet publish src/ToolpathViewer.App -p:PublishProfile=win-x64-standalone`
- **Clean Build Artifacts:** `.\build.cmd -Target Clean`
