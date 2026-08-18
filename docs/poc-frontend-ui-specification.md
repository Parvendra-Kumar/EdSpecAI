# EdSpec AI - POC Frontend UI Specification

**Purpose:** Implement the POC frontend for the currently implemented ASP.NET Core backend.
**Frontend:** React + TypeScript + Vite  
**Backend:** ASP.NET Core Web API  
**Scope:** Local/demo POC only

## POC workflow

Demo login -> Create draft specification -> Load specification version -> Edit draft -> Approve specification -> Generate assessment -> View generated questions.

The frontend must not implement AI behavior, persistence, assessment review logic, or business validation as a replacement for the backend. The backend response is authoritative.

## Available backend endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/specifications` | List stored specification versions for selection and catalog views |
| POST | `/api/specifications/drafts` | Create a specification draft |
| GET | `/api/specifications/{id}/versions/{version}` | Retrieve one specification version |
| PUT | `/api/specifications/{id}/versions/{version}` | Update an existing draft |
| DELETE | `/api/specifications/{id}/versions/{version}` | Delete a specification version and write an audit event |
| POST | `/api/specifications/{id}/versions/{version}/approve` | Approve a specification version |
| POST | `/api/specifications/{id}/versions/{version}/assessments/generate` | Generate an assessment from an approved specification |
| GET | `/api/assessments` | List persisted generated-assessment summaries |
| GET | `/api/assessments/{assessmentId}` | Retrieve a persisted generated assessment |
| GET | `/api/assessments/{assessmentId}/download` | Download a teacher-facing HTML copy of an assessment |

The specification list, specification detail/update/delete, assessment list/detail, and assessment download endpoints are implemented. Assessment review is performed by the existing backend workflow and persisted with audit events, but there is no separate review-management screen or real authentication in this local POC.

## Demo login

Use these local accounts in `web/edspec-ui/src/App.tsx`, with the comment `// Demo-only POC credentials. There is no real authentication in this prototype.`:

| Email | Password | Demo name | POC use |
|---|---|---|---|
| `teacher@edspec.demo` | `Teacher@123` | Demo Teacher | Create and generate |
| `reviewer@edspec.demo` | `Reviewer@123` | Demo Reviewer | Approve and inspect |
| `student@edspec.demo` | `Student@123` | Demo Student | Display-only identity |
| `admin@edspec.demo` | `Admin@123` | Demo Admin | Display-only identity |

Login compares local credentials, stores only the selected profile in React state or `sessionStorage`, and supports sign-out back to login. Invalid credentials show `Invalid demo email or password`. Use the signed-in demo name for `approvedBy` and `requestedBy`.

## Required screens and behavior

### Login

Email input, password input, sign-in button, invalid-credentials message, and a small demo-credentials hint.

### Workspace

Simple shell with the EdSpec title, signed-in demo name, sign-out action, specification workflow navigation, and assessment output navigation. Overview counts remain presentation-only POC values and must not be described as statistics API responses.

### Create specification

Map the form directly to `CreateDraftSpecificationRequest`: ID, version, title, subject, learning objective, question rules, difficulty distribution, and scoring rules. Validate the backend constraints early where helpful, but always display backend `400` responses.

Cross-field rules:

```text
easy + medium + hard = totalQuestions
totalPoints = totalQuestions * pointsPerQuestion
```

### Specification detail/edit

Load one ID/version through the GET endpoint and display identity, status, content, rules, approval information, and timestamps. Drafts can be edited, saved, or approved. Approved versions are read-only and can generate an assessment.

Approval body:

```json
{ "approvedBy": "Demo Reviewer" }
```

### Assessment output

Display the generated assessment ID, specification ID/version, status, creator, timestamp, and each returned question with its learning objective, difficulty, type, prompt, options, correct option, and points. The Assessments page loads persisted assessment summaries, opens the selected assessment through the GET endpoint, and provides a teacher download action.

## TypeScript models

```ts
export interface SpecificationDraft {
  id: string
  version: string
  status: string
  title: string
  subject: string
  learningObjective: string
  questionRules: { totalQuestions: number; questionType: string; optionsPerQuestion: number }
  difficultyDistribution: { easy: number; medium: number; hard: number }
  scoringRules: { pointsPerQuestion: number; totalPoints: number }
  approval: { required: boolean; approvedBy: string | null; approvedAt: string | null }
  createdAt: string
  updatedAt: string
}

export interface GeneratedAssessment {
  id: string
  specificationId: string
  specificationVersion: string
  status: string
  questions: GeneratedQuestion[]
  createdBy: string
  createdAt: string
}

export interface GeneratedQuestion {
  id: string
  learningObjective: string
  difficulty: string
  questionType: string
  prompt: string
  options: { id: string; text: string }[]
  correctOptionId: string
  points: number
}
```

## UI states

