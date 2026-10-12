# 🚀 Enterprise Firebase Push Notification Connector (.NET 8)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Firebase Admin SDK](https://img.shields.io/badge/FCM-HTTP_v1-FFCA28?logo=firebase&logoColor=black)](https://firebase.google.com/)
[![Redis](https://img.shields.io/badge/Redis-7.0_Alpine-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Podman](https://img.shields.io/badge/Container-Podman_%7C_Docker-892CA0?logo=podman&logoColor=white)](https://podman.io/)
[![Architecture Decision Records](https://img.shields.io/badge/ADR-Documented-22c55e)](#-architecture-decision-records-adr)

An enterprise-grade, high-throughput Firebase Cloud Messaging (FCM) gateway built with ASP.NET Core (.NET 8) and Redis 7. Features **active presence filtering**, **dynamic multi-tenant routing** across separate Firebase projects, **Apple APNs HTTP/2 protocol bridge**, and support for **all 6 FCM delivery types**.

---

## 🌟 Key Capabilities

* **⚡ Modern FCM HTTP v1 Protocol:** Replaces deprecated legacy server keys with short-lived OAuth 2.0 Google Service Account authentication and automated token caching via `FirebaseAdmin 3.1.0`.
* **💓 Redis Sliding TTL Presence Window:** Devices maintain active status via a 45-second heartbeat renewing a 90-second sliding TTL in Redis. Prevents battery drain and phantom notification spam on idle devices.
* **🏢 Dynamic Multi-Tenant Routing:** Single backend cluster routes dispatches across multiple separate Firebase projects (e.g. `CustomerApp`, `DriverApp`, `MerchantApp`) via named `FirebaseApp` instances and `appId`.
* **📬 Unified Delivery Engine:** A single composable endpoint (`POST /api/notifications/send`) supporting:
  * 🔔 **Visible Alerts** (Lock screen banner, ringtone, badge)
  * 🔕 **Silent Push** (Background data sync with `content-available: 1`)
  * 🖼️ **Rich Media** (High-res expandable image banners)
  * 🔘 **Interactive Buttons** (Action categories with inline "Accept" / "Decline")
  * ⏱️ **Collapsible Live ETA** (Replaces existing notification banners in-place)
  * 🚨 **Critical / Time-Sensitive** (Bypasses iOS Focus and Android DND)
  * 📡 **Pub/Sub Topics & Conditions** (Broadcast to millions of devices)
* **🍎 Native Apple iOS Bridge:** FCM automatically translates payloads into Apple APNs HTTP/2 binary protocol using a single uploaded company `.p8` Master Auth Key. No separate iOS backend required.
* **🔒 Zero-Secrets-in-Code (12-Factor):** All credentials injected via environment variables (`FIREBASE_APP_<NAME>_JSON`), with dual support for raw multiline JSON and Base64-encoded strings for CI/CD pipelines.
* **💻 Built-in Web Simulator:** Real-time in-browser PWA push simulator (`/simulator`) with live presence pings and token registration.

---

## 🏛️ System Architecture

```
+-----------------------------------------------------------------------------------+
|                           CLIENT & INGRESS TIER                                   |
|   [ Mobile Apps (iOS / Android / Flutter) ]   |   [ Upstream Microservices ]      |
+------------------------------------+-----------------------------+----------------+
                                     |                             |
                                     | Register / Heartbeat        | POST /api/notifications/send
                                     v                             v
+-----------------------------------------------------------------------------------+
|                   FIREBASE CONNECTOR GATEWAY (.NET 8)                             |
|                                                                                   |
|  +--------------------+   +-----------------------+   +------------------------+  |
|  |  DevicesController |   |   PresenceController  |   | NotificationsController|  |
|  +---------+----------+   +-----------+-----------+   +-----------+------------+  |
|            |                          |                             |             |
|            v                          v                             v             |
|  +--------------------+   +-----------------------+   +------------------------+  |
|  |   DeviceRegistry   |   |    PresenceService    |   |NotificationDelivery-   |  |
|  |   (Dead Pruning)   |   |   (45s Ping / 90s TTL)|   |Router (Multi-Tenant)   |  |
|  +--------------------+   +-----------+-----------+   +-----------+------------+  |
|                                       |                             |             |
|                                       v                             v             |
|                            [ Redis 7 Cluster ]            [ FirebaseAppManager ]  |
+---------------------------------------------------------------------+-------------+
                                                                      |
                                       +------------------------------+
                                       | Google OAuth2 HTTP v1
                                       v
+-----------------------------------------------------------------------------------+
|                        DELIVERY GATEWAY & PUSH CLOUD                              |
|                                                                                   |
|                   +---------------------------------------+                       |
|                   |       Google FCM HTTP v1 Gateway      |                       |
|                   +-------------------+-------------------+                       |
|                                       |                                           |
|                   +-------------------+-------------------+                       |
|                   |                                       |                       |
|        (Android Token)                         (iOS Token via .p8 Auth)           |
|                   |                                       |                       |
|                   v                                       v                       |
|       +-----------------------+               +-----------------------+           |
|       | Google Play Services  |               |  Apple APNs Servers   |           |
|       +-----------+-----------+               +-----------+-----------+           |
|                   |                                       |                       |
|                   v                                       v                       |
|           [ Android Phone ]                               [ Apple iPhone ]        |
+-----------------------------------------------------------------------------------+
```

---

## 🚀 Quickstart

### 1. Run with Podman Compose / Docker Compose

```bash
# 1. Clone repository and create your .env configuration
cp .env.example .env

# 2. Spin up Redis and the API container
podman-compose up -d
# or: docker compose up -d

# 3. View live logs
podman logs -f fcmpoc-api

# 4. Open Swagger UI & Documentation Hub
# Swagger:  http://localhost:5000/swagger
# Docs Hub: http://localhost:5000/docs/index.html
# Simulator: http://localhost:5000/simulator
```

### 2. Run Locally with .NET CLI

```bash
# Start local Redis (port 6379)
podman run -d --name fcmpoc-redis -p 6379:6379 redis:7-alpine

# Run the .NET API
dotnet run --project "src/FirebaseConnector/FirebaseConnector.csproj"
```

---

## 📋 API Reference

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/api/devices/register` | Registers device token, binds `appId`, and initializes 90s presence |
| `POST` | `/api/presence/heartbeat` | 45-second ping extending user presence sliding TTL in Redis |
| `GET` | `/api/presence/{userId}` | Queries real-time active status (`isActive`, `ttlSeconds`) |
| `POST` | `/api/notifications/send` | Unified delivery endpoint (Unicast, Multicast, Topic, Condition) |
| `POST` | `/api/topics/subscribe` | Subscribes a batch of device tokens to an FCM Pub/Sub topic |
| `POST` | `/api/topics/unsubscribe` | Removes a batch of device tokens from an FCM topic |

---

## 📬 Payload Samples

### 1. Alert Notification (With Active Presence Filter)
```json
POST /api/notifications/send
{
  "userId": "alex@company.com",
  "title": "Order Out for Delivery 🚚",
  "body": "Courier is approaching your destination.",
  "onlyIfActive": true,
  "priority": "High"
}
```

### 2. Silent Push (Background Sync)
```json
POST /api/notifications/send
{
  "userId": "alex@company.com",
  "isSilent": true,
  "data": {
    "action": "SYNC_DATABASE",
    "resource": "orders"
  }
}
```

### 3. Collapsible Live ETA Update
```json
POST /api/notifications/send
{
  "userId": "alex@company.com",
  "title": "Driver Arriving in 3 mins",
  "body": "Silver Honda Civic (Plate: ABC-987)",
  "collapseKey": "ORDER_8192_ETA"
}
```

### 4. Topic Broadcast (Pub/Sub to Millions)
```json
POST /api/notifications/send
{
  "topic": "drivers_announcements",
  "title": "High Surge Multiplier Active ⚡",
  "body": "Surge bonus +$5 per ride in Downtown."
}
```

---

## 📚 Modular Enterprise Documentation

Comprehensive interactive manuals and architectural records are available in the [`Docs/`](./Docs) folder and served live at `http://localhost:5000/docs/index.html`:

* [Documentation Portal (`Docs/index.html`)](./Docs/index.html)
* [System Architecture & Topology (`Docs/architecture.html`)](./Docs/architecture.html)
* [Presence Lifecycle & 90s Sliding TTL (`Docs/presence-lifecycle.html`)](./Docs/presence-lifecycle.html)
* [Apple iOS & APNs (.p8) Master Key Guide (`Docs/ios-integration.html`)](./Docs/ios-integration.html)
* [Notification Types & Payloads Specification (`Docs/notification-types.html`)](./Docs/notification-types.html)
* [Mobile Client SDKs: Swift, Kotlin, Flutter, React Native (`Docs/mobile-clients.html`)](./Docs/mobile-clients.html)
* [Podman, Docker & Kubernetes Deployment Manual (`Docs/deployment.html`)](./Docs/deployment.html)

### 📖 Step-by-Step Engineering Manuals (`Docs/Manual/`)
* [Manuals Hub & Library (`Docs/Manual/index.html`)](./Docs/Manual/index.html)
* [Manual 01: Setup & Verification Guide (`Docs/Manual/step-by-step-setup-guide.html`)](./Docs/Manual/step-by-step-setup-guide.html)
* [Manual 02: Mobile Apps & Multi-Project Integration (`Docs/Manual/mobile-apps-integration-guide.html`)](./Docs/Manual/mobile-apps-integration-guide.html)
* [Manual 03: Real-Time Event-Driven Mobile Architecture (`Docs/Manual/event-driven-mobile-architecture.html`)](./Docs/Manual/event-driven-mobile-architecture.html)

### 🏛️ Architecture Decision Records (ADR)
* [ADR Index & Decision Matrix (`Docs/ADR/index.html`)](./Docs/ADR/index.html)
* [ADR-001: FCM HTTP v1 Protocol & Admin SDK Migration](./Docs/ADR/ADR-001-fcm-http-v1.html)
* [ADR-002: Active Presence Tracking via Redis Sliding TTL](./Docs/ADR/ADR-002-sliding-presence.html)
* [ADR-003: Multi-Tenant Routing via Named FirebaseApp Instances](./Docs/ADR/ADR-003-multi-tenant-routing.html)
* [ADR-004: Zero Secrets Committed (Environment Secret Pattern)](./Docs/ADR/ADR-004-zero-secrets-in-code.html)
* [ADR-005: Unified Multi-Type Notification Payload Contract](./Docs/ADR/ADR-005-unified-payload.html)
* [ADR-006: Multi-App Token Isolation & Selective Broadcasting in FCM](./Docs/ADR/ADR-006-multi-app-targeting-and-broadcasting.html)

---

## 📄 License
MIT License. Built for enterprise push notification architecture evaluation.
