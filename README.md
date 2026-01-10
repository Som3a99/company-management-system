# Company Management System

## Overview
Company Management System is a web-based enterprise application designed to manage
internal company operations such as departments, employees, workflows, and organizational data
in a structured and scalable manner.

The project is built with maintainability and extensibility in mind, following a clear
layered architecture and modern ASP.NET Core best practices.

---

## Core Objectives
- Centralized company data management
- Modular and scalable architecture
- Clean separation of concerns
- Foundation for role-based access control

---

## Tech Stack
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- Razor Views
- Bootstrap / CSS

---

## Architecture
The solution follows a **3-layer architecture**:

- **ERP.PL** – Presentation Layer  
  Controllers, Razor Views, UI logic

- **ERP.BLL** – Business Logic Layer  
  Business rules, services, interfaces

- **ERP.DAL** – Data Access Layer  
  Entity Framework Core, database context, migrations

---

## Getting Started (Local Development)

1. Install **Visual Studio** with ASP.NET and web development workload
2. Open the solution file:
3. Configure database connection in `appsettings.json`
4. Apply migrations or ensure the database is created
5. Run the project

---

## Security Notes
- Sensitive configuration files are excluded from source control
- Secrets such as connection strings should never be committed
- Use environment-specific configuration files locally

---

## Project Status
This project is under active development.

➡️ **See `PROGRESS.md` for detailed development status and roadmap.**

---

## Developer Information

- **Lead Developer:** Mohamed Ismail
- **Email:** Mohamed_EMohamed@outlook.com
- **GitHub:** @Som3a99

For any inquiries or contributions, please reach out via email or GitHub.