Every API action must support loading with duplicate-click prevention, success confirmation, understandable `400`, `404`, `409`, and `502` messages, retry for `502` and network failures, and preservation of form values when an action fails.

## Suggested project structure

```text
src/
├── api/
│   ├── client.ts
│   ├── specificationsApi.ts
│   └── assessmentsApi.ts
├── auth/demoUsers.ts
├── components/
│   ├── AppShell.tsx
│   ├── ErrorSummary.tsx
│   ├── LoadingState.tsx
│   └── StatusBadge.tsx
├── pages/
│   ├── LoginPage.tsx
│   ├── SpecificationPage.tsx
│   └── AssessmentPage.tsx
└── App.tsx
```

## Acceptance criteria

- All four demo accounts and invalid-credential handling work.
- Sign-out returns to login.
- A valid draft can be created, loaded, edited, saved, and approved through the API.
- Assessment generation is disabled until approval and uses `requestedBy`.
- Returned questions and options render correctly.
- Backend `400`, `404`, `409`, and `502` responses are visible and understandable.
- Specification and assessment list/detail/delete/download actions use the implemented backend endpoints; the UI does not expose unavailable review-management or real-authentication features.

## Incremental change request

### CHANGE-001 - NEW

On the Create Draft Page, change only the text color of the `Create a specification draft` heading from red to yellow. Do not change its text, typography, spacing, position, alignment, layout, other UI colors, components, or behavior.

Acceptance criteria: the heading is yellow and every other UI element remains unchanged.

Implementation instruction: implement only incremental changes whose status is `NEW`; use the base specification only as context; do not regenerate, recreate, or refactor unrelated UI.

## UI change request - navigation and specification catalog

- Remove the Review queue and Audit history tabs from the UI because there are no corresponding user-facing management endpoints.
- Remove the `Backend authoritative` label from the header; use a neutral `POC workspace` label instead.
- Rename user-facing `Create draft` labels to `Create specification` while retaining the existing draft API and workflow.
- Show specification ID, version, title, subject, learning objective, status, rules, and updated date in a responsive catalog.
- Allow a specification to be opened and edited from the catalog. Save updates through the existing PUT workflow and show a success toast.
- Add a trash-icon delete action using the backend DELETE endpoint and preserve the audit-log pattern.
- Add an action on approved specifications to generate an assessment through the existing generation endpoint.
- Add a persisted Assessments page with list, detail, loading, empty, retry, error, and download states.
- Keep the existing create, load, edit, save, approve, generate, and assessment rendering flow intact.

## Canonical implementation notes for future developers

### Layout and navigation

The current UI uses a dark left sidebar and a light content workspace. The sidebar contains:

1. Overview
2. Create Specification
3. Assessments
4. Generate assessment
5. View all Specifications

Review queue and Audit history must not appear in navigation because there are no corresponding working backend endpoints. The header uses the neutral label `POC workspace`; do not display `Backend authoritative` as a user-facing tab or feature.

The Overview screen contains a greeting, New specification button, three summary cards, recent activity, and a Needs your attention panel. These summary values are POC presentation data and must not be described as list/statistics API responses.

### Specification catalog

Load stored specification versions through `GET /api/specifications` and render them in a responsive card grid or selection control. Each item shows:

- specification ID;
- version;
- title and subject;
- draft/approved status;
- last updated date;
- question count and total points;
- Open/Edit action;
- delete trash icon;
- `Generate assessment` for approved records only.

Use the existing GET-by-ID/version endpoint when opening a record. Save calls the existing PUT endpoint and updates the list immediately from the API response. Editing an approved specification clears its approval in the backend and returns it to draft status, requiring re-approval before generation. Deletion calls the backend DELETE endpoint with the signed-in user's name as `deletedBy`.

The Assessments navigation calls `GET /api/assessments`, opens a selected record through `GET /api/assessments/{assessmentId}`, and provides `GET /api/assessments/{assessmentId}/download` for a teacher-facing HTML download.

### User-facing labels and workflow

Use `Create specification` for the creation page heading, navigation/action labels, and creation button. The backend still creates a resource with status `draft`; renaming the UI label must not rename the API route or status. After creation, show the specification detail screen with:

1. Save (draft only)
2. Approve as the signed-in demo user (draft only)
3. Generate assessment (approved only)

The Generate assessment tab loads stored specifications into a selector, shows the selected specification's question and scoring rules, accepts the requester name, and calls the existing generation endpoint. Only approved versions are selectable. Loading, empty, API failure, retry, and missing-approved-specification states are visible to the user.

Approved fields are read-only. Do not label Save, Approve, or Generate actions as `Create specification`.

### API client and local development configuration

The frontend calls relative `/api/...` paths. Vite must proxy them to the HTTP ASP.NET development profile:

