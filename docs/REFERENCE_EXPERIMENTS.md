> **Historical proposal — superseded by Simulation Model v0.1.** This document describes the earlier pooled-capacity model. Its seeds, review factor, single WIP limit and numerical expectations are not current v0.1 behavior. See [SIMULATION_MODEL.md](SIMULATION_MODEL.md) for the implemented rules. Re-derive these experiments before turning them into v0.1 acceptance tests.

# Fem referensexperiment för simuleringskärnan

Status: **specifikation för nästa utvecklingssteg**, inte genomförda experiment eller uppmätta resultat. Dokumentet utgår från inkrement 3, där utveckling och kodgranskning delar utvecklarnas kapacitet. Modellregler och mätdefinitioner finns i [SIMULATION_MODEL.md](SIMULATION_MODEL.md).

## Syfte och bedömning

Simulatorn ska kunna förklara hur utvecklarkapacitet, testkapacitet, WIP, arbetsstorlek och beroenden påverkar leverans. Varje experiment har dels exakta kontroller som ska hålla, dels hypoteser som ska undersökas. En hypotes får motsägas av ett resultat; då ska resultatet kunna förklaras med statusförlopp, kapacitetsförbrukning och modellens regler. Vi ändrar inte motorn enbart för att få en önskad kurva.

Experimenten validerar den förenklade modellens beteende. De visar inte att den är kalibrerad för en verklig organisation. Ingen individuell produktivitet, självgranskningsregel, omarbetning eller kostnad för kontextbyten antas.

## Gemensamt referensscenario

Alla värden gäller om inte experimentet uttryckligen ändrar dem.

| Parameter | Värde |
|---|---:|
| Team | 1 |
| Utvecklare | 4 |
| Testare | 2 |
| Utvecklarkapacitet per person och dag | 1 |
| Testkapacitet per person och dag | 1 |
| WIP-gräns, alla aktiva statusar tillsammans | 8 |
| Antal arbetsobjekt | 60 |
| Size | 2 |
| Complexity | 1 |
| ReviewFactor | 0,5 |
| Prioritet / CreatedAt | 0 / 0 för samtliga objekt |
| Beroenden | Inga; DependencyProbability = 0 |
| Simuleringslängd | 60 dagar |
| Sprintlängd / releaseintervall | 10 / 10 dagar |
| Seed för förklarande enkelkörning | 42 |
| Seeds för upprepning | 42–141, samma seed för alla varianter i respektive par |

Granskningsfaktorn 0,5 är ett pedagogiskt experimentvärde, inte appens standardvärde 0,2 och inte ett skattat organisationsvärde. Varje normalstort objekt kräver 2 utvecklingsenheter, 1 granskningsenhet och 2 testenheter. Granskning får utvecklarkapacitet före utveckling. Varje statusövergång sker tidigast nästa dag, även om kapaciteten är stor.

Teoretiska kapacitetsgränser för identiska objekt är `utvecklare / 3` respektive `testare / 2` objekt per dag. De är övre gränser, inte prognoser: dagssteg, start- och sluteffekter, WIP, arbetsordning och beroenden kan sänka den faktiska genomströmningen. Antalet objekt är valt så att referensscenariot inte kan tömma hela backloggen inom horisonten; teststegets uppstart förhindrar det även om utvecklarkapaciteten ökas.

## Gemensamt genomförande och resultatunderlag

1. Kör varje variant med seed 42. Inspektera statusdiagrammet vid samma dagar och notera när köer uppstår.
2. Upprepa med seeds 42–141. Jämför varje variant med referensvarianten med samma seed. Ändra bara den faktor som experimentet anger.
3. Spara scenarioinställningar, modellversion, seed och resultat per körning. Detta är ett krav för den kommande experimentfunktionen; nuvarande UI har ännu ingen persistens/export.
4. Redovisa klara objekt och throughput tillsammans med medel-WIP, cycle time, lead time, utnyttjandegrader och ofärdigt arbete per status. Utvecklarutnyttjande ska delas upp i utveckling och granskning.
5. Redovisa skillnaden mot referensen per seed och medelskillnaden. När batchfördelningar införs ska även spridning visas; dagens UI visar enbart medelvärden.

