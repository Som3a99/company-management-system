# Deployment & Release Operations Guide

## 1) Environment Configuration

This project uses `ASPNETCORE_ENVIRONMENT` with environment-specific settings files:

- `ERP.PL/appsettings.json` (base/shared)
- `ERP.PL/appsettings.Development.json`
- `ERP.PL/appsettings.Production.json`

### Required environment variables (Production)

Set these in the hosting platform or server profile (never commit secrets):

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Server=<sql-host>;Database=ERPDB_Prod;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;"
DataProtection__KeysPath="/var/app/erp/dataprotection-keys"
Database__ApplyMigrationsOnStartup=false
```

> Use secure secret storage (Azure Key Vault / AWS Secrets Manager / CI secret store).

---

## 2) Branch Workflow (Single Repository)

### Primary branches

- `main` → production-ready code
- `develop` → active integration branch for ongoing work

### Supporting branches

- `feature/<feature-name>`
- `hotfix/<issue>`
- `release/<version>` (optional)

---

## 3) Workflow Rules

### Feature development

1. Branch from `develop`: `feature/<feature-name>`
2. Implement + test
3. Open PR into `develop`
4. Merge after review and checks pass

### Release process

1. Validate `develop`
2. (Optional) create `release/<version>`
3. Final verification + version bump
4. Merge into `main`
5. Tag release (`v1.0.0`, `v1.1.0`, ...)

### Hotfix process

1. Branch from `main`: `hotfix/<issue>`
2. Implement urgent fix + test
3. Merge into `main`
4. Merge same fix back into `develop`

---

## 4) Deployment Procedures

## Development server (from `develop`)

```bash
git checkout develop
git pull
dotnet restore CompanyManagementSystem.sln
dotnet build CompanyManagementSystem.sln -c Release
dotnet publish ERP.PL/ERP.PL.csproj -c Release -o /var/www/erp-dev
```

## Production server (from `main`)

```bash
git checkout main
git pull --ff-only
dotnet restore CompanyManagementSystem.sln
dotnet build CompanyManagementSystem.sln -c Release
dotnet publish ERP.PL/ERP.PL.csproj -c Release -o /var/www/erp-prod
```

### Migration execution

Preferred explicit migration command during deployment window:

```bash
dotnet ef database update --project ERP.DAL/ERP.DAL.csproj --startup-project ERP.PL/ERP.PL.csproj --configuration Release
```

Or controlled startup migration (non-production only by default):

```bash
Database__ApplyMigrationsOnStartup=true
```

### Restart procedure

Example (systemd service):

```bash
sudo systemctl daemon-reload
sudo systemctl restart erp-pl.service
sudo systemctl status erp-pl.service --no-pager
```

---

## 5) Rollback Procedure

1. Identify previous stable git tag (example `v1.0.0`)
2. Redeploy that tag
3. Re-run publish and restart service
4. If schema changed, execute rollback migration plan (forward-fix preferred if already live)

Example:

```bash
git checkout v1.0.0
dotnet publish ERP.PL/ERP.PL.csproj -c Release -o /var/www/erp-prod
sudo systemctl restart erp-pl.service
```

---

## 6) Backup Instructions

### Database backup (SQL Server)

- Full backup daily
- Differential backup every 6 hours
- Transaction log backups every 15 minutes (if Full recovery model)
- Keep encrypted off-site copies

### App-level artifacts

- Backup persisted data-protection keys (`DataProtection__KeysPath`)
- Backup uploads/documents directory
- Keep at least 14 daily restore points

---

## 7) Optional/Recommended Improvements

- Add structured request logging (correlation IDs + latency) to centralized log storage
- Add audit trail reporting dashboards
- Implement nightly automated database backup verification (restore test)
- Use feature flags to hide incomplete features safely

---

## 8) Release Discipline Tips

- Keep commits small and focused
- Tag every production release (`vX.Y.Z`)
- Require PR checks before merging to `main`


## 9) Database Seeding (Development vs Production)

The application supports environment-aware seeding via configuration keys:

- `Seed:Mode = None | Development | Production`
- `Seed:ResetDatabase = true|false`
- `Seed:ProductionAdminEmail`
- `Seed:ProductionAdminPassword`

### Seed Development Database (rich demo data)

```bash
ASPNETCORE_ENVIRONMENT=Development \
Seed__Mode=Development \
Seed__ResetDatabase=true \
Database__ApplyMigrationsOnStartup=true \
dotnet run --project ERP.PL/ERP.PL.csproj
```

Default seeded login accounts include:
- `ceo@company.com`
- `it.admin@company.com`
- employee/project manager accounts
- password: `Test@12345Ab`

### Seed Production Database (baseline safe data)

```bash
ASPNETCORE_ENVIRONMENT=Production \
Seed__Mode=Production \
Seed__ResetDatabase=false \
Seed__ProductionAdminEmail="admin@yourcompany.com" \
Seed__ProductionAdminPassword="<strong-password>" \
Database__ApplyMigrationsOnStartup=true \
dotnet run --project ERP.PL/ERP.PL.csproj
```

Production seeding only creates baseline identity roles and one admin account (no QA/demo dataset).

### Comparison Checklist

| Area | Development Seed | Production Seed |
|---|---|---|
| Schema migrations | ✅ | ✅ |
| Roles | ✅ | ✅ |
| Demo departments/employees/projects/tasks | ✅ | ❌ |
| Password reset/audit demo records | ✅ | ❌ |
| Admin user | ✅ (`ceo@company.com`, etc.) | ✅ (from env vars) |
| Database reset allowed | Optional (`Seed:ResetDatabase`) | Must remain `false` |
