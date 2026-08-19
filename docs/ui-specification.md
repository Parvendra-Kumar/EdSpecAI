# EdSpec AI — UI Specification

**Status:** Draft for POC implementation  
**Audience:** Frontend, Backend, QA and Architecture teams  
**Frontend:** React + TypeScript  
**Backend:** ASP.NET Core Web API  
**Last updated:** 2026-08-13

## 1. Purpose

The EdSpec UI is a reviewer workspace for managing versioned education specifications and generating assessments from approved specifications. It must make the backend workflow visible and traceable:

> Create draft → edit → validate → approve specification → generate assessment → inspect generated questions → approve or request correction.

The UI is a POC. It should support the current backend contracts and should not move AI, validation, persistence or approval rules into the browser.

## 2. Scope

### In scope

- Create and edit a specification draft.
- Load a specification by ID and semantic version.
- Display specification status and approval metadata.
- Approve a specification version.
- Generate an assessment from an approved specification.
- Display generated questions, options, correct option and points.
- Show backend validation and generation errors.
- Provide reviewer actions and an audit-friendly activity view.

### Out of scope for this POC

- Implementing Azure OpenAI or agent prompts in the frontend.
- Reimplementing C# validation rules in TypeScript.
- Editing generated assessment questions in the UI.
- Review-agent findings API (not currently implemented by the backend).
- Authentication and authorization (the current API accepts `ApprovedBy` and `RequestedBy` values from the client).

## 3. User and primary journey

The primary user is a human reviewer. The happy path is:

1. Open the specification editor.
2. Enter the title, subject, learning objective, question rules, difficulty distribution and scoring rules.
3. Save the draft.
4. Approve a specific version using the reviewer name.
5. Generate an assessment using the approved version.
6. Review the generated questions and their metadata.
7. If the backend returns validation errors, show them clearly and return the reviewer to the specification or generation step.

## 4. Information architecture

| Route | Screen | Purpose |
|---|---|---|
| `/` | Workspace overview | Show current specification and assessment activity. |
| `/specifications/new` | Create specification | Create a new draft specification. |
| `/specifications/:id/versions/:version` | Specification detail | View, edit or approve a specific version. |
| `/specifications/:id/versions/:version/edit` | Edit specification | Update an existing draft. |
| `/specifications/:id/versions/:version/assessment` | Assessment detail | Display generated assessment questions. |
| `/audit` | Activity history | POC-level traceability for user actions and API responses. |

The current prototype may use client-side tabs instead of a router, but these route boundaries should be preserved when the UI is connected to the API.

## 5. Screen requirements

### 5.1 Workspace overview

Display:

- Current approved specifications.
- Draft specifications.
- Recently generated assessments.
- Items requiring reviewer attention.
- Primary actions: **New specification**, **Open specification**, **Generate assessment**.

The overview is a convenience screen. Backend data remains authoritative.

### 5.2 Specification editor

Fields and validation:

| Field | Type | Required | Frontend validation |
|---|---|---:|---|
| `id` | text | No on create | Optional; backend slugifies it. |
| `version` | text | Yes on create | Must match `^\\d+\\.\\d+\\.\\d+$`, e.g. `1.0.0`. Immutable on update. |
| `title` | text | Yes | Minimum 3 characters. |
| `subject` | text | Yes | Minimum 3 characters. |
| `learningObjective` | textarea | Yes | Minimum 10 characters. |
| `questionRules.totalQuestions` | number | Yes | 1–100. |
| `questionRules.questionType` | text/select | Yes | Minimum 3 characters; POC default `multiple-choice`. |
| `questionRules.optionsPerQuestion` | number | Yes | 2–8. |
| `difficultyDistribution.easy` | number | Yes | 0–100. |
| `difficultyDistribution.medium` | number | Yes | 0–100. |
| `difficultyDistribution.hard` | number | Yes | 0–100. |
| `scoringRules.pointsPerQuestion` | number | Yes | 1–100. |
| `scoringRules.totalPoints` | number | Yes | 1–10,000. |

Inline cross-field messages:

- `easy + medium + hard` must equal `totalQuestions`.
- `totalPoints` must equal `totalQuestions × pointsPerQuestion`.

The UI may show these errors before submit, but it must also render the API's `400 ValidationProblemDetails` response because the backend is the final authority.

### 5.3 Specification detail

Display:

- ID, title, version and status.
- Subject and learning objective.
- Question rules, difficulty distribution and scoring rules.
- Created and updated timestamps.
- Approval information: required, approved by and approved at.

Actions:

- **Edit draft**: available only when `status === "draft"`.
- **Approve version**: available for a draft.
- **Generate assessment**: available only when `status === "approved"`.

Approval requires an `approvedBy` value with at least 2 characters.

### 5.4 Assessment detail

Display the response from `POST .../assessments/generate`:

- Assessment ID.
- Specification ID and version.
- Assessment status.
- Created by and created timestamp.
- Question count.
- Every question's ID, learning objective, difficulty, question type, prompt, options, correct option and points.

Correct options should be visually marked, but the UI must not change the backend response. The current `GeneratedQuestion` contract uses `CorrectOptionId`; the frontend maps that ID to the matching `GeneratedOption`.

## 6. Backend integration contract

The API base URL must be configurable, for example `VITE_API_BASE_URL`. Do not hard-code a production URL.

