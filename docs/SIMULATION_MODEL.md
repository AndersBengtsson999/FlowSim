# Simuleringsmodell – inkrement 1 och resultatanalys i inkrement 2

## Syfte och domän

Modellen utforskar hur kapacitet, arbetsstorlek, komplexitet, beroenden och WIP påverkar leverans. Den ger förenklade experimentresultat, inte en prognoskalibrerad organisationsmodell.

- **Organization** har ett namn och en lista av **Team**. I denna version måste listan innehålla exakt ett team; fler team avvisas tills det finns uttryckliga regler för teamtilldelning.
- **Team** innehåller namn, antal utvecklare, antal testare och WIP-gräns.
- **WorkItem** innehåller ID, titel, storlek, komplexitet, prioritet, status samt skapad-, start- och klartid. Tider är heltal i förflutna simuleringsdagar, inte kalenderdatum. Större prioritetsvärde behandlas först.
- **Dependency(WorkItemId, DependsOnWorkItemId)** anger en riktad relation. Alla föregångare måste vara Done innan Development får börja. Flera beroenden stöds av Core. Saknade referenser, självberoenden och cykler avvisas; dubbletter har ingen extra effekt.
- **Sprint(Start, End, Duration)** är ett halvt öppet intervall. Sista sprinten kortas vid simuleringens slut.
- **Release(Date, IncludedWorkItems)** innehåller objekt som blivit Done vid releasedatumet och inte ingått i en tidigare release.
- **SimulationScenario** håller organisation, arbetsobjekt, beroenden, daglig kapacitet per person, längd, sprint-/releaseintervall och seed.
- **SimulationResult** innehåller mätetal, slutliga arbetsobjekt, dagliga snapshots, sprintar och releaser.

Motorn skapar separat körningstillstånd; inmatade objekt ändras inte. Core kan även användas direkt med heterogena objekt, egna prioriteter, skapandetider och beroenden.

## Ett diskret tidssteg

Dag `d` representerar intervallet `[d, d + 1)`. Körningen omfattar exakt `DurationDays` dagar från dag noll.

1. Vid dagens början räknas skapade, ofärdiga objekt och backlogobjekt som är blockerade av beroenden.
2. Prioritera objekt efter fallande prioritet. Likvärdiga objekt ordnas med seedstyrd slump, med ID som sista skiljekriterium. Indata sorteras först efter ID så att listordningen inte styr slumpsekvensen.
3. Fyll lediga WIP-platser med tillgängliga backlogobjekt vars samtliga beroenden är Done. `StartedAt = d`. Nya objekt får utvecklingskapacitet samma dag. Start innebär inträde i Development även om utvecklarkapaciteten är noll.
4. Sampla WIP efter intag, före dagens arbete. Development, CodeReview och Testing upptar alla WIP-platser.
5. Behandla varje objekt exakt en gång i dagens ordning:
   - **Development:** kvarvarande arbete börjar på `Size × Complexity`. Konsumera högst återstående gemensam utvecklarkapacitet. Vid noll återstående arbete: CodeReview.
   - **CodeReview:** gå till Testing. Detta steg upptar en hel dag men ingen separat utvecklar- eller testkapacitet.
   - **Testing:** kvarvarande testarbete börjar på `Size`. Konsumera högst återstående testkapacitet. Vid noll återstående arbete: Done och `CompletedAt = d + 1`.
6. Spara dagens förbrukning och statusar efter arbetet. Om `d + 1` är en releasegräns, skapa en release av hittills klara, ännu ej releasade objekt.

Ett objekt kan aldrig kaskadera genom flera statusar på samma dag. En ledig WIP-plats eller ett avklarat beroende kan utnyttjas tidigast nästa dag. Ett objekt med storlek 1, komplexitet 1 och kapacitet 1 blir CodeReview vid slutet av dag 0, Testing vid slutet av dag 1 och Done vid tidpunkt 3.

## Kapacitet och WIP

```text
Daglig utvecklarkapacitet = NumberOfDevelopers × DeveloperCapacityPerDay
Daglig testkapacitet      = NumberOfTesters × TesterCapacityPerDay
Utvecklingsarbete        = Size × Complexity
Testarbete               = Size
```