```ts
server: {
  proxy: {
    '/api': { target: 'http://localhost:5246', changeOrigin: true }
  }
}
```

Run the API with the HTTP profile and the UI separately:

```powershell
dotnet run --project src/EdSpec.Api --launch-profile http
cd web/edspec-ui
npm install
npm run dev
```

The development API must expose `http://localhost:5246`; the frontend normally runs at `http://localhost:5173`. In development, HTTPS redirection is disabled so the Vite HTTP proxy does not follow a redirect to an inactive HTTPS port. Production must enforce HTTPS at the hosting layer.

### Troubleshooting 404, 401, and 500

#### 404 Not Found from `localhost:5173`

If DevTools shows a request such as `http://localhost:5173/api/specifications/drafts` returning 404, the Vite proxy is missing or the dev server was not restarted after changing `vite.config.ts`. Restart Vite and verify the proxy target is port `5246`.

#### 401 Unauthorized

The POC login is local-only and does not authenticate against ASP.NET. A 401 is therefore normally from Azure OpenAI during assessment generation, not from the demo login. Check that `AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, and `AzureOpenAI:DeploymentName` are valid for the Azure resource and deployment. Never commit an API key in `appsettings*.json`; use .NET User Secrets, environment variables, or Key Vault:

```powershell
dotnet user-secrets init --project src/EdSpec.Api
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/" --project src/EdSpec.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "<secret>" --project src/EdSpec.Api
dotnet user-secrets set "AzureOpenAI:DeploymentName" "<deployment>" --project src/EdSpec.Api
```

Any key previously pasted into chat, source, or a committed settings file must be revoked and replaced.

#### 500 Internal Server Error

First distinguish the source. A plain-text 500 from Vite usually means the proxy could not connect to the API (wrong port, API stopped, or HTTPS mismatch). A JSON 500 means ASP.NET threw an exception. Confirm the API is listening before testing the UI:

```powershell
Test-NetConnection localhost -Port 5246
```

Then inspect the `dotnet run` terminal for the exception. Common causes are missing Azure settings during API startup, invalid/corrupt JSON store files, a duplicate specification ID/version, or an Azure agent failure during generation. Create-draft should not call Azure; generation does. A duplicate ID/version should be surfaced as 409, not retried as a new record. Preserve the form values when showing the error.

After changing `Program.cs`, `vite.config.ts`, or appsettings/user-secrets, stop and restart both processes and hard-refresh the browser with `Ctrl+Shift+R`.

### Change history represented by this implementation

- Replaced the original static review/audit-heavy screen with the backend-connected POC workflow.
- Added local demo login and session sign-out.
- Added create, load, edit, save, approve, generate, and assessment rendering states.
- Added seeded overview/catalog presentation data from the supplied specification records.
- Added responsive dark-sidebar/light-workspace styling and layout overrides.
- Added the Vite-to-ASP.NET development proxy and HTTP development profile handling.
- Added persisted specification catalog, update, delete, audit, assessment-list, assessment-detail, and download workflows.

## Session implementation update

This section records the implementation completed during the current feature session. It supersedes the earlier POC notes that described specification-list and assessment-get endpoints as unavailable.

### End-to-end functional flow

1. The user signs in with the existing demo identity flow.
2. The left navigation opens Overview, Create Specification, Assessments, Generate assessment, or View all Specifications in the main content area.
3. Create Specification sends the form to `POST /api/specifications/drafts`.
4. View all Specifications loads records dynamically from `GET /api/specifications`. It supports loading, empty, refresh/retry, API error, status display, and specification links.
5. Opening a specification loads `GET /api/specifications/{id}/versions/{version}`. The editor displays the specification fields, rules, approval details, and timestamps.
6. Save sends the edited fields to `PUT /api/specifications/{id}/versions/{version}`. The returned record replaces the list/detail state and a success toast displays `Specification saved successfully`.
7. If an approved specification is edited, the backend clears its approval metadata and changes its status to `draft`. It must be approved again before assessment generation.
8. The trash icon sends `DELETE /api/specifications/{id}/versions/{version}` with `{ "deletedBy": "<signed-in-user>" }`. The backend persists the deletion and writes `specification.deleted` to the audit log.
9. Generate assessment loads the same dynamic specification list, enables only approved versions, shows the selected rules, accepts `requestedBy`, and reuses the existing Semantic Kernel/Azure OpenAI workflow.
10. Assessments loads persisted summaries from `GET /api/assessments`. Selecting an item retrieves its full questions from `GET /api/assessments/{assessmentId}`. The detail view offers a Download assessment link.

### Assessment-generation and Azure OpenAI behavior

The API registers the existing Semantic Kernel workflow and Azure OpenAI chat-completion service when all three settings are available:

```text
AzureOpenAI:Endpoint
AzureOpenAI:ApiKey
AzureOpenAI:DeploymentName
```

When configuration is missing, the API remains available for specification operations and generation returns a clear `502 Bad Gateway` configuration message instead of failing API startup with an unhandled dependency-injection error. Unexpected generation/review failures are also translated into meaningful 502 responses. API keys must be supplied through user secrets, environment variables, or a secret store and must not be committed to source control.

### Frontend behavior and labels

- The left navigation labels are `Create Specification` and `View all Specifications`.
- Assessment and specification values are loaded from the backend; the frontend does not use hard-coded catalog values.
- Assessment and specification pages show loading, empty, refresh/retry, validation, 404, 500, 502, and Azure configuration feedback through the existing error area.
- Save, create, approve, delete, and generation actions prevent duplicate clicks while busy.
- Successful actions use the toast styling added for specification saves and related success notifications.
- The UI keeps the existing dark sidebar/light workspace design and uses the existing relative `/api` paths with Vite proxying to `http://localhost:5246`.

