# Software Development Simulation

Ett interaktivt, plattformsoberoende desktopverktyg för att utforska flöde i mjukvaruutvecklingsorganisationer. **Detta är ett simuleringsverktyg, inte ett projektledningsverktyg.**

## Teknik och förutsättningar

- C#, .NET 10 LTS, Avalonia 11.3.22, MVVM och xUnit.
- macOS på Apple Silicon eller Intel med motsvarande [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Internetanslutning för första NuGet restore.
- Samma källkod kan byggas för Windows och Linux; endast macOS är verifierat.
- `global.json` tillåter installerade .NET 10 SDK-featureband. Ingen separat Avalonia-workload behövs.

## Bygg, testa och kör

Från projektroten:

```sh
dotnet restore SoftwareDevelopmentSimulation.sln
dotnet build SoftwareDevelopmentSimulation.sln --configuration Release --no-restore
dotnet test SoftwareDevelopmentSimulation.sln --configuration Release --no-build
dotnet run --project src/Simulation.UI/Simulation.UI.csproj --configuration Release --no-build
```

Under den initiala verifieringen installerades SDK tillfälligt i `/tmp/simulation-dotnet` eftersom datorn saknade .NET. Så länge den installationen finns kan samma kommandon användas efter:

```sh
export DOTNET_ROOT=/tmp/simulation-dotnet
export PATH="$DOTNET_ROOT:$PATH"
```

För fortsatt användning rekommenderas en vanlig SDK-installation; `/tmp` är inte permanent lagring.

## Användning

1. Ange personalstyrka, kapacitet per person och dag samt WIP-gräns.
2. Ange antal objekt, simuleringslängd, sprintlängd, releaseintervall och seed.
3. Ange storlek, komplexitet och sannolikheten att ett objekt beror på ett tidigare objekt. Decimaler kan anges med punkt eller komma.
4. **Run Simulation (A)** och **Run 100 Runs (A)** kör bara A. **Compare A / B** kör ett par och **Compare 100 Pairs** kör 100 par med seeds `seed … seed + 99`. B kan ha annan personal, kapacitet och WIP. Arbetsobjekt, beroenden, tid och seed är gemensamma. Körningen sker i bakgrunden och kan avbrytas med **Cancel**.
5. **Overview** visar mått för A, B och differensen B − A. Differenser i procentmått visas i procentenheter. Ett positivt värde är inte automatiskt bättre.
6. **Status over time** visar staplade statusantal vid varje dags slut. Dagreglaget inspekterar samma dag i båda diagrammen, med exakta statusantal i text. Diagrammen har samma skala. Vid batchkörning visas dagliga medelantal.
7. **Unfinished work** visar kvarvarande objekt per status, beroendeblockerad/redo backlog och ålder vid horisonten. Vid enskilda körningar visas upp till 20 äldsta objekt per scenario. Batchen visar medelvärden, utan att blanda individlistor från olika körningar.
8. Felaktiga värden visas i fönstret. Resultat från en tidigare körning töms när nästa påbörjas. Fel i B hindrar jämförelse men påverkar inte fristående A-körningar.

Kapacitet anges i arbetsenheter, inte antal parallella ärenden. Större storlek eller komplexitet förlänger utvecklingen. Noll utvecklare, testare eller kapacitet är tillåtet för att utforska flaskhalsar. WIP och tidsintervall måste vara positiva. UI/application-lagret begränsar scenarier till 2 000 objekt, 3 650 dagar och högst 1 000 000 objekt-dagar per körning.

## Struktur och beroenden

```text
SoftwareDevelopmentSimulation.sln
src/
  Simulation.Core/             Domän, validering, diskret simuleringsmotor
  Simulation.Application/      Requests, scenariogenerering, parade experiment och resultatanalys
  Simulation.Infrastructure/   Reserverat för framtida persistens
  Simulation.UI/               Avalonia-vyer, ViewModels, kommandon och statusdiagram
tests/
  Simulation.Core.Tests/       Regler, tidsgränser, kapacitet och mätetal
  Simulation.Application.Tests/ Scenariogenerering och batchorkestrering
docs/
  SIMULATION_MODEL.md          Modellens regler, formler och begränsningar
```

Projektberoenden: `UI → Application → Core`, `Infrastructure → Application`. Core använder endast .NET:s standardbibliotek. Ingen databas, service container eller extern MVVM-ram är nödvändig. ViewModel gör inputvalidering och anropar Application; vyernas code-behind innehåller ingen simuleringslogik. Infrastructure innehåller medvetet ingen påhittad persistensimplementation.

## Tester

xUnit täcker samtliga tio begärda fall: beroenden blockerar start, varje statusövergång, WIP-gräns, utvecklar- och testkapacitet, seedreproducerbarhet, lead time och throughput. Ytterligare tester täcker bland annat cycle time, blockerad tid, framtida ankomster, tomma scenarier, noll kapacitet, sprintar/releaser, ogiltiga indata, avbrott och batchmedelvärden.

## Verifiering av inkrement 2

Verifierat 2026-09-24 på samma macOS ARM64-miljö som nedan:

- Release-build: **0 varningar, 0 fel**.
- xUnit: **36 godkända tester** (20 Core, 16 Application).
- 10 nya tester kontrollerar identiska arbetsobjekt/beroenden för A/B, identiska resultat för samma teaminställningar, ändrad testkapacitet, dagliga statusantal, blockerade objekt vid horisonten, ankomst-/åldersgränser, batchmedelvärden, seedöverslag, tomma scenarier och validering/avbrott.
- Native macOS-smoketest verifierade 18 parameterfält, båda jämförelseknapparna, oförändrade A-körningar, två databundna diagram, dagreglage, listor, felhantering och avbrott. Bilder av samtliga tre flikar granskades.
- Modellreglerna och källfilerna i Core är oförändrade. Analysen bygger på motorns befintliga resultat.

## Verifiering av inkrement 1

Verifierat 2026-09-24 på macOS 26.5, Apple Silicon (`osx-arm64`), .NET SDK 10.0.401/runtime 10.0.12:

- Release-build: **0 varningar, 0 fel**.
- xUnit: **26 godkända tester** (20 Core, 6 Application).
- Native Avalonia-smoketest med appens riktiga App, MainWindow och ViewModel: 13 parameterbindningar laddades, ändrad inmatning slog igenom och resultatbindningar uppdaterades.
- Både Run Simulation och Run 100 Runs kördes via de databundna knappkommandona.
- Ogiltig inmatning visade fel och tömde tidigare resultat.
- Fönstrets renderade layout granskades. Scrollning används när alla parametrar inte ryms på skärmen.

Smoketestet använde en tillfällig extern testvärd; det ingår inte i xUnit-sviten. Vid UI-ändringar bör samma användarflöde kontrolleras genom att starta desktopappen.

## Avgränsning och nästa steg

Ett team körs i första inkrementet. Code Review tar en dag utan separat resursmodell. Sprintar och releaser används för gruppering och leveransrapportering och begränsar inte när arbete får börja. Alla genererade objekt finns i backlog vid dag noll. Ingen persistens, export, installationspaketering eller projektplanering ingår.

Scenariojämförelse, statusserier och ofärdigt arbete finns nu i inkrement 2. Lämpliga nästa steg är fördelningar/percentiler från batcher och sparade scenarier, innan simuleringsreglerna utökas. Teamtilldelning och beroenden mellan flera team återstår. Fullständiga antaganden finns i [SIMULATION_MODEL.md](docs/SIMULATION_MODEL.md).

Tekniska referenser: [Avalonias dokumentation](https://docs.avaloniaui.net/docs/get-started), [Microsofts .NET-versioner och LTS](https://dotnet.microsoft.com/en-us/download/dotnet).