Kapaciteter är separata pooler. Oanvänd kapacitet förs inte vidare till nästa dag. Ett objekt får konsumera hela poolen; inga individer eller maximalt en utvecklare per objekt modelleras. Prioritet och seedstyrd ordning styr fördelningen. WIP-gränsen begränsar aktiva objekt, inte antalet starter per dag. Noll personal/kapacitet är giltigt och kan skapa permanent kö.

## Seed och scenariogenerering

Application genererar `NumberOfWorkItems` likadana objekt med ID 1…N, prioritet 0 och `CreatedAt = 0`. Storlek och komplexitet är parametrar. Varje objekt efter det första får, med `DependencyProbability`, ett beroende till ett slumpmässigt valt lägre ID. Detta skapar en acyklisk graf med högst en föregångare per genererat objekt. Core stödjer fler föregångare.

Både generatorn och motorn använder egna `System.Random`-instanser med scenariots seed. Samma parametrar och seed reproducerar resultatet på samma .NET-version. Reproducerbarhet över framtida implementationer av .NET:s slumptalsgenerator garanteras inte. Utan beroenden och med identiska objekt kan olika seeds ge samma aggregerade mått; slump garanterar inte olika resultat.

100-körningsläget använder seed, seed + 1, … seed + 99. Heltalsöverslag sker uttryckligen med 32-bitars wraparound. Varje körning är fristående. Batchen sparar mätetal, inte 100 fullständiga dagshistoriker. UI visar aritmetiska medelvärden per körning, även för lead/cycle time, snarare än viktade medelvärden över alla objekt. Körningar utan färdiga objekt bidrar med noll för lead/cycle time.

## Mått och nämnare

| Mått | Definition |
|---|---|
| Completed Work Items | Antal objekt med Done vid körningens slut |
| Throughput | Antal Done / hela simuleringslängden, objekt per dag |
| Average Lead Time | Medel av `CompletedAt − CreatedAt` för Done-objekt |
| Average Cycle Time | Medel av `CompletedAt − StartedAt` för Done-objekt |
| Average WIP | Aritmetiskt medel av WIP-samplen efter dagens intag |
| Blocked Time | Summan av beroendeblockerade backlogobjekt vid dagsstart / summan av skapade, ofärdiga objekt vid dagsstart |
| Developer Utilization | Förbrukat utvecklingsarbete / tillgängligt utvecklingsarbete över alla dagar |
| Tester Utilization | Förbrukat testarbete / tillgängligt testarbete över alla dagar |

Blockerad tid är en andel **objekt-dagar**, inte en andel av kalenderdagarna. WIP-kö, resursbrist och CodeReview räknas inte som beroendeblockering. Beroendeblockering räknas även när WIP är fullt. Framtida objekt räknas först från CreatedAt. Utnyttjandegrad inkluderar alla simulerade dagar, även tomma dagar efter att backloggen tömts.

Noll nämnare ger noll. Lead/cycle time är noll när inget objekt är klart; det är en visningskonvention och betyder inte att ofärdigt arbete har noll leveranstid. Ofärdiga objekt ingår inte i lead/cycle time. UI visar andelar i procent. `DailySnapshot.Day` är nollbaserad; dess WIP/blocked-värden är från dagens början medan statusarna är från dagens slut.

### Kontrollerbart exempel

Två objekt A och B har storlek 1 och komplexitet 1. B beror på A, WIP är 2, och båda resurspoolerna har kapacitet 1/dag. Körningen varar 6 dagar.

- A startar dag 0 och är klart vid tidpunkt 3.
- B startar dag 3 och är klart vid tidpunkt 6.
- Throughput = 2/6, lead time = (3+6)/2 = 4,5 dagar, cycle time = 3 dagar.
- WIP är 1 varje dag. Blockerad tid = 3/9 objekt-dagar = 1/3.
- Båda utnyttjandegraderna är 2/6 = 1/3.

## Sprintar och releaser

