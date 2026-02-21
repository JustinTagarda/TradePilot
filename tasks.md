# TradePilot — Project Tasks & Implementation Guide

Version: 0.1
Project Root: D:\Projects\TradePilot
Primary Language: C# (.NET)
IDE: VS Code
Assistant: Codex

This document is the **single source of truth** for the project so new Codex sessions can resume work without losing context.

Codex must:
1. Read this file first.
2. Follow tasks in order.
3. Update task status when completed.
4. Never remove context sections from this file.
5. Only append updates or mark tasks complete.

---------------------------------------------------------------------

# 1. Project Overview

TradePilot is a **read-only MetaTrader 5 companion dashboard** built with .NET.

The system reads trading data from a running MT5 terminal and presents it in a clean web dashboard for monitoring and analytics.

The application is broker-agnostic and will work with MT5 demo accounts.  
No broker APIs are required.

Primary goal:
Showcase strong .NET engineering skills and architecture.

Key abilities of the system:

• Display MT5 account summary
• Display open positions
• Display pending orders
• Show updates in near real time
• Allow remote viewing via web browser
• Run locally with zero infrastructure cost
• Be cloud deployable later

Initial version is **read-only**.

No trading actions are performed.

---------------------------------------------------------------------

# 2. System Architecture

MT5 Terminal (Windows)
        │
        │ HTTP (localhost)
        ▼
Local Connector (ASP.NET Core Minimal API)
        │
        │ Secure HTTP (HMAC signed)
        ▼
Cloud API (ASP.NET Core)
        │
        ▼
Web UI (Blazor WebAssembly)

---------------------------------------------------------------------

# 3. Component Responsibilities

## MT5 Expert Advisor (EA)

Runs inside MetaTrader 5.

Responsibilities:

• Read account information
• Read open positions
• Read pending orders
• Create JSON snapshot
• Send snapshot to local connector

Communication method:
HTTP POST using WebRequest.

Frequency:
Every 1–2 seconds.

---------------------------------------------------------------------

## Local Connector

Runs on same machine as MT5.

Responsibilities:

• Receive snapshot from EA
• Validate signature
• Forward snapshot to Cloud API
• Provide health endpoint
• Log errors

Technology:
ASP.NET Core Minimal API

---------------------------------------------------------------------

## Cloud API

Central backend service.

Responsibilities:

• Receive snapshots
• Validate HMAC signature
• Store latest snapshot
• Provide API for UI
• Allow multiple MT5 sources

Endpoints:

GET /health

POST /v1/mt/snapshots

GET /v1/mt/sources

GET /v1/mt/sources/{sourceId}/latest

---------------------------------------------------------------------

## Web Dashboard

User interface.

Technology:
Blazor WebAssembly

Features:

Sources page  
Dashboard page

Dashboard displays:

Account
Positions
Orders
Last update time

Initially uses polling every 2 seconds.

SignalR may be added later.

---------------------------------------------------------------------

# 4. Technology Stack

.NET 8+

ASP.NET Core

Blazor WebAssembly

Minimal APIs

Serilog logging

xUnit tests

EF Core (added later)

SQLite (added later)

OpenAPI / Swagger

---------------------------------------------------------------------

# 5. Repository Structure

src/
    TradePilot.Shared
    TradePilot.Api
    TradePilot.Web
    TradePilot.Connector
    TradePilot.Tests

mt5/
    TradePilotEA.mq5

docs/
    roadmap.md
    architecture.md
    setup.md
    demo.md

tasks.md (this file)

---------------------------------------------------------------------

# 6. Data Contracts

Snapshot DTO

MtSnapshot
sourceId
timestampUtc
account
positions[]
orders[]

Account DTO

MtAccount
broker
server
login
currency
balance
equity
margin
freeMargin
marginLevel

Position DTO

MtPosition
ticket
symbol
side
volume
openPrice
stopLoss
takeProfit
currentPrice
profit

Order DTO

MtOrder
ticket
symbol
type
volume
price
stopLoss
takeProfit
timeUtc

---------------------------------------------------------------------

# 7. Security Model

All data transfers use HMAC signatures.

Trust boundaries:

MT5 EA -> Local Connector (per source secret)
Local Connector -> Cloud API (connector secret)

Headers:

X-Source-Id  
X-Timestamp  
X-Nonce  
X-Signature  

Signature:

HMACSHA256(secret, `${timestamp}.${nonce}.${body}`)

Rules:

X-Timestamp must be UTC Unix time (seconds).

Reject request if timestamp drift > 5 minutes.

Reject reused nonce (scoped by sourceId) within the allowed drift window.

Use constant-time comparison.

Connector must re-sign forwarded payloads with a new timestamp and nonce.

Do not forward inbound signatures to the next hop.

Secrets stored in configuration/environment variables.

---------------------------------------------------------------------

# 8. Development Phases

Phase 1
Foundation

Create solution
Create projects
Define DTOs
Basic API

Phase 2
Connector

Local connector service
Forward snapshots
Logging

Phase 3
Web UI

Sources page
Dashboard page
Polling updates

