# Intelligent Employee Onboarding Integration Simulator

A portfolio project that walks through what a real employee onboarding integration actually looks like once you get past the "call an API and hope it works" stage — validation, duplicate prevention, a downstream HCM call, safe retries, an ops dashboard to keep an eye on things, and a local AI layer that explains failures without ever seeing sensitive data.

## Why I built this

Most onboarding-integration demos stop at "submit a form, hit an API." That's not really how these systems behave in production. Real HR integrations have to deal with duplicate submissions, flaky downstream services, partial failures, retry limits, and support engineers who need to understand *why* something failed at 2am without digging through raw logs.

So I built a small end-to-end simulation of that world — no real company systems, no real employee data, just a working model of the plumbing: an HR-style request comes in, gets validated and stored, gets pushed to a mock HCM system, and if something goes wrong, the app classifies the failure, decides whether it's worth retrying, and generates a grounded explanation an operator can actually use.

It's meant to show:

- how I think about API integration design
- how I handle failure classification and controlled retries
- how I approach operational monitoring and support tooling
- how I use local generative AI responsibly, without leaking sensitive fields
- general secure engineering habits (rate limiting, correlation IDs, sanitized logs, etc.)

## How it fits together

mermaid :

flowchart LR
    HR[HR / Operations User] --> UI[Next.js Operations Dashboard]
    UI --> API[ASP.NET Core Onboarding API]
    API --> DB[(SQLite Database)]
    API --> HCM[Mock HCM API]
    API --> AI[Local Ollama / Phi-4 Mini]
    API --> LOGS[Structured Serilog Logs]

    HCM --> API
    AI --> API
    API --> UI


## What happens when you submit a request

mermaid :

flowchart TD
    A[Enter employee details] --> B[POST /api/onboarding]
    B --> C{Validation and duplicate check}
    C -->|Invalid or duplicate| D[Return safe error response]
    C -->|Valid| E[Create Pending transaction]
    E --> F{Operator action}
    F -->|Create only| G[Display Pending transaction]
    F -->|Create and process| H[Atomically claim transaction]
    G --> H
    H --> I[Set status to Processing]
    I --> J[Send employee to Mock HCM]
    J --> K{Downstream result}
    K -->|Success| L[Completed with HCM employee ID]
    K -->|Validation failure| M[Non-retryable failure]
    K -->|Temporary failure or timeout| N[Retryable failure]
    N --> O[Controlled retry within attempt limit]
    M --> P[Grounded AI explanation]
    N --> P


## What it does

Onboarding
You can create employee onboarding requests straight from the dashboard, either as "create only" (leave it pending) or "create and process" (send it downstream right away). Requests are validated server-side, employee numbers and emails get normalized, duplicate employee numbers are rejected, and everything lands in SQLite as a transaction.

Downstream processing
The mock HCM layer simulates the kind of responses you'd actually see from a real system — successful creation with an employee ID, validation failures, duplicates, temporary outages, timeouts, connection failures, 500s, and malformed responses. The point isn't to be exhaustive, it's to be realistic enough that the retry logic actually gets exercised.

Retries and concurrency
Only failures that are actually retryable get retried, and never more than three times. Completed transactions and validation failures are locked out of the retry path entirely. Claiming a transaction for processing is done as an atomic, conditional database update, so two operators (or two requests) can't accidentally process the same transaction twice.

AI, kept on a short leash
There's a local Ollama integration (phi4-mini) that generates plain-language guidance when something fails. It only ever sees sanitized operational fields — never the employee's name, number, email, department, country, or joining date, and never the full payload. The actual facts and retry decisions come from an approved guidance catalogue, not the model; the AI's job is just to phrase things clearly. Its output gets checked for required structure and disallowed wording, and if it fails that check, times out, or Ollama isn't running, the app falls back to deterministic guidance instead of guessing.

Security basics that are easy to skip and shouldn't be
Correlation IDs are validated and propagated through requests, logs are structured with Serilog and deliberately scrubbed of sensitive fields, errors come back as RFC-style ProblemDetails, CORS is locked down to the local dashboard origin, endpoints are rate-limited, request bodies are capped at 64 KB, and there are separate liveness/readiness health checks. Local database files, logs, build output, and env files are all gitignored. Last NuGet scan came back clean — no known vulnerable packages.

The dashboard itself
A health indicator, summary cards, employee search, status filters, pagination, a details drawer per transaction, buttons to process or retry, AI-generated failure explanations, and a form for creating new onboarding requests. Built to look and feel like something you'd actually use internally, not a bare CRUD demo.

## Stack

Backend — .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite, NUnit, Serilog, OllamaSharp

Frontend — Next.js 16, React 19, TypeScript, Tailwind, ESLint

Local AI — Ollama running Microsoft Phi-4 Mini

## Layout


intelligent-onboarding-integration/
├── dashboard/                         # Next.js operations dashboard
├── src/
│   ├── MockHcm.Api/                   # Simulated downstream HCM API
│   └── Onboarding.Api/                # Main onboarding integration API
├── tests/
│   └── Onboarding.Api.Tests/          # NUnit tests
├── IntelligentOnboardingIntegration.slnx
└── README.md


## API reference

### Onboarding transactions

