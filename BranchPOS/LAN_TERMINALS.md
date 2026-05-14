# LAN terminal setup

BranchPOS is a branch-server MVC application. Cashier terminals use a browser and connect to the same branch server and PostgreSQL database.

Run the branch server on the LAN profile:

```powershell
dotnet run --project BranchPOS\BranchPOS.csproj --launch-profile lan
```

Cashier terminals can then open:

```text
http://BRANCH_SERVER_LOCAL_IP:5000
```

Do not hardcode the server IP in application code. Change the `lan` launch profile or hosting configuration if the branch server needs a different port.

Each browser must be assigned a registered terminal code on the Terminal Setup page. The code is stored in a browser cookie and mirrored to `localStorage`.
