# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability in this project, please report it responsibly.

### How to Report

1. **Do NOT open a public GitHub issue** for security vulnerabilities.
2. Email the maintainer at the email address listed in the repository profile.
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- **Acknowledgment**: Within 48 hours of your report.
- **Assessment**: We will investigate and assess the severity within 5 business days.
- **Resolution**: Critical vulnerabilities will be patched as soon as possible. Non-critical issues will be addressed in the next release cycle.
- **Disclosure**: We will coordinate public disclosure with you after a fix is available.

## Security Measures

This project implements the following security measures:

### Authentication & Authorization
- ASP.NET Core Identity with strict password requirements (12+ characters, mixed case, digits, symbols)
- Account lockout after 5 failed attempts (15-minute lockout)
- Role-based and claims-based authorization policies
- CSRF/anti-forgery token protection
- Secure cookie configuration (HttpOnly, Secure, SameSite=Strict)

### Data Protection
- ASP.NET Core Data Protection API for key management
- Entity Framework Core with parameterized queries (no raw SQL)
- Input sanitization (HTML encoding, filename sanitization)
- File upload validation (extension whitelist, MIME type check, magic byte verification)

### Transport Security
- HTTPS enforcement with HSTS
- Secure cookie transport
- TLS for SMTP communication

### Security Headers
- Content-Security-Policy (CSP)
- X-Content-Type-Options: nosniff
- X-Frame-Options: DENY
- X-XSS-Protection: 1; mode=block
- Referrer-Policy: strict-origin-when-cross-origin
- Permissions-Policy
- Strict-Transport-Security

### CI/CD Security
- **CodeQL**: Static Application Security Testing (SAST) on every push/PR
- **Gitleaks**: Secret scanning across commit history
- **Dependabot**: Automated dependency vulnerability alerts and updates
- **Snyk**: Dependency vulnerability scanning with SARIF reporting
- **OWASP ZAP**: Dynamic Application Security Testing (DAST)
- **Dependency Review**: PR-level dependency change analysis

### Secrets Management
- All secrets must be stored in environment variables or GitHub Secrets
- No credentials in source-controlled configuration files
- Use `dotnet user-secrets` for local development

## Security Configuration Checklist

For deployment, ensure:

- [ ] All secrets are provided via environment variables (not config files)
- [ ] `AllowedHosts` is set to specific production domains
- [ ] Database connection uses managed identity or environment-injected credentials
- [ ] SMTP credentials are provided via environment variables
- [ ] AI service API keys are provided via environment variables
- [ ] HTTPS is enforced with valid TLS certificates
- [ ] Branch protection rules are enabled on `main`
- [ ] GitHub Security Advisories are monitored
- [ ] Dependabot alerts are reviewed promptly

## Branch Protection Recommendations

Enable these settings on the `main` branch:

1. **Require pull request reviews** before merging (at least 1 reviewer)
2. **Require status checks to pass** before merging:
   - CI Tests & Coverage
   - Security Scans
   - CodeQL Analysis
   - Gitleaks Secret Scanning
3. **Require branches to be up to date** before merging
4. **Do not allow force pushes**
5. **Do not allow deletions**
6. **Require signed commits** (recommended)
