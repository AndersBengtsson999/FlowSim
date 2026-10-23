# Documentation verification — B6.5.3

The implementation is authoritative. No external chapter drafts were available or claimed to be reviewed. The eight chapters were written from the requirements and the repository, with model version 0.10. Production Core, Application and Infrastructure logic is unchanged.

## Audited behavior and significant findings

| Area | Verified behavior / corrected assumption | Source |
| --- | --- | --- |
| Stage transitions | All admissions precede daily work. Stage completions normally await next admission; release can include testing completed at the same boundary. | Simulation.Core/SimulationEngine.cs, Defects.cs, WorkItem.cs |
| Capacity | Code Review, Rework, debt repayment, then Development use the shared developer pool. Testing has its own pool. Admitted work need not receive capacity. | Simulation.Core/SimulationEngine.cs, Models.cs |
| Productivity | Effective work differs from raw consumed capacity. Stage defaults are 1. Rework is not multiplied by a stage productivity setting. | Simulation.Core/StageProductivity.cs, SimulationEngine.cs |
| WIP | Stage limits count only active states. Total WIP includes started waiting work and Ready for Release. Increasing a limit does not create capacity. | Simulation.Core/WipPolicy.cs, Models.cs |
| Specialists | Subset of developers, not additional staff. Remaining Development pool splits proportionally; specialist capacity can help general work. | Simulation.Core/Skills.cs, SimulationEngine.cs |
| Collaboration | Development only, primary then second contribution; second contribution has 50% effective productivity. Eligible headcount constraints remain. | Simulation.Core/SimulationEngine.cs |
| Rework | Review/testing defects route through Rework and back to review. Separate discovery/rework random streams; monetary rework is already included in staffing cost. | Simulation.Core/Defects.cs, RoleCosts.cs |
| Debt | Start-time effort plan, debt created on Development completion, repayment from remaining developer capacity. Debt amount is not money. | Simulation.Core/TechnicalDebt.cs |
| Dependencies | Backlog admission waits; explicit dependencies require work completion, not customer launch. Residual delay sampled on arrival. | Simulation.Core/ResidualDependencies.cs, SimulationEngine.cs, SimulationSession.cs |
| Internal release | Flow-based daily or scheduled absolute multiples of interval; oldest ready first; integer capacity, no developer/tester charge. Unlimited is a UI representation of maximum integer. | Simulation.Core/Release.cs, SimulationEngine.cs |
| Customer launch | One active version. Manual needs released work unless resolving a pending target. LIVE stops at target; unattended Run resolves automatically. | Simulation.Core/ProductDelivery.cs, SimulationSession.cs |
| Recurring launches | Next target uses actual launch day. Postpone is relative to today and changes only current target; one-time successor becomes Manual. | Simulation.Core/ProductDelivery.cs |
| Intervention timing | Day N belongs to Before; new settings affect N+1. Day 100 / 20 days means 81–100 and 101–120 inclusive. Partial observations are not filled. | Simulation.Application/LivePerformance.cs, LiveSimulation.cs |
| Determinism | Snapshot/checkpoint capture stores random-stream state. Different interventions can change later random consumption; same seed does not imply paired events after policy divergence. | Simulation.Core/SimulationSession.cs, Defects.cs; Simulation.Infrastructure/LiveSessionJson.cs |
| All 24 KPIs | Definitions, units, cohorts, sums, ratios, snapshots, P85, regression slope and missing-data semantics checked against implementation. | Simulation.Application/KpiDashboard.cs, KpiDashboard.Explorer.cs, LivePerformance.cs, TeamEconomics.cs |
| Windows | 10/20/40 rolling evaluations; preceding comparison requires two full windows. Explorer history up to 120 days differs from evaluation window. Unlaunched is a balance. | Simulation.Application/KpiDashboard.cs; Simulation.UI/ViewModels/LiveViewModel.Kpis.cs |
| Cost defaults | Developer 85,000, tester 75,000, specialist 100,000 SEK/month; 20 working days. Full-availability 5 dev + 2 test = 28,750 SEK/day. Screenshot settings differ. | Simulation.Core/RoleCosts.cs, Models.cs |
| Cost allocation | Daily pool allocation uses raw capacity with bounded denominators. WIP + awaiting + launched + overhead reconciles to incurred. Launch reclassifies, not new expense/revenue. | Simulation.Core/LaunchEconomics.cs |
| Workshop pressure | Four observations / three capacity observations after latest intervention; growth alone is not a proven bottleneck. | Simulation.UI/ViewModels/QueuePressure.cs |
| UI controls | Global, workshop, inspection, KPI and product controls inspected directly. Original global popover guidance retained in chapters 1 and 3. | Simulation.UI/Views/MainWindow.axaml, GameView.axaml, LiveView.axaml, KpiDashboardView.axaml, ProductLaunchView.axaml |