Phase 4
MT5 Integration

EA sends snapshots
End-to-end flow works

Phase 5
Persistence

Add SQLite
Historical snapshots

Phase 6
Realtime

Add SignalR

Phase 7
Cloud Deployment

Azure ready
Environment configs

Scope note:
Task list 001-015 is the MVP path (Phases 1-4).
Phases 5-7 are tracked in a post-MVP backlog section.

---------------------------------------------------------------------

# 9. Task Rules for Codex

Before starting a task:

1. Read this file completely.
2. Find the first task with status = TODO.
3. Implement only that task.
4. When complete:
   Change status to DONE.
5. Do NOT skip tasks.
6. Do NOT rewrite earlier sections.

---------------------------------------------------------------------

# 10. Task List

### TASK 001
Create solution and base folder structure.

Status: DONE

Expected result:

TradePilot.sln

src folder

project placeholders

---------------------------------------------------------------------

### TASK 002
Create Shared project.

Status: DONE

Includes:

DTO models

JSON serialization options

---------------------------------------------------------------------

### TASK 003
Create Cloud API project.

Status: DONE

Features:

Minimal API
Health endpoint
Snapshot ingest endpoint
Sources endpoint
Latest snapshot endpoint
Swagger

Endpoints in scope:

GET /health
POST /v1/mt/snapshots
GET /v1/mt/sources
GET /v1/mt/sources/{sourceId}/latest

---------------------------------------------------------------------

### TASK 004
Implement HMAC validation service.

Status: DONE

Includes:

Nonce protection
Signature validation
Timestamp drift validation
Constant-time signature comparison
Secret resolution from configuration

---------------------------------------------------------------------

### TASK 005
Create Snapshot store service.

Status: DONE

In-memory store keyed by sourceId.

Store latest snapshot per source on ingest.

Service methods required by read endpoints:

GET /v1/mt/sources
GET /v1/mt/sources/{sourceId}/latest

---------------------------------------------------------------------

### TASK 006
Create Connector project.

Status: DONE

Endpoints:

/health
/ingest/snapshot

Validate inbound HMAC from EA.

Forward snapshots to API.

Re-sign outbound requests with fresh timestamp and nonce.

Configuration required:

Cloud API base URL
Connector sourceId
Connector outbound secret
Inbound source secret map (or a shared inbound secret for MVP)

---------------------------------------------------------------------

### TASK 007
Add structured logging.

Status: DONE

Use Serilog.

---------------------------------------------------------------------

### TASK 008
Create Blazor Web project.

Status: DONE

Basic layout
API client service.

API client methods:

List sources
Get latest snapshot by sourceId
Handle empty/loading/error states

---------------------------------------------------------------------

### TASK 009
Implement Sources page.

Status: TODO

Calls API to list sources.

---------------------------------------------------------------------

### TASK 010
Implement Dashboard page.

Status: TODO

Display account
positions
orders

---------------------------------------------------------------------

### TASK 011
Add polling service.

Status: TODO

Refresh snapshot every 2 seconds.

---------------------------------------------------------------------

### TASK 012
Implement MT5 EA snapshot sender (MVP).

Status: TODO

Timer loop (1-2 seconds)
Read account/positions/orders from MT5
Build JSON snapshot contract
Sign request headers (sourceId/timestamp/nonce/signature)
POST to local connector
Basic retry/error logging

---------------------------------------------------------------------

### TASK 013
Write documentation.

Status: TODO

Architecture
Setup
Demo steps.
Security and configuration.
MT5 terminal WebRequest URL allowlist steps.

---------------------------------------------------------------------

### TASK 014
Add unit tests.

Status: TODO

HMAC tests
API tests.
Connector signature flow tests.
Nonce replay and timestamp drift tests.

---------------------------------------------------------------------

### TASK 015
End-to-end local test.

Status: TODO

MT5 → Connector → API → Web UI

Acceptance checks:

Signed snapshot reaches API through connector
Source list returns expected sourceId
Latest snapshot endpoint returns current account/positions/orders
Web dashboard reflects updates on polling interval

---------------------------------------------------------------------

# 11. Completion Checklist

The MVP is complete when:

• MT5 demo account sends snapshots
• Connector receives them
• API stores them
• Web UI displays them
• Remote viewer can see dashboard
• HMAC validation and nonce replay protection are verified

---------------------------------------------------------------------

# 12. Post-MVP Backlog

### BACKLOG 001 (Phase 5)
Persistence with SQLite and EF Core.

Status: BACKLOG

Includes:

Snapshot history table
Retention policy
Read endpoints for historical snapshots

---------------------------------------------------------------------

### BACKLOG 002 (Phase 6)
Realtime updates with SignalR.

Status: BACKLOG

Includes:

Hub for source updates
Web client subscription
Polling fallback when SignalR is unavailable

---------------------------------------------------------------------

### BACKLOG 003 (Phase 7)
Cloud deployment baseline.

Status: BACKLOG

Includes:

Environment-specific configuration
Secret management strategy
Azure deployment profile and runbook

---------------------------------------------------------------------

# End of File