### List specifications

```http
GET /api/specifications
```

Success: `200 OK`, response body is the stored specification versions. The UI may use this endpoint to populate specification catalogs and assessment-generation selectors. Draft versions remain visible for context but cannot be used for generation until approved.

### Create draft

```http
POST /api/specifications/drafts
Content-Type: application/json
```

```json
{
  "id": "algebra-basic-001",
  "version": "1.0.0",
  "title": "Basic Algebra",
  "subject": "Mathematics",
  "learningObjective": "Solve single-variable linear equations",
  "questionRules": {
    "totalQuestions": 5,
    "questionType": "multiple-choice",
    "optionsPerQuestion": 4
  },
  "difficultyDistribution": { "easy": 2, "medium": 2, "hard": 1 },
  "scoringRules": { "pointsPerQuestion": 2, "totalPoints": 10 }
}
```

Success: `201 Created`, response body is `SpecificationDraft`.

### Get a specification version

```http
GET /api/specifications/{id}/versions/{version}
```

Success: `200 OK`. Missing resource: `404 Not Found`.

### Update a draft

```http
PUT /api/specifications/{id}/versions/{version}
Content-Type: application/json
```

The request contains the editable fields from the create request, excluding `id` and `version`.

Success: `200 OK`. The UI must not show this action for an approved version.

### Approve a specification

```http
POST /api/specifications/{id}/versions/{version}/approve
Content-Type: application/json
```

```json
{ "approvedBy": "Arti Chauhan" }
```

Success: `200 OK`; the response status becomes `approved`.

### Generate an assessment

```http
POST /api/specifications/{id}/versions/{version}/assessments/generate
Content-Type: application/json
```

```json
{ "requestedBy": "Arti Chauhan" }
```

Success: `200 OK`, response body is `GeneratedAssessment`.

Possible responses:

- `400`: specification is not approved.
- `404`: specification/version does not exist.
- `502`: Azure OpenAI failed or generated output failed deterministic validation. Render `message` and, when present, the `errors` array.

## 7. UI state model

All API-backed screens must support these states:

| State | UI behavior |
|---|---|
| Loading | Show skeleton or spinner; disable duplicate actions. |
| Ready | Show server data and allowed actions. |
| Saving | Keep form values; show `Saving…`; disable submit. |
| Success | Show confirmation and refresh server data. |
| Validation error | Show field-level and summary errors; preserve entered values. |
| Not found | Show resource-not-found state with a link to specifications. |
| Conflict | Explain that the specification version already exists. |
| Generation failure | Show backend message and retry action. |
| Network failure | Show retry action and avoid losing form data. |

## 8. Component structure

```text
AppShell
├── Sidebar
├── TopBar
└── Page
    ├── PageHeader
    ├── StatusBadge
    └── Content
        ├── SpecificationList
        ├── SpecificationForm
        ├── SpecificationDetail
        ├── AssessmentSummary
        ├── QuestionCard
        ├── ErrorSummary
        └── AuditTimeline
```

Recommended shared types should mirror the C# records instead of inventing a second domain model:

```ts
type SpecificationStatus = 'draft' | 'approved'
type QuestionType = string

interface SpecificationDraft {
  id: string
  version: string
  status: SpecificationStatus
  title: string
  subject: string
  learningObjective: string
  questionRules: { totalQuestions: number; questionType: string; optionsPerQuestion: number }
  difficultyDistribution: { easy: number; medium: number; hard: number }
  scoringRules: { pointsPerQuestion: number; totalPoints: number }
  approval: { required: boolean; approvedBy?: string; approvedAt?: string }
  createdAt: string
  updatedAt: string
}

interface GeneratedAssessment {
  id: string
  specificationId: string
  specificationVersion: string
  status: string
  questions: GeneratedQuestion[]
  createdBy: string
  createdAt: string
}
```

## 9. Accessibility and UX requirements

- Every input has a visible label and an error association.
- All actions are keyboard accessible.
- Do not communicate status by color alone; use text labels such as `Draft`, `Approved` and `Needs correction`.
- Maintain visible focus states.
- Use `aria-live="polite"` for save, approval and generation feedback.
- Confirm destructive or irreversible actions such as approval.
- Use responsive layouts for laptop and tablet widths; tables may scroll horizontally on small screens.

## 10. Acceptance criteria

- A reviewer can create a valid draft through the UI and see the `201` response.
- Invalid difficulty totals and scoring totals are blocked with understandable messages.
- A reviewer can load a specific ID/version and see all fields from `SpecificationDraft`.
- A reviewer can approve a draft and the UI reflects `status: approved`.
- Generate is disabled until the specification is approved.
- A reviewer can generate an assessment and see every returned question and option.
- `400`, `404`, `409` and `502` responses are rendered as useful UI states.
- No AI or backend validation logic is duplicated as the source of truth in the browser.
- The UI works against the API base URL supplied by configuration.

## 11. Known backend gaps for future UI work

The architecture document describes review findings, assessment approval/rejection and audit events, but the current backend controllers expose only specification create/get/update/approve and assessment generate. The UI should keep placeholders for these features, but they should be connected only after APIs are added for:

- Retrieve generated assessment by ID.
- Run independent Review Agent and retrieve findings.
- Approve or reject an assessment.
- Retrieve audit events.

