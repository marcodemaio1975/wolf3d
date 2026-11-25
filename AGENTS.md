# RetroRaycaster

## Struttura

- `RetroRaycaster.sln` raccoglie i tre progetti C# con target `net8.0`.
- `src/RetroRaycaster.Core/` contiene la libreria autonoma.
- `src/RetroRaycaster.Desktop/` contiene l'eseguibile e fa riferimento a Core.
- `tests/RetroRaycaster.Core.Tests/` contiene i test xUnit di Core.
- `WOLFSRC/` conserva i sorgenti DOS come riferimento storico. Mantieni questa directory intatta.

## Verifica

Usa un SDK .NET 8 compatibile con `global.json`. Dalla radice del repository esegui gli stessi comandi del workflow GitHub Actions.

```sh
dotnet restore RetroRaycaster.sln
dotnet build RetroRaycaster.sln -c Release --no-restore
dotnet test RetroRaycaster.sln -c Release --no-build
```

## Destinazione delle pull request

Crea ogni pull request all’interno del fork `marcodemaio1975/wolf3d`
con queste impostazioni:

- Base repository: `marcodemaio1975/wolf3d`
- Base branch: `master`
- Head repository: `marcodemaio1975/wolf3d`
- Compare branch: il branch del task, ad esempio `feature/software-renderer`

Prima di creare la PR, verifica che sia il repository di origine sia quello
di destinazione siano `marcodemaio1975/wolf3d`. Apri una PR verso
`id-Software/wolf3d` solo su mia richiesta esplicita.
