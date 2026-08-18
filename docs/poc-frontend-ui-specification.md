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
| POST | `/api/specifications/drafts` | Create a specification draft |
| GET | `/api/specifications/{id}/versions/{version}` | Retrieve one specification version |
| PUT | `/api/specifications/{id}/versions/{version}` | Update an existing draft |
| POST | `/api/specifications/{id}/versions/{version}/approve` | Approve a specification version |
| POST | `/api/specifications/{id}/versions/{version}/assessments/generate` | Generate an assessment from an approved specification |

The UI must not present specification listing, assessment retrieval, review findings, assessment approval/rejection, audit events, or real authentication as working backend features. Static POC placeholders are acceptable.

## Demo login

Use these local accounts in `src/auth/demoUsers.ts`, with the comment `// Demo-only POC credentials. There is no real authentication in this prototype.`:

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

Simple shell with the EdSpec title, signed-in demo name, sign-out action, specification workflow navigation, and assessment output navigation. Do not require dashboard counts because no list/statistics endpoints exist.

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

Display the generated assessment ID, specification ID/version, status, creator, timestamp, and each returned question with its learning objective, difficulty, type, prompt, options, correct option, and points. Keep the returned assessment in React state for the current session because no assessment GET endpoint exists.

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
- The UI does not claim to call unavailable review, audit, list, delete, or assessment-get APIs.

## Incremental change request

### CHANGE-001 - NEW

On the Create Draft Page, change only the text color of the `Create a specification draft` heading from red to yellow. Do not change its text, typography, spacing, position, alignment, layout, other UI colors, components, or behavior.

Acceptance criteria: the heading is yellow and every other UI element remains unchanged.

Implementation instruction: implement only incremental changes whose status is `NEW`; use the base specification only as context; do not regenerate, recreate, or refactor unrelated UI.

## UI change request - navigation and specification catalog

- Remove the Review queue and Audit history tabs from the UI because those backend endpoints are not implemented.
- Remove the `Backend authoritative` label from the header; use a neutral `POC workspace` label instead.
- Rename user-facing `Create draft` labels to `Create specification` while retaining the existing draft API and workflow.
- Show specification ID, version, title, subject, status, and updated date in a responsive grid/catalog.
- Add an action on each approved specification to generate an assessment through the existing generate endpoint.
- Add a View assessments action that opens the generated assessment already held in the current React session.
- Keep the existing create, load, edit, save, approve, and assessment rendering flow intact.
- Do not claim that the catalog, assessment view, review queue, or audit history are backed by unavailable list/get/review/audit endpoints. Catalog entries may be POC seed data until a backend list endpoint exists.

## Canonical implementation notes for future developers

### Layout and navigation

The current UI uses a dark left sidebar and a light content workspace. The sidebar contains only:

1. Overview
2. Specifications
3. Assessments

Review queue and Audit history must not appear in navigation because there are no corresponding working backend endpoints. The header uses the neutral label `POC workspace`; do not display `Backend authoritative` as a user-facing tab or feature.

The Overview screen contains a greeting, New specification button, three summary cards, recent activity, and a Needs your attention panel. These summary values are POC presentation data and must not be described as list/statistics API responses.

### Specification catalog

Until a specification-list endpoint is implemented, render the supplied POC seed records in a responsive card grid. Each card shows:

- specification ID;
- version;
- title and subject;
- draft/approved status;
- last updated date;
- Open/Load action;
- `Generate assessment` for approved records only.

The catalog must not invent a GET list API. Use the existing GET-by-ID/version endpoint when opening a record. The generated assessment is stored in React state for the current browser session, and the Assessments navigation opens that result through a `View assessment` action. There is intentionally no assessment GET call.

### User-facing labels and workflow

Use `Create specification` for the creation page heading, navigation/action labels, and creation button. The backend still creates a resource with status `draft`; renaming the UI label must not rename the API route or status. After creation, show the specification detail screen with:

1. Save (draft only)
2. Approve as the signed-in demo user (draft only)
3. Generate assessment (approved only)

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
- Preserved the limitation that review, audit, specification-list, and assessment-get APIs are not implemented.
