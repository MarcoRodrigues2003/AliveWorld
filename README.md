# AliveWorld — Job Board & Citizen Task Prototype (Unity)

Prototype framework for a small “town sim” loop:

- Local boards (Home / Work) publish **tickets** (jobs)
- Citizens evaluate nearby boards, **reserve** a ticket, and execute it
- Tickets support **scopes** (FamilyOnly / WorkplaceOnly / Public)
- Current job types: **Fetch** + timed job (**Clean**) as proof-of-concept

This repo is meant as a proof-of-concept for a course project and focuses on code structure and behavior rather than visuals.

## Features

- **Ticket system**
  - `BoardTicket` data model (priority + aging)
  - Ticket lifecycle: `Open → Reserved → InProgress → Done/Failed`
  - Scope rules:
    - Home: `FamilyOnly` + `Public`
    - Work: `WorkplaceOnly` + `Public`

- **Audits (job generation)**
  - `HomeAudit`: creates Fetch tickets when home inventory is low + optional Clean tickets
  - `WorkAudit`: creates Fetch tickets when workplace inventory is low

- **Citizen AI**
  - `CitizenJobSeeker`: chooses boards to visit, reads tickets, scores them, reserves one
  - `CitizenTicketExecutor`: executes the reserved ticket (generic routine system)

- **World resources**
  - `ResourceProviderDirectory` + `ResourceProvider` for fetching Water/Food/Fuel, etc.

## IDs (how ownership & eligibility works)

This project uses a few simple integer IDs to control who can take which tickets:

### CitizenIdentity fields
- `citizenId`
  - Unique ID for each citizen (used to reserve/execute tickets).
- `familyId`
  - The household/family the citizen belongs to.
- `workplaceId`
  - The workplace the citizen is assigned to.
  - `0` means “unemployed / no workplace”.

### Board ownership IDs
- `HomeBoard.familyId`
  - The home belongs to a specific family.
- `WorkBoard.workplaceId`
  - The workplace belongs to a specific workplace group.

### Ticket scopes
- `FamilyOnly`
  - Only citizens with `CitizenIdentity.familyId == HomeBoard.familyId` should take it.
- `WorkplaceOnly`
  - Only citizens with `CitizenIdentity.workplaceId == WorkBoard.workplaceId` should take it.
- `Public`
  - Any citizen may take it (used for “emergency/public service” style tickets).

### Example setup
- Home_1 has `HomeBoard.familyId = 1`
- Work_1 has `WorkBoard.workplaceId = 1`
- Citizen_1 has `CitizenIdentity: citizenId=1, familyId=1, workplaceId=1`

Result:
- Citizen_1 can take **FamilyOnly** tickets from Home_1
- Citizen_1 can take **WorkplaceOnly** tickets from Work_1
- Citizen_1 can take **Public** tickets from anywhere (if within range)

## How to run (minimal scene)

1. Create a floor and bake a NavMesh (AI Navigation / NavMeshSurface).
2. Add a `SimClock` in the scene.
3. Add a `ResourceProviderDirectory` and at least one `ResourceProvider` (e.g., Water).
4. Create at least one Home:
   - `HomeBoard`, `HomeInventory`, `HomeAudit`, `HomeBoardTickDriver`
5. Create at least one Workplace:
   - `WorkBoard`, `WorkInventory`, `WorkAudit`, `WorkBoardTickDriver`
6. Create a Citizen with:
   - `NavMeshAgent`, `CitizenNavMover`, `CitizenIdentity`, `CitizenJobMemory`,
     `CitizenJobSeeker`, `CitizenTicketExecutor`
7. Press Play and inspect boards/inventories to see tickets being created and completed.

## Project structure (main scripts)

- `Assets/Scripts/Citizen/`
  - `CitizenJobSeeker`, `CitizenTicketExecutor`, `CitizenNavMover`, `CitizenIdentity`, `CitizenJobMemory`
- `Assets/Scripts/Home/`
  - `HomeBoard`, `HomeInventory`, `HomeAudit`, `HomeBoardTickDriver`
- `Assets/Scripts/Work/`
  - `WorkBoard`, `WorkInventory`, `WorkAudit`, `WorkBoardTickDriver`
- `Assets/Scripts/World/`
  - `ResourceProvider`, `ResourceProviderDirectory`, `WorldBlackboard` (optional multipliers)
- `Assets/Scripts/Core/`
  - Ticket definitions + `SimClock`

## Notes / limitations

- This is a prototype. Some behaviors are simplified:
  - The timed job type (**Clean**) currently completes at the board location.
  - No scene “target objects” are wired yet (repairs, cooking stations, etc.).
- The system is designed to be extended with more ticket kinds and richer execution steps.

---

## Permissions

All rights reserved.

If you want to use this code/framework in your own project, contact **Marco Rodrigues** for permission.
