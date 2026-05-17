# Security Report: Terminal Section and User Audits

## Overview

This report summarizes the security features added to BranchPOS around LAN terminals, terminal-based sessions, and user audit tracking. The goal of these changes is to make every POS action traceable to a registered terminal, a signed-in user, an active branch, and a controlled session.

## Terminal Registration Security

Each browser used as a POS terminal must be registered before it can access the application. The terminal setup flow validates that the terminal code exists and is active before issuing a terminal identity.

Security controls added:

- Registered terminal list with unique terminal codes.
- Active/inactive status for each terminal.
- Branch assignment for every terminal.
- Terminal setup page for assigning a browser to a registered terminal.
- Application-wide terminal check that redirects unregistered browsers to terminal setup.
- Terminal code normalization to avoid duplicate or mismatched codes caused by casing or whitespace.

This prevents random browsers on the LAN from immediately using POS screens without first being linked to an approved terminal record.

## Protected Terminal Identity

The terminal identity is no longer just a plain terminal code. A random terminal token is generated, hashed, and stored in the database. The browser receives a protected cookie containing the terminal code and raw token.

Security controls added:

- 32-byte random terminal token generation.
- SHA-256 token hashing before database storage.
- ASP.NET Data Protection for protecting the terminal identity cookie.
- Fixed-time hash comparison when verifying terminal tokens.
- HttpOnly terminal cookie to reduce JavaScript access.
- SameSite Strict cookie setting.
- Secure cookie enforcement in production when configured.
- Expiring terminal cookie based on operational settings.
- Legacy terminal-code cookie removal after issuing the protected identity.

This makes terminal identity harder to spoof because knowing a terminal code alone is not enough once a token hash exists.

## Terminal Access Enforcement

Most application routes now require a valid terminal identity before continuing. Only setup, account, and static asset paths bypass this check.

Security controls added:

- Middleware-level terminal validation before protected pages are reached.
- Rejection of missing, invalid, inactive, or tampered terminal identities.
- Central `RequireCurrentTerminalAsync` guard used by orders, sessions, purchases, and inventory.

This ensures that POS operations are consistently tied to a registered terminal.

## Branch Access Protection

Terminal and session operations respect branch assignments. Users can only work with branches they are authorized to access.

Security controls added:

- Admins can see active branches.
- Non-admin users are limited to their assigned active branch.
- Terminal creation, editing, toggling, and session start validate branch access.
- Session start is blocked when the terminal branch does not match the user's allowed branch.

This reduces the risk of users operating in the wrong branch or viewing/editing terminal data outside their scope.

## Session Security

The user session flow now binds work to a user, branch, role, terminal ID, terminal code, and terminal name.

Security controls added:

- A registered terminal is required before starting a session.
- A user can only have one active session at a time.
- PostgreSQL advisory locking is used while starting sessions to reduce race conditions.
- Unique database index enforces one active session per user.
- Session resume can be restricted to the same terminal.
- Active draft orders must be completed or cancelled before ending a session.
- Stale sessions can be marked as interrupted when heartbeats stop.
- Session codes are generated from a database sequence for reliable uniqueness.

This protects against duplicate sessions, cross-terminal misuse, and unclear responsibility for POS actions.

## Transaction-Level Terminal Validation

Orders, purchases, and inventory adjustments validate that the operation's terminal matches the active session.

Security controls added:

- Orders require terminal identity and validate terminal ID/code against the active session.
- Purchases require terminal identity and validate terminal ID/code against the stock session.
- Inventory adjustments require terminal identity and validate terminal ID/code against the stock session.
- Transactions are blocked if the referenced terminal is inactive or not registered.
- Orders, purchases, and inventory transactions store terminal ID and terminal code for traceability.

This prevents a request from one terminal from being submitted under another terminal's active session.

## Terminal Heartbeats and Monitoring

Terminal heartbeat tracking records which terminal is online, when it was last seen, and which user/session is currently active.

Security controls added:

- One heartbeat record per terminal.
- Last-seen timestamp for online/offline status.
- Current user and current session tracking.
- Branch-level heartbeat filtering/indexing.
- Write throttling to avoid excessive heartbeat database writes.
- Admin terminal screen shows recent heartbeat information.

This gives administrators visibility into active terminals and helps identify stale or disconnected sessions.

## Audit Logging

A new audit log system records important security and administration events with contextual metadata.

Audit data captured:

- Action name.
- Entity name and entity ID.
- Old values and new values in JSON format.
- User ID.
- Branch ID.
- Terminal ID.
- Timestamp.
- Request IP address.
- Browser user agent.

Logged actions include:

- Terminal created.
- Terminal updated.
- Terminal activated/deactivated.
- Session started.
- Session continued.
- Session ended.
- Session interrupted.
- Branch created.
- Branch updated.
- Branch activated/deactivated.

This gives the system a clear trail of who changed what, when they changed it, from which browser/IP, and which branch or terminal was involved.

## Admin-Only Terminal Management

The terminal management section is protected with admin authorization.

Security controls added:

- Terminal list, create, edit, and toggle actions require the Admin role.
- Terminal create/edit/toggle POST actions require anti-forgery tokens.
- Terminal code uniqueness is enforced at the database level.
- Terminal changes are audit logged with old and new values.

This prevents non-admin users from registering, editing, or disabling terminals.

## User Account Controls

User management is also restricted to admins and includes branch and role validation.

Security controls added:

- Admin-only user management.
- Password policy requiring at least 8 characters, uppercase, lowercase, and digits.
- Cashiers and stock managers must be assigned to a branch.
- Users can be deactivated.
- Users with operational history are deactivated instead of deleted.
- Role and branch changes require anti-forgery protection.

This keeps historical records connected to the original user and prevents accidental deletion of users tied to orders, sessions, purchases, or inventory transactions.

## Database Integrity Improvements

Several database-level protections support the security features.

Security controls added:

- Unique terminal codes.
- Unique terminal heartbeat per terminal.
- Unique active session per user.
- Unique public IDs for orders, purchases, inventory transactions, and sessions.
- Audit log indexes by timestamp, user, entity, branch, and terminal.
- Foreign keys from audit logs to users, branches, and terminals.
- Restrictive foreign keys on operational records to preserve history.

These constraints make the security rules harder to bypass through concurrency issues or direct data inconsistencies.

## Current Coverage and Notes

The implemented security model now covers terminal identity, branch access, active user sessions, terminal/session matching, terminal monitoring, and audit trails for important operational changes.

Areas that may be considered for future hardening:

- Add audit log entries for user creation, role assignment, activation/deactivation, and deletion/deactivation decisions.
- Add an admin-facing audit log viewer with filters by user, terminal, branch, action, and date.
- Rotate terminal tokens manually when a terminal browser is replaced or suspected compromised.
- Enforce HTTPS for LAN production deployments.
- Add rate limiting to login and terminal setup attempts.
- Consider showing warnings for terminals that are active in the database but have not sent a heartbeat recently.

## Conclusion

The terminal and audit security additions significantly improve BranchPOS accountability. POS activity is now tied to a registered terminal, an authorized user, an assigned branch, and an active session. Administrative terminal changes and session lifecycle events are recorded in audit logs, while token-based terminal identity and branch/session validation reduce the chance of unauthorized or cross-terminal activity.
