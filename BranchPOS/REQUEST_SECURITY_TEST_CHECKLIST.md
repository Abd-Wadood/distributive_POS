# Request Overload and Login Security Checklist

- Try 6 wrong passwords for one account within one minute; the account should lock and show "Invalid login details or account temporarily locked."
- Rotate usernames from the same IP; audit logs should record `SuspiciousUsernameRotation` after repeated attempts.
- Submit `/Orders/Finalize` repeatedly from the same terminal/user; extra requests should return "You are submitting orders too quickly. Please wait."
- Call `/Products/Search` more than 20 times per minute from the same terminal/user/IP; extra requests should be rate limited.
- Send `/Sessions/Heartbeat` faster than the configured interval; extra requests should be rejected by `TerminalHeartbeatPolicy` and heartbeat storage should remain one latest row per terminal/session.
- Submit `/Sessions/Start` more than 3 times per minute; extra attempts should be rate limited and service logic should continue an existing active session instead of creating another for the same user.
- Visit `/Reports` as a cashier; access should be denied.
- Visit `/Reports` rapidly as admin/stock manager; extra requests should be rate limited and report rows should be paginated.
- Send a request body larger than the configured limit; the response should be friendly and not expose raw exceptions.
- Open Admin Dashboard after failures/rate-limit hits; security counters should reflect failed logins, locked accounts, rate-limit hits, suspicious IPs, heartbeat spam, and report spam.