| Method | Endpoint | Purpose |
|---|---|---|
| POST | /api/onboarding | Create a pending onboarding transaction |
| GET | /api/onboarding | Get filtered and paginated transactions |
| GET | /api/onboarding/summary | Get dashboard summary counts |
| GET | /api/onboarding/{transactionId} | Get one transaction |
| POST | /api/onboarding/{transactionId}/process | Process a pending transaction |
| POST | /api/onboarding/{transactionId}/retry | Retry an eligible failed transaction |
| POST | /api/onboarding/{transactionId}/explain | Generate grounded failure guidance |

### Health

| Method | Endpoint | Purpose |
|---|---|---|
| GET | /health/live | Confirm the API process is running |
| GET | /health/ready | Check SQLite, Mock HCM, and Ollama readiness |

### Filtering examples


GET /api/onboarding?pageNumber=1&pageSize=10
GET /api/onboarding?status=Completed&pageNumber=1&pageSize=10
GET /api/onboarding?search=EMP-7001&pageNumber=1&pageSize=10


## Running it locally

You'll need: .NET 10 SDK, Node.js 20.9+, npm, Ollama, and Git.

1. Clone it

git clone https://github.com/Chintu-2909/intelligent-onboarding-integration.git
cd intelligent-onboarding-integration


2. Build the backend

dotnet restore IntelligentOnboardingIntegration.slnx
dotnet build IntelligentOnboardingIntegration.slnx


3. Pull the model

ollama pull phi4-mini
ollama list

Ollama should be reachable at http://localhost:11434.

4. Start Mock HCM

dotnet run --project src/MockHcm.Api

Runs on http://localhost:5103 by default.

5. Start the Onboarding API (in a new terminal)

dotnet run --project src/Onboarding.Api

Runs on http://localhost:5083 by default.

6. Set up the dashboard

cd dashboard
cat > .env.local <<'EOF'
NEXT_PUBLIC_ONBOARDING_API_URL=http://localhost:5083
EOF
npm install

.env.local is gitignored on purpose — don't commit it.

7. Run the dashboard

npm run dev

Then open http://localhost:3000.

## Testing

Run the backend suite:

dotnet test IntelligentOnboardingIntegration.slnx


Latest run: 26 total, 26 passed, 0 failed, 0 skipped.

Frontend checks:

cd dashboard
npm run lint
npm run build


Dependency scan:

dotnet list IntelligentOnboardingIntegration.slnx package --vulnerable --include-transitive


## Example request

json
{
  "employeeNumber": "EMP-9001",
  "firstName": "Priya",
  "lastName": "Sharma",
  "email": "priya.sharma@example.com",
  "department": "Engineering",
  "country": "India",
  "joiningDate": "2026-08-17"
}


## Transaction statuses

| Status | Meaning |
|---|---|
| Pending | Request was created but not processed |
| Processing | The transaction was atomically claimed for processing |
| Completed | Employee creation succeeded in Mock HCM |
| ValidationFailed | Downstream validation rejected the request |
| Duplicate | A downstream duplicate was reported |
| TemporaryFailure | A temporary downstream or connection failure occurred |
| TimedOut | The downstream request exceeded the configured timeout |
| Failed | A non-specific or unexpected failure occurred |
| RetryLimitExceeded | Three processing attempts were reached |

## What's deliberately kept out of the logs

No email addresses, no full payloads, no first/last names, no department, no country, no joining date.

What *does* show up in logs: transaction ID, employee number, status, retry count, error code, the HCM employee ID once creation succeeds, and the correlation ID.

Everything in this project is synthetic. Please don't put real employee or company data into it.

## Known limitations

- Mock HCM is exactly that — a simulator, not a real HCM connection
- SQLite is fine for local demo purposes, not for production scale
- There's no authentication or authorization layer yet
- Local HTTP is used for development convenience, not meant for production
- CORS is locked to the local dashboard origin only
- Ollama and Phi-4 Mini need to be installed locally to use the AI features
- The dashboard has some transitive npm findings in Next.js dependencies where npm audit fix --force would force an unsafe breaking downgrade — don't run it
- Deploying this publicly would need a fresh dependency review and real production hardening first

## If this were going to production

A few things I'd add before this ever touched real data:

- Microsoft Entra ID for authentication and authorization
- Azure API Management in front of the API
- Azure SQL Database instead of SQLite
- Managed Identity and Azure Key Vault for secrets
- Private networking and HTTPS-only everywhere
- Application Insights for centralized monitoring
- A real HCM integration (Oracle HCM, Workday, or SAP SuccessFactors)
- CI/CD pipelines with security gates
- Proper data-retention and audit policies

## How the AI piece actually works

The model is intentionally kept advisory, not authoritative:

1. The app looks up approved guidance for whatever error code was recorded.
2. Only sanitized technical fields get sent to Ollama — nothing about the employee.
3. The model can rephrase for readability but has no say in retry logic.
4. Its output is checked for required headings and disallowed language before it's shown.
5. If that check fails, or Ollama's unreachable, the app falls back to deterministic guidance.
6. Operators are reminded that AI-generated guidance should be verified, not taken as gospel.

## Screenshots

### Operations Dashboard

docs/images/dashboard-overview.png

### Employee Onboarding Form

docs/images/onboarding-form.png

### Grounded AI Failure Guidance

docs/images/ai-explanation.png

## Author

Chintu-2909
Software engineer focused on .NET, Azure integration services, APIs, cloud architecture, and enterprise integrations.

## Disclaimer

This is a learning and portfolio project. It runs on simulated services and synthetic data, and isn't intended to handle real employee information without additional security, privacy, identity, compliance, and operational work first.
