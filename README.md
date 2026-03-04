# CompanyFlow ERP — Enterprise Management Platform

[![CI Tests & Coverage](https://github.com/Som3a99/company-management-system/actions/workflows/ci-tests-coverage.yml/badge.svg)](https://github.com/Som3a99/company-management-system/actions/workflows/ci-tests-coverage.yml)
[![Security Scans](https://github.com/Som3a99/company-management-system/actions/workflows/security-scans.yml/badge.svg)](https://github.com/Som3a99/company-management-system/actions/workflows/security-scans.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## Table of Contents

- [Project Overview](#project-overview)
- [System Features](#system-features)
- [Architecture Overview](#architecture-overview)
- [Technology Stack](#technology-stack)
- [Role-Based Access Model](#role-based-access-model)
- [Project Structure](#project-structure)
- [Key Components](#key-components)
- [Database Design](#database-design)
- [Installation & Setup](#installation--setup)
- [Running the Project](#running-the-project)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Security Considerations](#security-considerations)
- [Future Improvements](#future-improvements)
- [Screenshots](#screenshots)
- [Developer Information](#developer-information)

---

## Project Overview

**CompanyFlow ERP** is a full-featured, production-grade ASP.NET Core MVC web application that provides centralized management of employees, departments, projects, and tasks within an organization. It is designed as a modular, multi-company-ready enterprise resource planning (ERP) platform with role-based access control, real-time notifications, AI-powered analytics, and a comprehensive reporting engine.

### What It Does

- Centralizes employee records, organizational structure, and project portfolios into a single source of truth.
- Tracks projects and tasks through their full lifecycle with Kanban-style task boards.
- Provides role-specific dashboards with actionable insights, overdue alerts, and AI-generated summaries.
- Automates background processes such as deadline notifications, anomaly detection, and report generation.

### The Problem It Solves

Organizations struggle with fragmented tools for HR, project tracking, and reporting. CompanyFlow unifies these workflows behind a single interface with strict access control, audit trails, and intelligent analytics — reducing context-switching and improving operational visibility.

### Who Uses It

| Role | Usage |
|---|---|
| **CEO** | Organization-wide dashboards, AI executive digests, anomaly detection, full administrative control |
| **IT Admin** | User account management, password reset approval, system health monitoring, security audit |
| **Department Manager** | Team management, HR operations, department-scoped reports and task oversight |
| **Project Manager** | Project lifecycle management, team assignment, task boards, workload analysis |
| **Employee** | Personal task dashboard, task status updates, project participation, notification preferences |

---

## System Features

### Organization Management

- **Department Management** — CRUD operations with unique department codes (`ABC_123` format), manager assignment, soft-delete support, and paginated search.
- **Employee Management** — Full lifecycle management with image upload, salary validation (configurable range), phone sanitization, gender-based default avatars, and file signature validation for uploads.
- **User Account Management** — Account creation with cryptographically secure 16-character passwords, role assignment, activation/deactivation, and protected CEO role operations.

### Project & Task Management

- **Project Management** — Full CRUD with project code format validation (`PRJ-YYYY-XXX`), team member assignment via junction table, budget tracking, and project status lifecycle (Planning → In Progress → On Hold → Completed → Cancelled).
- **Task Board** — Kanban-style task management with drag-and-drop status updates, priority levels (Low → Critical), task assignment, comments with @mentions, and AI-powered description generation.
- **Task API** — RESTful API endpoints (`/api/tasks`) with optimistic concurrency via `RowVersion`, separate from the MVC task board for integration scenarios.

### Dashboards & Intelligence

- **CEO Executive Dashboard** — Organization-wide KPIs, overdue trend analysis, AI intelligence data, team health scores, anomaly alerts, high-risk tasks, and department performance metrics.
- **IT Admin Dashboard** — System health monitoring, security metrics (failed logins, locked accounts, role changes), infrastructure stats (report jobs, cache health), identity/access overview, and audit log viewer.
- **Manager Dashboard** — Team member list with workload labels, task metrics, overdue/high-risk tasks, project progress bars, and department performance details.
- **Employee Dashboard** — Personal assigned tasks, overdue/due-soon alerts, project summaries, and notification feed.

### Reporting & Analytics

- **Multi-Type Reports** — Task, Project, Department, Audit, and Estimation Accuracy reports.
- **Multi-Format Export** — CSV, PDF, Excel (`.xlsx`), and HTML export.
- **Background Report Jobs** — Queued report generation with status tracking, completion notifications, and downloadable output files.
- **Report Presets** — Save and reuse filter configurations.
- **Scope-Based Access** — CEO sees all data; Department Managers are scoped to their department; Project Managers to their project.
- **Rate Limiting** — Token bucket rate limiter (30 requests/minute) on reporting endpoints to prevent abuse.

### AI-Powered Features

- **AI Executive Summary** — Natural language report summaries generated via Google Gemini 2.0 Flash API with template-based fallback.
- **AI Task Description Generation** — Automatically generates structured task descriptions (Goal, Steps, Definition of Done).
- **Task Risk Scoring** — Real-time risk score calculation (0–100) based on due date proximity, priority, status, and assignment.
- **Workload Analysis** — Employee workload distribution analysis for balanced task assignment.
- **Task Assignment Suggestions** — Intelligent task assignment recommendations based on current workload and capacity.
- **Project Forecasting** — Completion date forecasting based on team velocity and remaining work.
- **Team Health Scoring** — Composite team health score with risk factor identification.
- **Audit Anomaly Detection** — Automated detection of suspicious patterns in audit logs (brute-force attempts, unusual activity).
- **Executive Weekly Digest** — Aggregated intelligence data for CEO decision-making.

### Notifications & Communication

- **Real-Time Notifications** — SignalR-powered push notifications with `ReceiveNotification` and `UpdateUnreadCount` events.
- **Notification Center** — Paginated, filterable by severity (Info / Warning / Critical) and type (12 notification types).
- **Per-User Preferences** — Granular control over in-app and email notifications with mute options per category.
- **Email Notifications** — SMTP-based email delivery for critical events.

### Background Processing

| Job | Schedule | Purpose |
|---|---|---|
| `ReportJobWorkerService` | 0.5–2s polling | Processes queued report generation jobs |
| `TaskDeadlineNotificationJob` | Every 6 hours | Sends due-soon (48h) and overdue notifications |
| `AnomalyDetectionNotificationJob` | Every 2 hours | Runs audit anomaly scanner, alerts CEOs |
| `NotificationArchiveJob` | Daily | Archives read notifications older than 90 days |
| `NotificationHardDeleteJob` | Daily | Permanently deletes archived notifications older than 365 days |
| `CacheWarmupHostedService` | On startup | Pre-populates frequently accessed cache entries |
| `CacheTelemetryHostedService` | Every 5 minutes | Logs cache hit/miss statistics |

### Security & Audit

- **Comprehensive Audit Logging** — Tracks user actions, resource changes, IP addresses, user agents, and success/failure status.
- **Password Reset Workflow** — Ticket-based password reset with IT Admin approval/denial flow.
- **Account Lockout** — 5 failed attempts trigger 15-minute lockout.
- **IT Admin Security Monitoring** — Dashboard panels for failed logins, locked accounts, role changes, and anomaly detection.

---

## Architecture Overview

CompanyFlow follows a **three-layer architecture** built on the ASP.NET Core MVC pattern:

```
┌──────────────────────────────────────────────────────────┐
│                   ERP.PL (Presentation)                  │
│  Controllers · Views · ViewModels · Middleware · Hubs    │
│  Security · Background Jobs · Helpers · AutoMapper       │
├──────────────────────────────────────────────────────────┤
│                ERP.BLL (Business Logic)                  │
│  Services · Repositories · DTOs · Interfaces             │
│  Reporting Module · AI / Analytics Services              │
├──────────────────────────────────────────────────────────┤
│                 ERP.DAL (Data Access)                    │
│  DbContext · Entity Models · EF Configurations           │
│  Migrations · Identity Integration                       │
└──────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Project | Responsibility |
|---|---|---|
| **Presentation** | `ERP.PL` | HTTP handling, Razor views, authentication/authorization, SignalR hubs, background hosted services, input validation, middleware pipeline |
| **Business Logic** | `ERP.BLL` | Domain services, repository implementations, Unit of Work coordination, reporting engine, AI service integrations, caching strategy |
| **Data Access** | `ERP.DAL` | Entity definitions, EF Core DbContext, Fluent API configurations, database migrations, Identity schema |

### Design Patterns

| Pattern | Implementation |
|---|---|
| **Repository + Unit of Work** | Generic repository base with specialized repositories (`EmployeeRepository`, `DepartmentRepository`, etc.) coordinated through `UnitOfWork` |
| **Service Layer** | Business logic encapsulated in injectable services with interface-based contracts |
| **Dependency Injection** | All services registered in `Program.cs` with appropriate lifetimes (Scoped, Singleton, Transient) |
| **Custom Claims Factory** | `ApplicationUserClaimsPrincipalFactory` injects domain-specific claims (`EmployeeId`, `ManagedDepartmentId`, etc.) at login |
| **Global Query Filters** | Soft-delete entities automatically filtered via EF Core query filters |
| **Optimistic Concurrency** | `RowVersion` columns on `Employee` and `TaskItem` entities |
| **Observer Pattern** | SignalR hub decoupled via `INotificationHubService` → `SignalRNotificationHubService` |
| **Background Service Pattern** | Seven `IHostedService` implementations for periodic and continuous processing |

### How Layers Interact

1. **HTTP Request** → Controller receives the request and validates input via `InputSanitizer`.
2. **Controller** → Calls services from `ERP.BLL` via injected interfaces.
3. **Service** → Coordinates business logic, uses repositories for data access, and interacts with external APIs (Gemini AI).
4. **Repository** → Queries `ApplicationDbContext` (EF Core) with LINQ, respecting global query filters.
5. **Response** → Controller maps entities to ViewModels via AutoMapper and renders Razor views.
6. **Background** → Hosted services independently poll for work or run on schedules, sharing the same service layer.
7. **Real-Time** → `INotificationHubService` pushes events to connected SignalR clients.

---

## Technology Stack

| Category | Technology | Purpose |
|---|---|---|
| **Runtime** | .NET 8.0 | Long-Term Support runtime |
| **Web Framework** | ASP.NET Core MVC | Server-side web application with Razor views |
| **ORM** | Entity Framework Core 8.0.24 | Database access with migrations, query filters, and Fluent API |
| **Database** | SQL Server (LocalDB for dev) | Relational data storage |
| **Identity** | ASP.NET Core Identity | Authentication, authorization, role/claim management |
| **Real-Time** | SignalR | WebSocket-based push notifications |
| **Object Mapping** | AutoMapper 16.0 | Entity ↔ ViewModel mapping |
| **AI Integration** | Google Gemini 2.0 Flash API | Narrative generation, task descriptions, executive summaries |
| **Email** | SMTP (System.Net.Mail) | Transactional email delivery |
| **Caching** | `IMemoryCache` | Size-limited in-memory cache with telemetry |
| **Compression** | Gzip Response Compression | HTTP response compression |
| **Rate Limiting** | Token Bucket (ASP.NET Core) | Request throttling on heavy endpoints |
| **Data Protection** | ASP.NET Core Data Protection | Cookie encryption, anti-forgery tokens |
| **Testing** | xUnit + Moq + FluentAssertions | Unit and integration testing |
| **Integration Testing** | `WebApplicationFactory` + EF InMemory | Full pipeline integration tests |
| **Load Testing** | k6 | Performance smoke tests |
| **Code Coverage** | Coverlet | XPlat code coverage collection |
| **CI/CD** | GitHub Actions | Automated testing and security scanning |
| **Frontend** | Razor Views + Bootstrap + CSS | Server-rendered UI |

---

## Role-Based Access Model

CompanyFlow implements a hybrid **role-based + claim-based** authorization model with five system roles:

### Roles & Permissions

| Role | Access Scope | Key Capabilities |
|---|---|---|
| **CEO** | Organization-wide | Full administrative control, executive dashboards, AI digests, anomaly detection, all reports, project CRUD, user management |
| **ITAdmin** | System administration | User account lifecycle, password reset approvals, system health monitoring, cache management, security audit logs |
| **DepartmentManager** | Own department | Employee HR operations (assign/remove), department-scoped reports, team workload view, task oversight |
| **ProjectManager** | Own project(s) | Project editing, task board management (CRUD), team assignment, project-scoped reports, workload analysis |
| **Employee** | Personal scope | View assigned tasks, update task status, post comments, manage notification preferences, view project summaries |

### Authorization Policies

| Policy | Requirement |
|---|---|
| `RequireCEO` | Role: CEO |
| `RequireITAdmin` | Role: CEO or ITAdmin |
| `RequireManager` | Role: CEO, DepartmentManager, or ProjectManager |
| `RequireEmployee` | Claim: `EmployeeId` exists |
| `RequireDepartmentManager` | Claim: `ManagedDepartmentId` exists |
| `RequireProjectManager` | Claim: `ManagedProjectId` exists |

### Custom Claims

At login, `ApplicationUserClaimsPrincipalFactory` injects the following claims into the user's identity (cached for 5 minutes):

- `EmployeeId` — Links the user to their employee record
- `DepartmentId` — The user's department
- `ManagedDepartmentId` — Department managed by this user (for department managers)
- `ManagedProjectId` — Project managed by this user (for project managers)
- `AssignedProjectId` — Project the user is assigned to

---

## Project Structure

```
CompanyManagementSystem/
│
├── CompanyManagementSystem.sln          # Solution file
├── .github/workflows/                   # CI/CD pipeline definitions
│   ├── ci-tests-coverage.yml            # Test runner with code coverage
│   └── security-scans.yml              # Dependency review + vulnerability scanning
│
├── ERP.DAL/                             # Data Access Layer
│   ├── Models/                          # Entity classes (Department, Employee, Project, TaskItem, etc.)
│   └── Data/
│       ├── Contexts/                    # ApplicationDbContext (IdentityDbContext)
│       ├── Configurations/              # EF Core Fluent API entity configurations
│       └── Migrations/                  # EF Core database migrations
│
├── ERP.BLL/                             # Business Logic Layer
│   ├── Common/                          # Shared types (PagedResult, TaskContracts, CacheKeys)
│   ├── DTOs/                            # Data transfer objects for analytics & AI
│   ├── Interfaces/                      # Service and repository contracts
│   ├── Repositories/                    # Repository + Unit of Work implementations
│   ├── Services/                        # Core business services (Task, Risk, AI, Cache, etc.)
│   └── Reporting/
│       ├── Interfaces/                  # IReportingService, IReportExportService, IReportJobService
│       ├── Services/                    # Report generation, export, and job processing
│       └── Dtos/                        # Report-specific data transfer objects
│
├── ERP.PL/                              # Presentation Layer
│   ├── Program.cs                       # Application entry point and DI configuration
│   ├── Controllers/                     # 17 MVC + API controllers
│   ├── Views/                           # Razor views organized by feature area
│   │   ├── Home/                        # Landing page and dashboard routing
│   │   ├── Account/                     # Login, profile, password management
│   │   ├── Department/                  # Department CRUD views
│   │   ├── Employee/                    # Employee CRUD views
│   │   ├── Project/                     # Project management views
│   │   ├── TaskBoard/                   # Kanban task board views
│   │   ├── Reporting/                   # Report views (Task, Project, Audit, Anomaly, Digest)
│   │   ├── ExecutiveHome/               # CEO dashboard
│   │   ├── ManagerHome/                 # Manager dashboard
│   │   ├── EmployeeHome/               # Employee dashboard
│   │   ├── ITAdmin/                     # IT admin portal
│   │   ├── ITAdminHome/                 # IT admin dashboard
│   │   ├── UserManagement/              # User account management views
│   │   ├── Notifications/              # Notification center
│   │   ├── DepartmentManagerHr/         # HR operations views
│   │   └── Shared/                      # Layout, partial views, error pages
│   ├── ViewModels/                      # View models grouped by feature area
│   ├── Services/                        # PL-level services (Audit, RoleManagement, SMTP, Background Jobs)
│   ├── Security/                        # Custom claims principal factory
│   ├── Middleware/                      # Global exception handling middleware
│   ├── Hubs/                            # SignalR notification hub
│   ├── Helpers/                         # Document settings, input sanitizer
│   ├── Mapping/                         # AutoMapper profiles (Department, Employee, Project)
│   ├── Extensions/                      # Pagination extensions
│   ├── Data/                            # Database seeder (SystemDataSeeder)
│   ├── Utilities/                       # General utility classes
│   └── wwwroot/                         # Static files (CSS, JS, images, generated reports)
│
└── Tests/                               # Test project
    ├── Infrastructure/                  # Test factories, auth handlers, DB helpers
    ├── Services/                        # 15 service test suites
    ├── Repositories/                    # 3 repository test suites
    ├── Controllers/                     # Integration + rate limiting tests
    ├── Security/                        # Authorization policy + security tests
    ├── Caching/                         # Cache service tests
    └── load-tests/                      # k6 performance smoke tests
```

---

## Key Components

### Task Management System

The task system is built around the `TaskItem` entity and spans multiple controllers. The `TaskBoardController` provides a Kanban-style UI for creating, editing, and transitioning tasks through statuses (New → In Progress → Blocked → Completed → Cancelled). The `TasksController` exposes RESTful API endpoints with optimistic concurrency control via `RowVersion`. Tasks support priority levels, due dates, estimated/actual hours, comments with @mentions, and AI-generated descriptions. The `TaskRiskService` calculates a real-time risk score (0–100) for each task based on multiple factors.

### Reporting Engine

The reporting subsystem in `ERP.BLL/Reporting/` provides a complete pipeline: `IReportingService` generates report data (filtered by scope), `IReportExportService` formats output into CSV/PDF/Excel/HTML, and `IReportJobService` manages background report generation with queued jobs. The `ReportJobWorkerService` polls for pending jobs every 0.5–2 seconds. Users can save filter presets and receive notifications when background reports complete.

### Role-Specific Dashboards

Each role has a dedicated dashboard controller and view:
- **CEO** (`ExecutiveHomeController`) — KPIs across the entire organization, AI intelligence data, team health scores, anomaly alerts, and weekly digest.
- **IT Admin** (`ITAdminHomeController`) — System health, security metrics, infrastructure stats, identity/access overview.
- **Manager** (`ManagerHomeController`) — Team members with workload labels, task metrics, project progress.
- **Employee** (`EmployeeHomeController`) — Personal tasks, overdue alerts, project summaries.

The `HomeController` automatically redirects authenticated users to their role-appropriate dashboard.

### Background Workers

Seven hosted services run independently:
- **Report generation** — Processes queued export jobs.
- **Deadline notifications** — Detects tasks due within 48 hours or overdue, notifies assignees and managers.
- **Anomaly detection** — Scans audit logs for suspicious patterns and alerts CEO users.
- **Notification lifecycle** — Archives old read notifications and permanently deletes aged archived records.
- **Cache management** — Warms up cache on startup and tracks hit/miss telemetry.

### AI Analytics Suite

The AI layer integrates with Google Gemini 2.0 Flash for natural language generation and implements local analytics algorithms:
- `AiNarrativeService` — Generates executive summaries from report data.
- `TaskDescriptionService` — Produces structured task descriptions from titles and context.
- `DashboardIntelligenceService` — Aggregates intelligence metrics (overloaded employees, behind-schedule projects).
- `ProjectForecastService` — Estimates project completion dates based on team velocity.
- `TeamHealthService` — Calculates composite team health scores.
- `AuditAnomalyService` — Detects brute-force attempts and unusual activity patterns.

All AI services include template-based fallbacks when the external API is unavailable.

### Notification System

Notifications flow through `INotificationService` → database persistence → `INotificationHubService` → SignalR push to connected clients. The `NotificationHub` maps authenticated users by ID and pushes `ReceiveNotification` events. Users configure per-category preferences (mute task assignments, status changes, report notifications) through the `NotificationsController`.

---

## Database Design

### Entity Relationship Overview

```
┌──────────────┐       ┌──────────────┐
│  Department   │◄──────│   Employee   │
│              │  1:N   │              │
│  Manager ────┼───────►│  (Manager)   │
└──────┬───────┘        └──────┬───────┘
       │ 1:N                   │
       ▼                       │ M:N
┌──────────────┐        ┌─────┴────────┐
│   Project    │◄───────│ProjectEmployee│
│              │  1:N   │  (Junction)   │
│  PM ─────────┼───────►│              │
└──────┬───────┘        └──────────────┘
       │ 1:N
       ▼
┌──────────────┐        ┌──────────────┐
│   TaskItem   │◄───────│ TaskComment  │
│              │  1:N   │              │
│  AssignedTo ─┼───────►│  Author ─────┼──► ApplicationUser
└──────────────┘        └──────────────┘

┌──────────────┐        ┌──────────────────┐
│ApplicationUser│───────│ NotificationPref │
│  (Identity)  │  1:1   │                  │
│  Employee? ──┼───────►└──────────────────┘
└──────┬───────┘
       │ 1:N
       ▼
┌──────────────┐  ┌──────────────┐  ┌───────────────────┐
│AppNotification│  │  AuditLog    │  │PasswordResetRequest│
└──────────────┘  └──────────────┘  └───────────────────┘
```

### Core Entities

| Entity | Key Fields | Notes |
|---|---|---|
| **Department** | `DepartmentCode`, `DepartmentName`, `ManagerId`, `IsDeleted` | Soft-delete; unique code format |
| **Employee** | `FirstName`, `LastName`, `Email`, `Salary`, `Position`, `Gender`, `ImageUrl`, `RowVersion` | Soft-delete; 1:1 with `ApplicationUser` |
| **Project** | `ProjectCode`, `ProjectName`, `Budget`, `Status`, `StartDate`, `EndDate` | Belongs to department; has a project manager |
| **TaskItem** | `Title`, `Priority`, `Status`, `DueDate`, `EstimatedHours`, `ActualHours`, `RowVersion` | Belongs to project; assigned to employee |
| **TaskComment** | `Content`, `CreatedAt` | Belongs to task; authored by user |
| **ProjectEmployee** | `AssignedAt`, `AssignedBy` | Junction table for many-to-many project ↔ employee |
| **ApplicationUser** | Extends `IdentityUser` with `EmployeeId`, `IsActive`, `RequirePasswordChange` | ASP.NET Core Identity integration |
| **AppNotification** | `Title`, `Message`, `Type`, `Severity`, `IsRead`, `IsArchived` | Per-user notification with 12 types |
| **AuditLog** | `Action`, `ResourceType`, `ResourceId`, `IpAddress`, `UserAgent`, `Succeeded` | Full audit trail |
| **ReportJob** | `ReportType`, `Format`, `Status`, `OutputPath`, `FiltersJson` | Background report generation queue |
| **PasswordResetRequest** | `TicketNumber`, `Status`, `ExpiresAt`, `DenialReason` | IT-admin-approved reset workflow |

### Key Relationships

- **Department → Employee**: One-to-many; each employee belongs to one department.
- **Department → Manager**: One-to-one; a department has one manager (who is an employee).
- **Project → Department**: Many-to-one; projects belong to departments.
- **Project ↔ Employee**: Many-to-many via `ProjectEmployee` junction table.
- **Project → TaskItem**: One-to-many; tasks belong to a project.
- **TaskItem → Employee**: Many-to-one; each task is assigned to one employee.
- **ApplicationUser → Employee**: One-to-one optional; not all employees have user accounts.

---

## Installation & Setup

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB, Express, or full edition)
- [Git](https://git-scm.com/)
- (Optional) [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

### Step-by-Step Setup

#### 1. Clone the Repository

```bash
git clone https://github.com/Som3a99/company-management-system.git
cd company-management-system
```

#### 2. Restore Dependencies

```bash
dotnet restore CompanyManagementSystem.sln
```

#### 3. Configure the Database

Update the connection string in `ERP.PL/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ERPDB_Dev;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

#### 4. Configure AI Service (Optional)

To enable AI-powered features, add your Google Gemini API key in `ERP.PL/appsettings.json`:

```json
{
  "AiService": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "ApiUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "Model": "gemini-2.0-flash"
  }
}
```

> AI features gracefully fall back to template-based responses when the API key is not configured or the service is unavailable.

#### 5. Apply Database Migrations

**Option A** — Automatic on startup (recommended for development):

Set in `appsettings.Development.json`:
```json
{
  "Database": {
    "ApplyMigrationsOnStartup": true
  }
}
```

**Option B** — Manual migration:

```bash
cd ERP.PL
dotnet ef database update --project ../ERP.DAL
```

#### 6. Seed Initial Data

Configure seed mode in `appsettings.Development.json`:

```json
{
  "Seed": {
    "Mode": "Development",
    "ResetDatabase": false
  }
}
```

| Mode | Behavior |
|---|---|
| `None` | No seeding |
| `Development` | Seeds sample departments, employees, projects, tasks, and user accounts |
| `Production` | Seeds only the initial admin account |

#### 7. Configure Email (Optional)

For email notification support, configure SMTP settings. [Mailtrap](https://mailtrap.io/) is recommended for development:

```json
{
  "Email": {
    "SmtpHost": "sandbox.smtp.mailtrap.io",
    "SmtpPort": 587,
    "SmtpUsername": "your_username",
    "SmtpPassword": "your_password",
    "FromEmail": "noreply@companyflow.local",
    "FromName": "CompanyFlow ERP",
    "EnableSsl": true
  }
}
```

#### 8. Run the Application

```bash
cd ERP.PL
dotnet run
```

The application will be available at `https://localhost:5001` (or the port configured by your environment).

---

## Running the Project

### Visual Studio 2022

1. Open `CompanyManagementSystem.sln`.
2. Set `ERP.PL` as the startup project.
3. Press **F5** or click **Start Debugging**.

### VS Code

1. Open the root folder in VS Code.
2. Install the **C# Dev Kit** extension.
3. Open the terminal and run:

```bash
cd ERP.PL
dotnet run
```

4. Or use the built-in **Run and Debug** panel with the generated `launch.json`.

### dotnet CLI

```bash
# Development mode (with hot reload)
cd ERP.PL
dotnet watch run

# Production mode
dotnet run --configuration Release --launch-profile "Production"
```

### Health Check

Once running, verify the application is healthy:

```bash
# Application health
curl https://localhost:5001/health

# Cache health
curl https://localhost:5001/health/cache
```

---

## Testing

### Test Strategy

The project employs a multi-layered testing strategy:

| Layer | Type | What It Tests |
|---|---|---|
| **Unit Tests** | Service tests (15 suites) | Business logic, AI services, risk scoring, caching, reporting |
| **Repository Tests** | Data access tests (3 suites) | EF Core queries, CRUD operations, soft-delete filters |
| **Integration Tests** | Controller tests (2 suites) | Full HTTP pipeline, endpoint authorization, rate limiting |
| **Security Tests** | Auth policy tests (2 suites) | Role-based access enforcement, security header validation |
| **Load Tests** | k6 smoke tests | Performance thresholds (p95 < 800ms at 80 VUs) |

### Test Frameworks

- **xUnit** — Test runner and assertions
- **Moq** — Mock object framework for service dependencies
- **FluentAssertions** — Expressive assertion syntax
- **EF Core InMemory** — Isolated database per test
- **WebApplicationFactory** — Full ASP.NET Core integration test host
- **Coverlet** — Cross-platform code coverage collection
- **k6** — JavaScript-based load testing

### Test Infrastructure

- `TestWebApplicationFactory` — Custom `WebApplicationFactory` with InMemory database and configurable fake authentication.
- `TestAuthHandler` — Bypass authentication scheme for integration tests.
- `TestDbContextFactory` — Creates isolated `ApplicationDbContext` instances per test.
- `PassthroughCacheService` — No-op cache implementation for deterministic unit tests.

### Running Tests

```bash
# Run all tests
dotnet test Tests/Tests.csproj

# Run with code coverage
dotnet test Tests/Tests.csproj --collect:"XPlat Code Coverage" --results-directory TestResults

# Run specific test category
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Services"

# Run load tests (requires k6 installed)
k6 run Tests/load-tests/reporting-smoke.js
```

---

## CI/CD

### GitHub Actions Pipelines

#### CI Tests & Coverage (`ci-tests-coverage.yml`)

**Triggers:** All pushes and pull requests on all branches.

**Steps:**
1. Checkout code
2. Setup .NET 8.0 SDK
3. Restore NuGet packages
4. Run tests with XPlat Code Coverage (Coverlet)
5. Upload test results and coverage reports as artifacts

#### Security Scans (`security-scans.yml`)

**Triggers:** Pull requests + weekly schedule (Monday 3:00 AM UTC).

**Jobs:**
1. **Dependency Review** (PR only) — Analyzes dependency changes for known vulnerabilities.
2. **NuGet Vulnerability Scan** — Runs `dotnet list package --vulnerable --include-transitive` to detect vulnerable packages.

---

## Security Considerations

### Authentication

- **ASP.NET Core Identity** with custom user model (`ApplicationUser`).
- Strong password policy: 12+ characters, uppercase, lowercase, digit, special character, 6 unique characters minimum.
- Account lockout: 5 failed attempts → 15-minute lockout.
- Sliding session expiration: 30 minutes of inactivity.
- IT-admin-approved password reset workflow with ticket numbers and expiration.

### Authorization

- Hybrid **role-based + claim-based** authorization with 6 policies.
- Custom `ApplicationUserClaimsPrincipalFactory` injects domain claims at login.
- Controller-level and action-level authorization attributes.
- Scope-based data filtering (managers only see their own department/project data).

### Data Protection

- **Cookie Security** — `HttpOnly`, `Secure`, `SameSite=Strict`.
- **Anti-Forgery Tokens** — Validated on all POST requests via `X-CSRF-TOKEN` header.
- **CSRF Protection** — `SameSite=Strict` cookies + anti-forgery token validation.
- **Data Protection Keys** — Persisted to filesystem with application-specific purpose (`ERP-CompanyManagement`).

### HTTP Security Headers

| Header | Value | Purpose |
|---|---|---|
| `Content-Security-Policy` | Script/style from self + CDNs only | Prevent XSS |
| `X-Content-Type-Options` | `nosniff` | Prevent MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `X-XSS-Protection` | `1; mode=block` | Browser XSS filter |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Control referrer leakage |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Enforce HTTPS |

### Input Validation & Sanitization

- `InputSanitizer` utility — HTML encoding, SQL LIKE escaping, XSS prevention, filename sanitization, phone number validation.
- File upload validation — 10MB size limit, extension whitelist, MIME type checking, magic byte (file signature) verification.
- Optimistic concurrency on critical entities via `RowVersion`.

### Monitoring & Audit

- Comprehensive `AuditLog` capturing all significant user actions with IP address and user agent.
- `AuditAnomalyService` automatically detects suspicious patterns (brute-force, unusual login times).
- `AnomalyDetectionNotificationJob` runs every 2 hours and sends critical alerts to CEO users.
- IT Admin dashboard provides real-time security metrics.

### Rate Limiting

- Token bucket rate limiter on reporting endpoints (30 requests/minute per user).
- Status 429 (Too Many Requests) returned when limits are exceeded.

---

## Future Improvements

- **REST API Layer** — Expose a full Web API for mobile and third-party integrations.
- **Microservices Migration** — Extract reporting, notifications, and AI services into independent microservices.
- **Advanced Analytics Dashboard** — Interactive charts with drill-down capabilities using a client-side charting library.
- **AI-Based Predictions** — Machine learning models for task estimation accuracy, employee attrition risk, and project success probability.
- **Real-Time Collaboration** — Live task board updates via SignalR for multi-user collaboration.
- **Multi-Tenancy** — Tenant isolation for SaaS deployment across multiple organizations.
- **Localization** — Multi-language support with resource-based localization.
- **Docker Containerization** — Dockerfile and Docker Compose for containerized deployment.
- **Audit Log Export** — Export audit trails to external SIEM systems.
- **Two-Factor Authentication** — TOTP/SMS-based 2FA for enhanced account security.

---

## Developer Information

- **Lead Developer:** Mohamed Ismail
- **Email:** Mohamed_EMohamed@outlook.com
- **GitHub:** [@Som3a99](https://github.com/Som3a99)

For any inquiries or contributions, please reach out via email or GitHub.

---

<p align="center">
  Built with ❤️ using ASP.NET Core 8.0
</p>