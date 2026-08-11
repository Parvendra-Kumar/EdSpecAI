# Architecture

## Dependency direction

```text
                Domain
                  ▲
                  │
             Application
             ▲    ▲    ▲
             │    │    │
Infrastructure  Agents  Validation
             ▲    ▲    ▲
             └────API───┘
```

Dependencies always point inward. `EdSpec.Domain` is independent of Azure OpenAI, agent frameworks, persistence, HTTP, and UI concerns.

## Responsibilities

- **Domain:** specification, assessment, review, approval, and audit concepts.
- **Application:** orchestration use cases and interfaces for persistence, agents, validation, and audit.
- **Infrastructure:** persistence and external system adapters.
- **Agents:** assessment creation and independent review agent implementations.
- **Validation:** deterministic specification and assessment rules.
- **API:** dependency composition and HTTP endpoints.

