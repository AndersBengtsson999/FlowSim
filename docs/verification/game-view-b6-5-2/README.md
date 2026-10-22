# B6.5.2 — Compact LIVE KPIs and responsive workshop

## Cause and change

B6.5.1 reserved only the KPI heading below the workshop, while the cards still contained trends, comparisons and explanations. Its 420-DIP minimum workshop height could push those cards outside the page viewport.

The shared Team Performance component now displays only name, formatted current value and unit on each card. Five selected cards fit in one row at supported desktop widths; narrower widths wrap. Minimum text-row space accommodates ordinary changes in value/unit wrapping without jumping. Cards grow if exceptionally long content needs more space rather than clipping it. The section uses one small header with Analyze and KPI selection actions. Period selection is retained in detailed analysis rather than permanently occupying the compact section.

Clicking a card uses the existing non-modal KPI Explorer. Its details include definition, current value/unit, selected period, previous-period comparison, historical range/chart, and allocation components where applicable. Both comparison modes remain available. Selecting another card updates the existing Explorer window. Closing it retains the selected KPI and evaluation window. Native buttons provide keyboard activation and focus; compact cards add a pointer cursor and hover feedback.

The workshop reserves the measured height of the complete compact strip and uses the remaining page viewport, with the existing aspect-ratio camera. The former inline Explorer/sidebar layout is removed: detailed analysis does not resize the workshop. Small windows and below-workshop station inspection use scrolling as a fallback. No artwork, camera input policy, metrics, calculations, persistence or simulation mechanisms changed.

## Verification

Release build succeeded with zero warnings/errors. All 924 tests passed (Core 218, Application 295, UI 411), zero failures/skips. Native macOS verification passed.

- build.log: complete Release solution build.
- tests.log: complete automated test suite.
- native.log: production Avalonia view verification on macOS using the native software renderer.
- Program.cs and Verification.csproj: reproducible native harness, with optional --headless.

New focused tests cover five-card desktop widths, readable narrow widths, detail selection, evaluation-window preservation, LIVE updates, /1,000 SEK units and no simulation mutation. Existing fit, pan, queue selection, replay, economic allocation and KPI tests remain in the full suite.

Native acceptance checks the entire compact strip against the actual ancestor ScrollViewer viewport at requested sizes 1280x800, 1440x1000 and 1920x1080. macOS constrains actual window height to the available screen; actual dimensions are logged. Each card has exactly three TextBlocks and no chart. Clicking a real card opens the selected KPI, retains the evaluation window, preserves workshop dimensions and does not alter simulation state. Stepping LIVE preserves details and compact height. Closing Explorer retains the period. Additional checks cover station inspection, 980px fallback layout, save/load, three customer launches, contextual KPI navigation and Flow Board.

Screenshots:
- screenshots/compact-1280.png
- screenshots/compact-1440.png
- screenshots/compact-1920.png
- screenshots/selected-kpi-details.png
- screenshots/layout-980.png
- screenshots/flow-board.png

## Limitations

At small sizes, or with expanded product details / station inspection below the workshop, scrolling is intentional. Extremely long numbers or titles may grow a card to avoid clipping; ordinary LIVE values are checked for stable height. Physical macOS trackpad gestures still require human acceptance; the existing input policy is unchanged. The native harness activates controls programmatically, not through physical pointer/trackpad input.