Sprintar är rapporteringsintervall, inte fasta åtaganden eller batchgränser för intag. Release sker vid multiplar av ReleaseInterval. Done och released är skilda begrepp; ett objekt kan vara Done men vänta på en release efter simuleringshorisonten. Ingen extra slutrelease skapas. Throughput mäter Done, inte antal releasade objekt. Tomma schemalagda releaser behålls.

## Avgränsningar

Alla dagar har samma kapacitet: inga helger, frånvaro, omarbete, defekter, testfel, kompetensskillnader, möteskostnader eller tidsåtgång för context switching. Ingen separat reviewkapacitet eller tester-/utvecklarväxling. Ingen persistens eller kalibrering mot historiska data. Scenarioresultat är avsedda för jämförande experiment under dessa uttryckliga antaganden.


## Inkrement 2: parade experiment och resultatanalys

Inga statusregler, kapacitetsregler, slumpregler eller ursprungliga mätformler ändras. `ExperimentRunner` kör den befintliga motorn och `ResultAnalysis` projicerar dess utdata till rapporter.

### Rättvis A/B-jämförelse

`TeamParameters` för B kan ändra personalstyrka, kapacitet per person och WIP. A och B delar exakt samma WorkItem- och Dependency-listor, seed, horisont, sprintlängd och releaseintervall. Modellen ändrar aldrig dessa indata under körning. Ett seed per par genererar arbetsobjekten en gång; samma scenariotillstånd ligger till grund för båda körningarna.

100-parsläget kör två scenarier för varje seed i intervallet seed … seed + 99, med samma dokumenterade wraparound som tidigare. Rapporten innehåller aritmetiska medelvärden per scenario. Differensen är B − A. För procentmått anges differensen i procentenheter. Ingen statistisk signifikans eller generell förbättring sluts utifrån differensens tecken.

### Status över tid

Varje punkt representerar status **efter** dagens arbete; dag 1 är slutet av motorns dag 0. De fem statusantalens summa är antalet objekt som skapats under eller före den dagen. Framtida objekt utesluts från rapporten även om de redan finns i scenariots inputlista.

Diagrammen visar antal, inte andelar eller genomflöde. Båda använder samma skala eftersom jämförda scenarier delar arbetsobjekt och skapandetider. Ett dagreglage visar exakta antal för samma dag i A och B. Batchdiagrammet visar medelantal i varje status per dag; det är inte en enskild körning och kan innehålla bråktal.

### Ofärdigt arbete vid horisonten

- Ett ofärdigt objekt är skapat före horisonten och är inte Done.
- Ålder = horisonten − CreatedAt. Cycle age = horisonten − StartedAt, eller saknat värde om Development inte startat.
- Beroendeblockerad backlog räknas med **slutliga** statusar på föregångarna. Detta kan skilja sig från sista DailySnapshot.BlockedItems, som samplas vid dagens början.
- Redo backlog = backlog minus beroendeblockerad backlog. Dessa objekt kan vänta på WIP-utrymme eller nästa dags intag. Detta mått påstår inte att väntan beror på en bestämd resurs.
- Genomsnittlig och högsta ålder räknas bland ofärdiga objekt. Tom mängd ger noll.
- De 20 äldsta objekten sorteras efter fallande ålder, därefter stigande ID. UI visar status, ålder, cycle age och beroendeblockering.
- Alla objekt som genereras av nuvarande UI skapas dag 0. Därför är totalåldern densamma för alla kvarvarande objekt; cycle age och status kan däremot skilja sig. Mer varierande åldrar kräver framtida inflödesmodell eller egna skapandetider via Core.
- I batchläget visas medelvärden av körningarnas ofärdiga antal och åldersmått. ”Oldest age” är medelvärdet av varje körnings högsta ålder, inte maximum över hela batchen. Körningar utan ofärdiga objekt bidrar med noll. Individlistor visas endast för enskilda körningar.

Lead/cycle time inkluderar alltjämt endast Done-objekt. Gränssnittet uppmanar därför till att även granska kvarvarande arbete. Rapporterna introducerar inte censureringsjusterade ledtider eller prognoser för ofärdiga objekt.
