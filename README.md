# BranchPOS

BranchPOS is an ASP.NET Core MVC POS application using Entity Framework Core and PostgreSQL.

## Requirements

- .NET SDK 10
- PostgreSQL
- EF Core CLI tools (`dotnet-ef`)

Install the EF Core CLI tool if it is not already available:

```powershell
dotnet tool install --global dotnet-ef
```

## Clone and setup

```powershell
git clone <repository-url>
cd "Offline POS"
dotnet restore "Offline POS.sln"
dotnet build "Offline POS.sln"
```

Create your machine-specific configuration without committing credentials:

```powershell
Copy-Item BranchPOS\appsettings.Local.example.json BranchPOS\appsettings.Local.json
```

Edit `BranchPOS/appsettings.Local.json` and enter your PostgreSQL server, database, username, and password. This project uses Npgsql/PostgreSQL; a SQL Server `Trusted_Connection` string is not compatible unless the application provider and migrations are deliberately converted.

Apply the existing EF Core migrations:

```powershell
dotnet ef database update --project BranchPOS
```

Run the application:

```powershell
dotnet run --project BranchPOS
```

Run all tests:

```powershell
dotnet test "Offline POS.sln"
```

## Cleaning generated files

The cleanup script removes only generated build and test folders:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\clean.ps1
```

It does not remove source code, EF Core migrations, application settings, or `wwwroot`.