Paths in the table are relative to src/. No source was modified to match the guide.

## Architecture and offline behavior

- docs/user-guide/registry.json centrally records 73 topic IDs, source chapters, titles, keywords, related IDs and all 24 metric mappings. Markdown headings carry matching explicit IDs.
- GuideCatalog loads embedded resources, validates identities and links, and extracts section bodies through the Markdown AST source spans. Navigation never searches rendered text or relies on fixed offsets.
- GuideNavigation maintains back/forward topic history; GuideService owns one non-modal window and has no model/session reference. Unknown topic IDs fall back to Getting Started; unknown metrics to the KPI chapter; unknown stations to inspection guidance.
- GuideWindow provides independent chapter/content/context scrollers, search, chapter selector at narrow widths, active-topic indicator and keyboard actions. Contextual help leaves station and KPI selections intact.
- GuideMarkdown renders headings, paragraphs, emphasis, lists, tables, inline code, formulas as text, internal links and illustrations using native Avalonia controls. It uses [Markdig 1.3.2](https://www.nuget.org/packages/Markdig), licensed BSD-2-Clause. There is no embedded browser, HTML execution, remote image fetch or runtime network requirement.
- Markdown, registry and images are embedded in Simulation.UI.dll, including published builds. The renderer provides descriptive text if an optional image cannot be loaded; strict asset integrity is checked separately in tests.

## Illustrations and provenance

Three unchanged real B6.5.2 application screenshots: LIVE overview, selected testing queue/workshop, and KPI Explorer. Sources are in docs/verification/game-view-b6-5-2/screenshots. The guide copies them into assets/images and overlays numbered markers from annotations.json at render time. Originals are not edited; marker placement scales with the actual image rectangle.

Four explanatory diagrams are generated from audited relationships: workflow, internal release/customer launch, economic reconciliation, and Day 100 comparison boundaries. assets/create_diagrams.py reproduces them using Pillow; it is a development tool, not a runtime requirement. They are labelled as diagrams, not UI captures or observed experimental outcomes. No requested image references remain placeholders.

## Verification evidence and limitations

The documentation tests check eight chapters, all headings/registry references, internal and related links, every KPI and real station mapping, required embedded images, search ranking/case/body/glossary/partial terms, history/fallbacks and resource path restrictions.

The native production-view harness in docs/verification/game-view-b6-5-3 checks global/Game View/station/KPI/product entry points, single-window reuse, all chapter renders, wide/medium/narrow layouts, search/empty results, close/reopen, simulation snapshot equality and continued playback. It also repeats prior LIVE/KPI/launch/inspection/Flow Board acceptance checks.

Build, full-suite, native and publish results are recorded with the verification artifacts. Physical keyboard/screen-reader/trackpad usability still needs human acceptance; automated focus and layout checks cannot establish a complete accessibility audit. Screenshot values illustrate a captured session and must not be read as default model values. The guide describes a learning model, not financial or operational forecasts.
