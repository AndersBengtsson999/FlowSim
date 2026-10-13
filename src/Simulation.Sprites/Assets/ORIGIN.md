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