Dagens A/B-funktion kan köra två resursvarianter åt gången. Flerpunktsserier kräver därför upprepade A/B-körningar tills experimentfunktionen byggts. Ändringar av storlekar eller beroendegrafer kan ännu inte jämföras i samma UI-par.

### Gemensamma exakta kontroller

- Samma scenario och seed ger samma resultat på samma .NET-/modellversion.
- Varje skapad post finns i exakt en status; statusantalen summerar här alltid till 60.
- WIP överskrider inte gränsen. DevelopmentWork + ReviewWork överskrider inte dagens utvecklarkapacitet; TestingWork överskrider inte dagens testkapacitet. Använd flyttalstolerans vid numeriska jämförelser.
- Ett objekt startar inte före sina beroendens CompletedAt. Inget objekt genomför flera statussteg på samma dag.
- Throughput = antal Done / 60, oavsett sprint- och releasegränser.
- Noll klara objekt innebär att lead/cycle time inte kan bedömas. Motorns värde 0 ska förklaras som denna konvention, inte som snabb leverans.

## R1 – Vad händer när antalet utvecklare förändras?

**Ändra:** antal utvecklare till 0, 1, 2, 4 och 8. Referensvariant: 4. Alla övriga parametrar är gemensamma, inklusive testkapacitet och granskningsfaktor.

**Hypotes:** med få utvecklare begränsar den gemensamma utvecklings-/granskningspoolen leveransen. När utvecklarkapaciteten blir tillräcklig begränsar teststeget flödet, och fler utvecklare ger mindre effekt. Platåns placering ska undersökas, inte hårdkodas.

**Läs av:** throughput mot antal utvecklare, Testing-antal över tid, utvecklings-/granskningsandelar, testutnyttjande och ofärdiga objekt. Ofullständigt arbete kan förbruka kapacitet utan att bidra till throughput.

**Exakta kontroller:** 0 utvecklare ger 0 Done och 0 förbrukat utvecklings-/granskningsarbete. För varje variant gäller `Done × 3 ≤ utvecklare × 60` och `Done × 2 ≤ 2 × 60`.

**Rimlighetsbedömning:** en eventuell förbättring, platå eller enskild försämring ska gå att förklara i dagsförloppet. Kräv inte strikt ökande throughput för varje seed; den dagliga arbetsordningen och ändrad WIP-beläggning kan påverka en ändlig körning.

**Stöd i dag:** går att köra i UI med upprepade A/B-par. Det finns ingen färdig parameterkurva.

## R2 – Vad händer när testkapaciteten är lägre?

**Ändra:** antal testare till 0, 1, 2 och 4; kapacitet per testare förblir 1. Referensvariant: 2. Utvecklarantalet är alltid 4.

Jämför kapaciteterna i relation till arbetskravet. Referensens utvecklarpool klarar högst 4/3 objekt per dag medan testpoolen klarar högst 1. Det räcker inte att jämföra råa arbetsenheter mellan stegen, eftersom ett objekt kräver olika mycket arbete i dem.

**Hypotes:** vid låg testkapacitet samlas arbete i Testing. När WIP fylls begränsas nytt intag, vilket även kan ge outnyttjad utvecklarkapacitet. Ökad testkapacitet bör minska denna begränsning tills utveckling/granskning eller en annan regel blir flaskhals.

**Läs av:** Testing-antal och WIP över tid, testutnyttjande, utvecklarutnyttjande, throughput och hur många objekt som återstår i Testing vid dag 60. Testing-antalet omfattar både väntande objekt och objekt som får testarbete.

**Exakta kontroller:** 0 testare ger 0 Done, 0 testarbete och 0 testutnyttjande. Arbete som nått Testing stannar där. Aktivt WIP förblir högst 8. För positiva antal testare gäller `Done × 2 ≤ testare × 60`.

**Rimlighetsbedömning:** en lägre utvecklarutnyttjandegrad kan vara en följd av testflaskhalsen och ska inte tolkas som brist på backlog. Högre utnyttjandegrad är inte i sig ett mål.

**Stöd i dag:** går att köra i UI med upprepade A/B-par.