### Tests and builds executed

- `npm run build` in `web/edspec-ui` — passed.
- `dotnet test EdSpecAI.sln --no-restore -p:UseAppHost=false` — passed: 5 unit tests and 12 integration tests.
- API smoke check — `GET http://localhost:5246/api/specifications` returned `200 OK`.
- Frontend smoke check — `http://localhost:5173/src/App.tsx` served the updated navigation, save-toast, delete, and specification-library code.

### Files changed during this implementation

Frontend:

- `web/edspec-ui/src/App.tsx` — navigation, dynamic specification catalog, specification editor/save/delete flow, Generate assessment flow, persisted Assessments list/detail/download flow, loading/error/empty states, and toast notifications.
- `web/edspec-ui/src/assessment-library.css` — assessment list/detail layout and presentation.
- `web/edspec-ui/src/generator.css` — Generate assessment page styling.
- `web/edspec-ui/src/specification-library.css` — specification catalog and editor styling.
- `web/edspec-ui/src/specification-delete.css` — delete/trash-icon styling.
- `web/edspec-ui/src/toast.css` — save/success toast styling.
- `web/edspec-ui/src/ui-change-overrides.css` — removed the rule that hid navigation items after the third item.

Backend API and workflow:

- `src/EdSpec.Api/Controllers/SpecificationsController.cs` — specification list, update approval reset, delete endpoint, validation, and audit event.
- `src/EdSpec.Api/Controllers/AssessmentsController.cs` — assessment list, detail, download, and generation endpoints.
- `src/EdSpec.Api/Program.cs` — optional Azure OpenAI/Semantic Kernel registration so missing settings produce a generation-time configuration response.
- `src/EdSpec.Api/Workflows/SemanticKernelAssessmentWorkflowOrchestrator.cs` — meaningful Azure configuration and unexpected-failure handling.
- `src/EdSpec.Api/EdSpec.Api.csproj` — development user-secrets support.
- `src/EdSpec.Application/Assessments/AssessmentGenerationAgentResult.cs` — assessment repository list/detail contracts.
- `src/EdSpec.Application/Specifications/ISpecificationDraftRepository.cs` — specification delete contract.
- `src/EdSpec.Infrastructure/Assessments/JsonGeneratedAssessmentRepository.cs` — persisted assessment list/detail reads.
- `src/EdSpec.Infrastructure/Specifications/JsonSpecificationDraftRepository.cs` — persisted specification deletion.

Tests and persisted POC data:

- `tests/EdSpec.IntegrationTests/Assessments/AssessmentsControllerTests.cs` — assessment list, detail, download, and 502 coverage.
- `tests/EdSpec.IntegrationTests/Specifications/SpecificationsControllerTests.cs` — list ordering, approved-to-draft update, and delete coverage.
- `tests/EdSpec.IntegrationTests/Specifications/JsonSpecificationDraftRepositoryTests.cs` — persistence coverage for specification updates.
- `src/EdSpec.Api/specifications.json` — persisted specification records used by the catalog.
- `src/EdSpec.Api/assessments.json` — persisted generated assessments used by the Assessments page.
- `src/EdSpec.Api/assessment-reviews.json` — persisted workflow review results.
- `src/EdSpec.Api/audit-log.json` — persisted create/update/approve/delete/generation audit events.

Configuration and documentation:

- `artifacts/verify-build/EdSpec.Api/appsettings.json` — verify-build Azure OpenAI configuration shape.
- `artifacts/verify-build/EdSpec.Api/appsettings.Development.json` — development verify-build configuration shape.
- `docs/ui-specification.md` — aligned the broader UI specification with the dynamic specification-list endpoint.
- `docs/poc-frontend-ui-specification.md` — this consolidated implementation record.

The `artifacts/verify-build` settings are environment-specific. Keep real Azure OpenAI credentials out of source control and replace any exposed key immediately.
