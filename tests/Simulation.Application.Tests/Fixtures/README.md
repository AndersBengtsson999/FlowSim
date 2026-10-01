# Model 0.2 compatibility fixtures

These files were generated with the original model 0.2 Release binaries before rebuilding the changed implementation, not by editing current JSON to remove properties.

Configuration: LiveSimulation.Demo, Development Triangular(1,3,7), defects enabled with review/testing probabilities 0.2. Advance 10 days, create checkpoint "Legacy checkpoint", change Team to (4 developers, 3 testers) with label "Legacy testers", advance 5 days and save Live. Continue the original model another 15 days and serialize its SimulationResult at Day 30. The scenario file contains the original request.

AvailabilitySupplyTests loads the saved Day 15 session and compares continuation with the original Day 30 result, normalizing only SimulationModelVersion. It also verifies missing availability defaults and original-model provenance across resaving.
