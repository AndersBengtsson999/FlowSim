# Sprite artwork origin and use

The first six PNGs were newly generated for the Software Development Simulation sprite prototype with the built-in OpenAI `image_gen` tool, using the `imagegen` skill. They are AI-generated original assets, not downloaded stock art, emoji or an external game asset pack. The generated developer was used only as a style reference for the other five independent assets. No external artist's image, third-party logo or branded character was used as a reference.

| Local asset | Subject | Generated source filename |
| --- | --- | --- |
| developer.png | Developer with teal shirt and yellow lanyard | exec-f36eadfa-d0d8-473e-ad4d-be78ff0cefa2.png |
| desk.png | Empty oak desk with teal legs and yellow drawers | exec-30662bad-c5eb-4fc7-a6b4-157bf1ffa8db.png |
| computer.png | Monitor, keyboard and mouse equipment sprite | exec-7891ab3e-88db-4dc7-a1ca-119e6595d7a6.png |
| package.png | Cardboard work package with yellow tape | exec-d350c845-5019-45cd-85ca-7042e795ac15.png |
| plant.png | Green plant in a cream/teal pot | exec-e02b2d9d-c2f3-465b-89ce-965ace3e89ee.png |
| floor.png | Empty tiled floor section, no scenery baked into it | exec-25e188eb-7b83-4460-beda-52f11fafc3c6.png |

The original files were copied into this folder unchanged, preserving alpha and embedded provenance metadata. Original generation occurred during this task on 2026-10-12; integration and verification completed 2026-10-13. Exact prompts are saved in [prompts.json](../../../docs/verification/game-view-b1-1/prompts.json). Dimensions, alpha inspection and SHA-256 values are recorded in [assets-inspection.json](../../../docs/verification/game-view-b1-1/assets-inspection.json).

Usage-rights basis: OpenAI's Europe Terms of Use, updated January 16, 2026, assign Output rights to the user as between OpenAI and the user, to the extent permitted by law. They also state that outputs may not be unique. These files are supplied to the requesting project under that output-ownership basis; this note does not assert exclusivity or apply a third-party stock/Creative Commons license. See [OpenAI Europe Terms — Content](https://openai.com/policies/eu-terms-of-use/), checked 2026-10-13. The account's applicable service agreement governs use.

No generation service, API key, download URL or network connection is needed to load or display these assets at runtime.

## B1.2 additions — 2026-10-13

Three further original PNGs were generated with the same built-in image tool and the existing developer/desk as style references. They use the same output-rights basis described above. None is a complete workstation illustration.

| Local asset | Subject | Generated source filename |
| --- | --- | --- |
| developer-seated.png | Seated developer with typing arms, no chair or furniture | exec-52cefe06-ee3e-4c01-ab31-8ac89c0e1a54.png |
| developer-collaborating.png | Standing colleague pointing toward a screen, no partner or furniture | exec-326b7bc2-5eb1-43de-9667-79c0b76e979c.png |
| chair.png | Empty office chair, no person | exec-26480d26-ca6d-46d8-b278-bdc379942237.png |

The B1.2 assets also remain unchanged PNG copies. The upper-body rendering layer selects a source rectangle from `developer-seated.png`; it is not a separately generated asset. See [B1.2 prompts](../../../docs/verification/game-view-b1-2/prompts.json) and [B1.2 asset inspection](../../../docs/verification/game-view-b1-2/assets-inspection.json). The six B1.1 PNGs remain byte-for-byte unchanged.


## B1.3 integration — 2026-10-13

All assets and the reusable renderer now live in `src/Simulation.Sprites`. Both the LIVE UI and the isolated prototype reference this library. The nine B1.1/B1.2 PNGs were moved without altering their bytes.

Three additional original assets were generated with the built-in image tool (not the CLI), using the existing desk only as a style reference. The same output-ownership basis above applies. No complete scene or outside artwork was used.

