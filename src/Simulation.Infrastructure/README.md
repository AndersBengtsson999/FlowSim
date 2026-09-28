# Simulation.Infrastructure

Step 9 adds versioned UTF-8 JSON configuration persistence (`ExperimentJson`) and comparison CSV export (`ComparisonCsv`). JSON supports explicit Fixed/Triangular effort objects and validates scenario/experiment inputs. Writes use a sibling temporary file and replacement. CSV retains numeric results, signed deltas, configuration snapshots and execution provenance.

Infrastructure references Application. Core has no dependency on this project, JSON or filesystem concerns. There is no database, autosave, migration or persisted result archive.
