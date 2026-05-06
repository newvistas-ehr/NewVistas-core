# Development Environment Setup

This project uses machine-specific `appsettings.Development.json` files that are **not tracked in Git**. Each developer/machine needs to create their own.

## Your Current Machine (MSSQLLocalDB)

The `appsettings.Development.json` files are already configured for `(localdb)\MSSQLLocalDB`.

## DIGITALSTORM-PC Machine Setup

When working on DIGITALSTORM-PC, update these files:

### NewVistas.SiloHost\appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Orleans": "Warning"
    }
  },
  "ConnectionStrings": {
    "SqlExpress": "Server=DIGITALSTORM-PC\\SQLEXPRESS;Database=NewVistasDB;Trusted_Connection=True;TrustServerCertificate=True;",
    "OrleansDatabase": ""
  }
}
```

### NewVistas.WebServer\appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Orleans": "Debug",
      "Orleans.Runtime": "Information"
    }
  },
  "ConnectionStrings": {
    "OrleansDatabase": "Server=DIGITALSTORM-PC\\SQLEXPRESS;Database=NewVistasOrleans;Trusted_Connection=True;MultipleActiveResultSets=true",
    "SqlExpress": "Server=DIGITALSTORM-PC\\SQLEXPRESS;Database=NewVistasDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

## Azure Demo Deployment

For Azure deployment, connection strings will be stored in:
- **Azure App Configuration** (recommended)
- **Azure Key Vault** (for production)
- **Azure App Service Configuration** settings (alternative)

The base `appsettings.json` files have empty connection strings, which will be overridden by Azure configuration.

## Running with SQL Express Mode

Use the `--use-sqlexpress` command-line argument to enable persistent SQL storage:

```powershell
dotnet run --project NewVistas.SiloHost -- --use-sqlexpress
```

This reads from the `SqlExpress` connection string in your Development settings.

## Note

**Never commit `appsettings.Development.json`** files to Git. They're now in `.gitignore`.