| Local asset | Subject | Generated source filename |
| --- | --- | --- |
| shelf.png | Empty low storage shelf; packages remain separate | exec-43972acf-11ac-4173-a6b2-65329fb46b3c.png |
| delivery-van.png | Parked yellow/teal delivery vehicle, empty cargo space | exec-10ed6555-ce9f-4b23-b1a7-7f76a6a97300.png |
| maintenance-bench.png | Cabinet and attached tool board; no fictional debt tasks | exec-af8480c8-3d30-4f2f-909d-e5e40b85ae25.png |

Saved in this directory as unchanged PNGs with alpha and provenance metadata. See [exact B1.3 prompts](../../../docs/verification/game-view-b1-3/prompts.json) and [dimensions, transparency and hashes](../../../docs/verification/game-view-b1-3/assets-inspection.json). The van is a single reusable object; its doors and wheels are not independently animated layers. The original floor sprite remains available to the prototype; LIVE deliberately retains the connected B1 floor geometry.

## B2.3 gestures — 2026-10-14

`developer-gestures.png` is a new, original two-pose strip produced by the built-in `image_gen` tool using this project's existing `developer.png` as the edit reference. Source: `exec-f754de75-0e9e-4d32-b6c9-cdd6e6d73151.png`. It retains its generated RGBA pixels and provenance metadata without postprocessing. Neutral and pointing poses share one character identity. The output-use basis above applies; no third-party stock artwork was introduced. Exact prompt, dimensions, alpha and hash: [B2.3 provenance](../../../docs/verification/game-view-b2-3/artwork.json).

Both cells are 887 × 887 pixels in a 1774 × 887 strip. Neutral foot anchor: (490, 820); pointing foot anchor relative to its cell: (414, 820). The differing X anchors compensate for generated cell placement. PixelsToDip = 0.28. The scene applies its ordinary 0.34 character scale. Idle uses only the neutral frame. Collaboration/maintenance play neutral, neutral, neutral, pointing, pointing, neutral at 1.5 frames/second, driven by the shared presentation clock. This is a two-pose gesture, not a smooth skeletal animation. No runtime generation or network access is needed.

## B2.4 walking — 2026-10-14

`developer-walking.png` is original generated artwork from the built-in `image_gen` tool, using this project's original developer identity through two intermediate edits. Final source: `exec-7f1710f6-9043-4148-9adb-8da9041a2007.png`. The 1774 × 887 RGBA PNG is copied without pixel/alpha changes. It supplies four directions with contact/passing poses, sharing one decoded texture across four catalog aliases. The existing output-use basis applies; no third-party stock license is claimed. [Exact prompts, hash and limitations](../../../docs/verification/game-view-b2-4/artwork.json).

## B4.1 station identity additions — 2026-10-15

Three independent original RGBA sprites were generated with the built-in OpenAI image_gen tool using the imagegen skill and the existing project-owned desk.png as the sole style reference. They are not stock art, external icons or screenshots. The generated PNG files are copied unchanged, preserving transparency and provenance metadata. They use the same output-rights basis described above; no additional third-party asset license is asserted.

| Asset | Generated source | Use |
| --- | --- | --- |
| planning-board.png | exec-f51302e8-aff5-489d-ae4c-f26abc282168.png | Backlog planning board; decorative cards are not counted work items |
| review-board.png | exec-ccdde89b-ed28-4898-a126-8978f217e623.png | Review comparison/checklist display; decorative tick is not an item approval result |
| test-console.png | exec-e5514c22-9e42-4085-a9f2-54dea09806fb.png | Tabletop diagnostic equipment; waveform is decorative, not a metric |

All three are 1254×1254 RGBA, with alpha ranging from 0 to 255. Full image rectangles are registered in SpriteCatalog. Foot/contact anchors in source pixels: planning (630,1080), review (630,1090), console (620,930). PixelsToDip is .25; per-object scale positions them consistently with existing furniture. They load through the existing embedded-resource bitmap cache and require no network at runtime. Exact prompts: [B4.1 prompts](../../../docs/verification/game-view-b4-1/prompts.json).
