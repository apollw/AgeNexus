# Age Nexus

Plataforma para registrar, comprovar e analisar partidas da série *Age of Empires*.

O projeto está na fase de fundação. Consulte a estrutura em [`docs/architecture`](docs/architecture/solution-structure.md) e as decisões em [`docs/adr`](docs/adr/0001-modular-monolith.md).

Para configurar o ambiente e iniciar a aplicação, consulte [Como rodar o Age Nexus](docs/COMO-RODAR.md).

```powershell
dotnet build AgeNexus.slnx
dotnet test AgeNexus.slnx
dotnet run --project src/AgeNexus.Web
```
