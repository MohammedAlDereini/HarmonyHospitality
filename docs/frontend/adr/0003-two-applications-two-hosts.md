# ADR 0003: Two applications on two hostnames with two BFF clients

- **Status:** Accepted — approved by the owner 2026-10-18 (D17)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Platform people manage tenants and never act inside a hotel (platform tenant rule, 2026-10-11). ASVS 5.0 3.5.4 asks for separate applications on separate hostnames. Today one BFF client exists (harmony-web-bff).

## Decision

`harmony-web` (hotel staff) and `harmony-console` (platform people) are separate Angular applications, each served by its own BFF deployment on its own hostname. The console BFF is the same code with its own Identity client `harmony-console-bff`, its own Data Protection application name and key-ring key, its own Redis key prefix and its own cookie name. Fitness check F5 forbids harmony-web from importing platform-admin and harmony-console from importing hotel domains.

## Consequences

A second BFF deployment and Identity client (backend work, B9). Platform reference-data screens move out of the hotel app (R16).

## Evidence

Architecture v2.1 §3, §11.4 B9, audit finding A4 (shared Data Protection names would let one host decrypt the other's tickets).

## How it is verified

F5; G8 sign-in on both hosts; a stolen web cookie presented to the console host is refused.