## R3 – Vad händer när WIP begränsas?

**Ändra:** WIP till 1, 2, 4, 8 och 32. Referensvariant: 8. Behåll samma 60 objekt och all resurskapacitet.

**Hypotes:** låg WIP kan förhindra att utveckling, granskning och testning arbetar med olika objekt under samma dag. När WIP är tillräcklig kan ytterligare påbörjade objekt ge liten throughputvinst men längre tid i aktivt arbete. Modellen saknar kontextbyteskostnad; vi förväntar oss därför inte automatiskt att hög WIP sänker produktiviteten.

**Läs av:** throughput och cycle time sida vid sida med medel-WIP, köernas statusfördelning och utnyttjandegrader. Redovisa även lead time och ofärdigt arbete.

**Exakt, handräkningsbar kontroll:** med WIP 1 kräver varje objekt en utvecklingsdag, en granskningsdag och en testdag med dessa inställningar. Nästa objekt tas in först följande dag. Därför blir exakt 20 objekt klara på 60 dagar, throughput 1/3 och genomsnittlig cycle time 3 dagar. Medel-WIP är 1. Detta är härlett ur reglerna, inte ett rapporterat körresultat.

**Rimlighetsbedömning:** lägre cycle time kan bero på att mer väntan flyttats till Backlog. Alla objekt skapas dag 0, så lägre cycle time betyder inte nödvändigtvis lägre lead time. Kräv ingen universell optimal WIP och ingen strikt monotoni för enskilda seeds.

**Stöd i dag:** går att köra i UI med upprepade A/B-par.

## R4 – Vad händer med olika stora arbetsobjekt?

**Ändra endast storleksfördelningen:**

| Variant | Storlekar | Antal | Summa Size |
|---|---|---:|---:|
| A: jämnstora | Alla har Size 2 | 60 | 120 |
| B: blandade | Vart fjärde ID har Size 5, övriga Size 1 | 15 stora + 45 små | 120 |

Behåll Complexity 1, ReviewFactor 0,5 och alla övriga referensparametrar. Samma ID:n, titlar, skapandetider, prioriteter och seeds används. Det finns inga beroenden. Storlekarna är avsiktligt olika; påstå därför inte att arbetsobjekten är identiska i detta experiment. Fördelningen över ID är fast och behöver ingen ytterligare slumpgenerator.

Båda varianterna kräver totalt 120 utvecklingsenheter, 60 granskningsenheter och 120 testenheter. En jämförelse där antalet små objekt ökar utan kontroll på total arbetsmängd besvarar en annan fråga.

**Hypotes:** blandade storlekar ändrar leveransmönstret och kan ge andra väntetider. Stora objekt kan uppta WIP längre, men arbetsordningen spelar också roll. Motorn kan fördela arbete på flera objekt över flera dagar och modellerar inte att en utvecklare är låst till ett objekt.

**Läs av:** antal Done, ofärdiga objekt per storleksklass, lead/cycle time per storleksklass och kumulativ levererad arbetsmängd. Undvik att dra slutsatser enbart från antal klara objekt.

**Nytt mått som behövs:** `DeliveredDevelopmentWork = Σ(Size × Complexity)` för Done-objekt; arbetsmängdsgenomströmning = detta värde / 60. Det är ett mått på slutförd modellerad arbetsmängd, inte affärsvärde. Delvis utfört arbete och review/test räknas inte som extra levererad arbetsmängd. Visa gärna motsvarande återstående arbetsmängd för ej Done-objekt, tydligt skild från redan förbrukad kapacitet.

**Exakta kontroller:** båda varianterna har 60 objekt och samma totala arbete i varje steg. Levererad arbetsmängd ligger mellan 0 och 120. Levererad arbetsmängd plus arbetsmängden för ej Done-objekt är 120. Antal Done kan skilja utan att den levererade arbetsmängden skiljer lika mycket.

**Rimlighetsbedömning:** ingen förutbestämd vinnare. Resultatet ska visa vilka storlekar som levererats och vilka som återstår. En högre genomströmning i antal objekt bevisar inte högre arbetsmängdsgenomströmning.

