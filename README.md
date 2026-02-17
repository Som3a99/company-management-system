# CompanyFlow - Business Management Platform

## Elevator Pitch
“It is a web-based company management system designed to help businesses manage employees, projects, tasks, and operations in one centralized platform. The system is modular, customizable, and can be deployed for different companies as a ready-to-use business solution.”

## 1-Minute Professional Explanation
CompanyFlow is a modular ERP-style management system that improves operational efficiency by centralizing employees, departments, projects, tasks, and accountability workflows. It is built as a scalable multi-company-ready platform, with role-based permissions, progress tracking, and structured workflows suitable for real business operations.

## Project Overview (Technical)

### Purpose
- Manage employees and departments in a single source of truth
- Track projects, tasks, and delivery progress
- Improve visibility and accountability across teams
- Standardize internal workflows with role-based controls

### Core Features
- Employee & Department management
- Project & Task tracking
- Role-based access control
- Workflow and status monitoring
- Audit and activity tracking foundation
- Modular architecture for future customization

### Technology Stack
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- - Razor Views + Bootstrap/CSS
- Layered architecture (`ERP.PL`, `ERP.BLL`, `ERP.DAL`)

### Architecture
- **ERP.PL (Presentation Layer):** MVC controllers, views, middleware, web configuration
- **ERP.BLL (Business Logic Layer):** services, interfaces, application rules
- **ERP.DAL (Data Access Layer):** EF Core context, entities, persistence

---

## System Workflow (Non-Technical)

For non-technical teams, CompanyFlow can be described simply as:

> “A system that helps companies organize their work, track employee tasks, and manage projects in one place instead of using scattered tools.”

### How work flows in the system
1. Admin/Manager signs in
2. Employees and departments are organized
3. Projects are created and assigned
4. Tasks are distributed to team members
5. Progress is monitored through dashboard/reporting views
6. Managers follow up with clear accountability data

### Business Value
- Reduces operational chaos
- Improves team productivity
- Centralizes company information
- Increases management visibility
- Adaptable across industries

### Suggested Website Taglines
- **Smart Business Management in One Platform.**
- **Organize Work. Track Progress. Grow Faster.**
- **From Tasks to Teams - Everything in One Place.**

---

## Environments

The system supports environment-based configuration using `ASPNETCORE_ENVIRONMENT`:
- `Development` -> local/dev database and debug-friendly settings
- `Production` -> secure production configuration with secrets from environment variables

See `DEPLOYMENT.md` for branch strategy, release process, deployment steps, rollback, and backup guidance.


## Seeding Modes

The app supports environment-aware seeding:
- `Seed__Mode=Development` -> full demo dataset for QA/UAT
- `Seed__Mode=Production` -> production-safe baseline (roles + admin only)

See `DEPLOYMENT.md` section **Database Seeding (Development vs Production)** for exact commands and comparison matrix.

---

## Developer Information

- **Lead Developer:** Mohamed Ismail
- **Email:** Mohamed_EMohamed@outlook.com
- **GitHub:** @Som3a99

For any inquiries or contributions, please reach out via email or GitHub.
