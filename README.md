# EdSpec AI

EdSpec AI is a spec-driven POC for generating and independently reviewing a synthetic EdTech assessment.

## Solution structure

- `EdSpec.Domain` contains business entities and rules and has no AI or infrastructure dependencies.
- `EdSpec.Application` contains use cases and ports and depends only on Domain.
- `EdSpec.Infrastructure`, `EdSpec.Agents`, and `EdSpec.Validation` implement Application ports.
- `EdSpec.Api` is the composition root.
- `EdSpec.UnitTests` and `EdSpec.IntegrationTests` verify the solution.
- `web/edspec-ui` contains the React/TypeScript dashboard.

## Build

```powershell
dotnet build EdSpecAI.sln
dotnet test EdSpecAI.sln
```

```powershell
Set-Location web/edspec-ui
npm install
npm run build
```

## POC scenario

End users can create an approved specification for any subject or topic, then generate and independently review an MCQ assessment from that specification.