**Stöd i dag:** Core kan köras med dessa listor. UI-generatorn skapar ännu bara identiska storlekar och A/B delar samma arbetsobjekt. Storleksprofiler, jämförelsen och de nya måtten behöver byggas.

## R5 – Vad händer när ärenden är beroende av varandra?

**Ändra endast beroendegrafen:** använd samma 60 normalstora objekt som i referensen.

| Variant | Exakta beroenden |
|---|---|
| A: oberoende, referens | Inga |
| B: en kedja | ID 2 beror på 1, ID 3 på 2, …, ID 60 på 59 |
| C: gemensam föregångare | Varje ID 2–60 beror på ID 1 |

Alla andra värden inklusive WIP 8, prioritet 0 och seeds är lika. Grafen ska anges uttryckligen. `DependencyProbability = 1` i dagens generator skapar en slumpmässig acyklisk graf, inte nödvändigtvis kedjan eller den gemensamma föregångaren ovan.

**Hypotes:** en lång kedja begränsar parallelliteten även när teamet har ledig kapacitet. En gemensam föregångare blockerar många objekt i början, men öppnar för parallellt arbete när den är Done. Samma antal beroenden kan alltså ge olika flöde: B och C har båda 59 kanter.

**Läs av:** throughput, blockerad tid, ofärdig beroendeblockerad backlog, WIP och resursutnyttjande över tiden. Skilj blockering vid dagsstart från kvarvarande blockering vid slutet. Låg WIP kan vara följden av att få objekt är startbara, inte av en för låg WIP-gräns.

**Exakt kontroll för kedjan:** bara ett objekt kan vara aktivt åt gången. Med referenskapaciteten blir ett objekt klart var tredje dag: exakt 20 Done vid dag 60 och throughput 1/3. Alla efterföljare startar tidigast vid föregångarens CompletedAt. Detta är en härledd kontroll, inte ett uppmätt resultat.

**Exakt kontroll för gemensam föregångare:** ID 2–60 kan inte starta under dag 0–2. ID 1 är Done vid tidpunkt 3; från dag 3 är samtliga återstående objekt fria från beroendeblockering, även om de kan vänta på WIP-plats. I den oberoende varianten är beroendeblockeringen alltid noll.

**Rimlighetsbedömning:** diagrammet ska förklara skillnaden mellan den långvarigt begränsade kedjan och den tillfälliga blockeringen bakom en gemensam föregångare. Generalisera inte till att varje extra beroende alltid minskar observerad throughput i varje ändlig körning.

**Stöd i dag:** Core stödjer explicita grafer och flera föregångare. Val av dessa mönster och jämförelse mellan grafer saknas i UI/Application.

## När är kärnan tillräckligt verifierad?

Kärnan har en dokumenterad beteendegrund när:

- samtliga exakta kontroller ovan har automatiserade tester,
- samtliga fem experiment kan återskapas med namngivna inställningar och seeds,
- en exempelrapport för varje experiment visar mätetal, statusförlopp och ofärdigt arbete,
- resultat kan förklaras även när en hypotes inte håller,
- begränsningarna kring ändlig backlog, dagssteg, poolad kapacitet och mått på endast avslutade objekt framgår.

Nuvarande 48 xUnit-tester täcker redan många grundregler men utgör inte en genomförd verifiering av hela denna experimentserie. Godkända regeltester är heller ingen garanti för att modellen beskriver en verklig organisation.

## Föreslagen genomförandeordning

1. Gör R1–R3 till namngivna referensscenarier och lägg till de handräkningsbara kontrollerna. Dokumentera körresultaten utan att ändra motorreglerna.
2. Inför explicit storleksprofil och mått på slutförd arbetsmängd för R4. Håll total mängd arbete och antal objekt konstanta.
3. Inför valbara beroendemönster och jämförelser mellan grafer för R5. Återanvänd motorns befintliga beroenderegler.
4. Samla resultaten i en experimentvy med parameterkurvor och parade jämförelser. Utöka därefter batchredovisningen med spridning.

Detta är en prioriterad specifikation. Ingen ny experimentvy, arbetsprofil eller ytterligare motorregel införs genom detta dokument.
