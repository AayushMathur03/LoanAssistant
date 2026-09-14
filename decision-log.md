# Decision Log

Record of key architectural, technical, and implementation decisions for the project.

---

## ADR-001: Strict Clean Architecture Layer Boundaries
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: The project spec requires Clean Architecture with strict layer dependency rules (`Loan.Domain` -> `Loan.Application` -> `Loan.Infrastructure` / `Loan.Web` / `Loan.Workers`).
- **Decision**: `Loan.Domain` will have ZERO third-party NuGet package dependencies (no EF Core, no Azure SDKs, no Semantic Kernel, no ASP.NET Core). All domain rules (DTI, LTV, Credit Score evaluation) are 100% deterministic C# logic (**BR-01**, **BR-03**).
- **Consequences**: Ensures high testability with standard NUnit unit tests and prevents LLM halluncinations from influencing financial rules.

---

## ADR-002: Modern Vanilla CSS + Vanilla JS Presentation Layer
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Requirement specifies ASP.NET Core MVC + HTML/CSS + vanilla JavaScript only (no heavy SPA frameworks like React/Angular/Vue/Blazor). UI must look presentation/demo-ready and polished.
- **Decision**: Custom responsive design system using CSS custom properties (variables), modern typography (Inter/Roboto), card grids, status badges, confidence indicators, side-by-side document review, toast notifications, and modal dialogs built with vanilla JS.
- **Consequences**: Fast page loads, zero extra npm dependency bloat, native Razor view rendering with streaming capabilities where needed.
