<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="NewVistas.BlazorWeb/wwwroot/branding/newvistas-logo-dark.png">
    <img src="NewVistas.BlazorWeb/wwwroot/branding/newvistas-logo.png" alt="NewVistas EHR" width="440">
  </picture>
</p>

<p align="center"><em>A modern, open, modular Electronic Health Record platform.</em></p>

---

NewVistas is an open, modular Electronic Health Record platform inspired by the U.S. Department of Veterans Affairs' VistA and the Indian Health Service's RPMS. Built on **.NET 10**, **Microsoft Orleans** virtual actors, **Blazor Server**, and **ASP.NET Core**, it delivers a distributed, cloud-ready architecture that preserves four decades of VistA clinical wisdom while giving sites a contemporary developer and user experience.

## Getting started

- [START.md](START.md) — quick start and orientation
- [SETUP-DEVELOPMENT-ENVIRONMENT.md](SETUP-DEVELOPMENT-ENVIRONMENT.md) — set up a local dev environment
- [AZURE_DEPLOY.md](AZURE_DEPLOY.md) — deploy to Azure
- [SYSADMIN_GUIDE.md](SYSADMIN_GUIDE.md) — system administration
- [WEBSITE_BLURB.md](WEBSITE_BLURB.md) — full overview of the module suite

## Architecture at a glance

The solution is a .NET Aspire application. Key projects include the Blazor web frontend (`NewVistas.BlazorWeb`), the Orleans silo host (`NewVistas.SiloHost`), the API/web server (`NewVistas.WebServer`), the patient portal (`NewVistas.PatientPortal`), shared contracts (`NewVistas.Abstractions`), and the Aspire orchestrator (`NewVistas.AppHost`).

## License

See [LICENSE](LICENSE).
