# Review: items 2, 3 and 4 (27 October 2026)

Everything below is built and proven locally. **Nothing is committed or pushed.** It stops before the first PMS service, as asked.

## Proof

| Run | Result |
|---|---|
| Full regression (33 suites + browser gate) | all green (FINAL + FINALB + FINALC in Harmony.TestEnv\s35) |
| s59 settings | 11/11 |
| s60 messaging | 5/5 |
| Browser gate s56 | 44/44 (hotel roles, settings page, console registry) |
| Framework tests | Identity 15/15, BuildingBlocks 8/8 |
| Frontend | check 0, tests 208 + 2, both apps build, size-report ok |

## What each item gives

- **Item 2, roles per hotel or group.** A role at one hotel or one group, temporary grants, the hotel wall on every call, the web app naming the hotel on every call, the person page assigning both.
- **Item 3, settings.** Services register keys (console: Reference data, Settings registry). A customer sets values for the whole chain, a group or one hotel. A hotel's value wins, then the highest-precedence group, then the customer, then the default. A tie between groups is a conflict, never a guess. The hotel Settings page shows the trace. Every service reads its settings through `ISettingsReader` from a Redis copy.
- **Item 4, events.** Identity tells other services about tenants, hotels and settings on RabbitMQ (exchange `harmony.identity`, types 1001-1301). A publish that fails during an outage waits in the outbox and is delivered when RabbitMQ is back.

## Decisions for you

1. **Item 4 follows "use the framework as is".** The framework publishes after the commit from memory. A crash between the commit and the publish loses that event. The ERD wants the event written in the same transaction (OutboxMessages). Keep, or build the stricter outbox before the PMS?
2. **RabbitMQ is new local infra** (`infra/rabbitmq`, image `rabbitmq:4-management`). It is the framework's only bus, and Identity's own note expected it. Confirm.
3. **With messaging on, Identity needs RabbitMQ to start.** It fails fast and starts once RabbitMQ is up. That is the framework's behaviour.
4. **Messaging is off unless the settings carry `EventBusSettings`.** Production refuses to start without it. Your Development settings do not turn it on yet. Turn it on?
5. **I fixed three bugs in the framework's SQL Server outbox installer.** It could never have run on SQL Server. I did not fix the virtual-host check, which crashes when RabbitMQ is down and creates hosts without rights. We use the default host "/" instead.
6. **Identity never parses a setting value**, as the design says. The owning service checks it.
7. **A hotel manager can set their own hotel's settings only.** A group or whole-customer value needs ManageSettings from a customer-wide role.
8. **Existing customers get the two new permissions through the migration's backfill.** Their Administrator role gains them too.
9. **The Arabic web build is 399.7 kB**, just under the 400 kB warning line.
10. **Item 2 counts a group membership as current when it has no end.** Settings use the dated period. Align item 2 to the dated rule?

## Your steps

1. Read and say "commit". Order: framework first (its pack becomes the next feed version), then Harmony.
2. `Update-Database` for the migration `ConfigSettingsAndValues`.
3. To try messaging locally: `docker compose -f infra/rabbitmq/docker-compose.yml up -d`, then add `EventBusSettings` and `OutboxSettings` (see Harmony.TestEnv\s60\tests\e2e\messaging-e2e.ps1 for the shape).

## Next

The first PMS service plan, together.
