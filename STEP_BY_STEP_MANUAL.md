# Complete Step-by-Step Manual: Firebase Connector & FCM Push Notifications

This manual is your comprehensive, button-by-button guide to setting up Google Firebase, generating credentials, running the **Firebase Connector** backend, and delivering real push notifications.

---

## Table of Contents
1. [Overview & How FCM Actually Works](#1-overview--how-fcm-actually-works)
2. [Phase 1: Firebase Console Setup (Click-by-Click)](#2-phase-1-firebase-console-setup-click-by-click)
3. [Phase 2: Setting up Client (Getting Your First Device Token)](#3-phase-2-setting-up-client-getting-your-first-device-token)
4. [Phase 3: The Firebase Connector Backend Architecture](#4-phase-3-the-firebase-connector-backend-architecture)
5. [Phase 4: Running with Podman / Docker Compose](#5-phase-4-running-with-podman--docker-compose)
6. [Phase 5: Step-by-Step Practice & Verification Walkthrough](#6-phase-5-step-by-step-practice--verification-walkthrough)
7. [Phase 6: Multi-App & Production FAQ](#7-phase-6-multi-app--production-faq)

---

## 1. Overview & How FCM Actually Works

To push notifications to a mobile device, three parties interact:
1. **The Client (Phone / Simulator):**
   * Downloads a configuration file (`google-services.json` on Android, `GoogleService-Info.plist` on iOS, or `firebaseConfig` on Web).
   * Asks Google for a **Device Registration Token** (e.g. `c7xY...`).
   * Sends this token to your backend along with the user's ID/email.
2. **Your Backend ("Firebase Connector"):**
   * Stores the user's token and tracks if the user is **actively logged in** (via Redis 45s heartbeats).
   * Authenticates with Google using a **Service Account Private Key JSON file**.
   * Sends the notification payload to Google’s FCM REST API.
3. **Google FCM Servers:**
   * Receives the request from your backend, validates your Service Account authority, finds the target device, and delivers the message to the phone screen!

---

## 2. Phase 1: Firebase Console Setup (Click-by-Click)

Follow these exact steps in your browser:

### Step 1.1: Create a Firebase Project
1. Open [https://console.firebase.google.com/](https://console.firebase.google.com/) and sign in with your Google account.
2. Click the big card labeled **+ Add project** (or **Create a project**).
3. **Step 1 of 3 (Project Name):**
   * Type a project name, e.g., `fcmpoc-connector`.
   * Check the box to accept the Firebase terms.
   * Click the blue **Continue** button.
4. **Step 2 of 3 (Google Analytics):**
   * For this POC, you can **turn off** the toggle for *Enable Google Analytics for this project* (simplifies setup).
   * Click the blue **Create project** button.
5. Wait 15–30 seconds while Google provisions the cloud project.
6. When it says *"Your new project is ready"*, click the blue **Continue** button.

---

### Step 1.2: Generate the Backend Service Account Key (.json) [Updated for Latest Firebase Console]
This is the "master key" that grants your ASP.NET Core backend authority to send notifications via the **FCM HTTP v1 API**:

1. In the top-left sidebar of the Firebase Console, locate **Project Overview**.
2. Immediately to the right of "Project Overview", click the **Gear Icon (⚙️)**.
3. In the dropdown menu, click **Project settings**.
4. In the top navigation tabs, click the **Service accounts** tab.
5. In the center card titled **Firebase Admin SDK**:
   * You will see the automatically provisioned service account email:  
     `firebase-adminsdk-xxxxx@<your-project-id>.iam.gserviceaccount.com`.
   * Under code snippets (`Node.js`, `Java`, `Python`, `Go` — .NET uses the same JSON key), click the blue button:  
     👉 **Generate new private key**.
6. A confirmation modal will appear warning you:
   > *"Generate a new private key? Your private key gives access to your project's Firebase services. Keep it confidential."*
7. Click the blue **Generate key** button.
8. Your browser will download a file named like:  
   `fcmpoc-connector-firebase-adminsdk-xxxxx-xxxxxxxxxx.json`.
9. **Rename this file** to:  
   `firebase-key.json`  
   and move it to your project folder:  
   `A:\asp.net Projects\FCMPOC\secrets\firebase-key.json`.

#### ⚠️ Modern Firebase & Google Cloud Edge Cases (Check these if you encounter issues):

* **Case 1: "Firebase Cloud Messaging API (V1) - Enabled" Check:**
  * In **Project settings**, click the **Cloud Messaging** tab.
  * Look under **Firebase Cloud Messaging API (V1)**. It should show as **Enabled**.
  * *(Note: Google deprecated and permanently disabled the legacy Cloud Messaging API in June 2024. Your Firebase Connector uses the modern HTTP v1 protocol exclusively).*

* **Case 2: "Key creation is blocked by organization policy" (Corporate Google accounts):**
  * If your company's Google Cloud organization enforces the constraint `iam.disableServiceAccountKeyCreation`, you will see an error when clicking *Generate new private key*.
  * **Fix:** For local POC testing, create the Firebase project using a personal Google account (`@gmail.com`), or ask your GCP organization admin to add an exception for this test project.

* **Case 3: Alternative way via Google Cloud Console:**
  * If the Firebase Console page ever spins or hangs, you can download the exact same key directly from Google Cloud IAM:
    1. Navigate to: [https://console.cloud.google.com/iam-admin/serviceaccounts](https://console.cloud.google.com/iam-admin/serviceaccounts) (select your project at top).
    2. Click on the `firebase-adminsdk` service account.
    3. Click the **Keys** tab ➔ **Add Key** ➔ **Create new key** ➔ Select **JSON** ➔ Click **Create**.
    4. It downloads the identical private key file!

```text
A:\asp.net Projects\FCMPOC\secrets\firebase-key.json
```

Open it only to verify that it contains `project_id`, `client_email`, and `private_key`, and that `project_id` matches the Firebase project used by the web client. Do not paste the file contents into source code, screenshots, chat, or public repositories. The `secrets` folder is mounted read-only into the container in Step 4.1.

---

### Step 1.3: Set Up a Client Receiver (The Fast 60-Second Web Client)
To receive real notifications without building an Android APK:

1. Still in **Project settings**, click the **General** tab.
2. Scroll down to the section titled **Your apps**.
3. Under *"There are no apps in your project"*, click the **Web icon (`</>`)**.
4. In the **App nickname** box, type: `TestMobileClient`.
5. Leave the box *"Also set up Firebase Hosting"* unchecked.
6. Click the blue **Register app** button.
7. Google will show you a code snippet containing your `firebaseConfig`. It looks like:
   ```javascript
   const firebaseConfig = {
     apiKey: "AIzaSyD-xxxxxxxxxxxxxxxx",
     authDomain: "fcmpoc-connector.firebaseapp.com",
     projectId: "fcmpoc-connector",
     storageBucket: "fcmpoc-connector.appspot.com",
     messagingSenderId: "123456789012",
     appId: "1:123456789012:web:abcdef123456"
   };
   ```
   **Copy these values** (you will paste them into `mobile-simulator/index.html`).
8. Click **Continue to console**.

---

### Step 1.4: Generate the Web Push Certificate (VAPID Key)
1. Go back to the **Cloud Messaging** tab inside **Project settings**.
2. Scroll down to the bottom section titled **Web configuration**.
3. Under **Web Push certificates**, find the button labeled **Generate key pair** and click it.
4. A long alphanumeric string will appear (e.g. `BEl8...`).  
   **Copy this key** (this is your `vapidKey`).

---

## 3. Phase 2: Setting up Client (Getting Your First Device Token)

We have provided a ready-to-run Mobile Phone Simulator inside:  
`A:\asp.net Projects\FCMPOC\mobile-simulator\index.html`

### Step 2.1: Configure the Simulator
1. Open `A:\asp.net Projects\FCMPOC\mobile-simulator\index.html` in your text editor (VS Code / Notepad).
2. Find lines 122–127:
   ```javascript
   const firebaseConfig = {
     apiKey: "YOUR_API_KEY",
     projectId: "YOUR_PROJECT_ID",
     messagingSenderId: "YOUR_SENDER_ID",
     appId: "YOUR_APP_ID"
   };
   ```
   Paste the values from **Step 1.3**.
3. On line 170, replace `"YOUR_VAPID_KEY"` with the VAPID key from **Step 1.4**:
   ```javascript
   deviceToken = await getToken(messaging, { vapidKey: "YOUR_COPIED_VAPID_KEY" });
   ```
4. Save the file.

### Step 2.2: Launch the Simulator in Your Browser
1. Double-click `index.html` to open it in Google Chrome or Microsoft Edge.
2. In the simulator screen:
   * Target App: `CustomerApp`
   * Email: `driver_alex@company.com`
   * API URL: `http://localhost:5000/api`
3. Click **🔑 Login & Register Device**.
4. When your browser pops up asking: *"Allow notifications?"*, click **Allow**.
5. The simulator will display your real, genuine **Google FCM Device Token** and begin sending heartbeats!

---

## 4. Phase 3: The Firebase Connector Backend Architecture

The backend (`FirebaseConnector`) exposes 3 essential API endpoints:

| Method | Endpoint | Purpose |
| :--- | :--- | :--- |
| `POST` | `/api/devices/register` | Stores the user's `deviceToken`, `appId`, and `userId`. Marks device as `IsActive = true`. |
| `POST` | `/api/presence/heartbeat` | Mobile app sends this every 45s. Extends Redis TTL by 90 seconds. |
| `POST` | `/api/devices/logout` | Deactivates device in DB, deletes Redis key so no pushes are sent to logged-out users. |
| `POST` | `/api/notifications/send` | Dispatches notification. If `onlyIfActive: true`, verifies presence in Redis before dispatching. |

### How It Dispatches:
1. The **Delivery Router** checks Redis: `presence:user:{userId}:device:{deviceId}`.
2. If the user is inactive or logged out, it skips delivery (preventing stale notification spam).
3. If active, it resolves the **Named FirebaseApp instance** for that `appId`.
4. It calls `FirebaseMessaging.SendEachForMulticastAsync(message)`.
5. If Google FCM responds with `MessagingErrorCode.Unregistered` (e.g. user uninstalled app), the router automatically marks the token inactive in the database.

---

## 5. Phase 4: Running with Podman / Docker Compose (Configured via .env)

All environment variables, ports, connection strings, and Firebase project paths are configured via **`.env`** instead of modifying `appsettings.json`.

### Step 4.1: Drop Your Firebase Key
Place your downloaded Service Account key here:
```
A:\asp.net Projects\FCMPOC\secrets\firebase-key.json
```

### Step 4.2: Configure Multi-App Secrets in `.env`
Open `A:\asp.net Projects\FCMPOC\.env` and follow our standardized multi-app naming convention:

```env
# Standard Multi-App Secret Convention:
# Pattern: FIREBASE_APP_<NAME>_JSON='<MINIFIED_JSON_OR_BASE64>'

FIREBASE_APP_CUSTOMER_JSON='{"type":"service_account","project_id":"fcmpoc-customer-app",...}'
FIREBASE_APP_DRIVER_JSON='{"type":"service_account","project_id":"fcmpoc-driver-app",...}'
FIREBASE_APP_MERCHANT_JSON='{"type":"service_account","project_id":"fcmpoc-merchant-app",...}'

# Global / Default Fallback:
FIREBASE_DEFAULT_CREDENTIALS_JSON='{"type":"service_account","project_id":"fcmpoc-connector",...}'
```

> [!TIP]
> **Pro-Tip on JSON formatting in `.env`:**
> 1. **Option A (Minified JSON):** Open your downloaded `.json` in VS Code / Notepad, remove line breaks so it's a single line, and wrap it in single quotes (`'...'`).
> 2. **Option B (Base64 - cleanest for CI/CD):** Encode the file to Base64 in PowerShell:  
>    `[Convert]::ToBase64String([IO.File]::ReadAllBytes("firebase-key.json"))`  
>    and paste the Base64 string directly. Our C# backend automatically detects and decodes Base64 in memory!
> 3. **Auto-Discovery:** Whenever a new app is added (e.g. `FIREBASE_APP_VENDOR_JSON`), the backend **auto-discovers it at startup** without requiring any code changes!

### Step 4.3: Start Containers with Podman Compose
Open PowerShell in `A:\asp.net Projects\FCMPOC`:

```powershell
# Start Redis and the Firebase Connector API
podman-compose up -d --build
```
*(Or if using Docker Compose: `docker-compose up -d --build`)*

### Step 4.4: Verify Running Services
```powershell
podman-compose ps
```
You will see:
* `fcmpoc-redis` running on port `6379`
* `fcmpoc-api` running on port `5000` (auto-injected with all `.env` values)

Test that the API is up by opening Swagger in your browser:  
👉 **[http://localhost:5000/swagger](http://localhost:5000/swagger)**

---

## 6. Phase 5: Step-by-Step Practice & Verification Walkthrough

Follow this 5-minute scenario to practice the entire flow:

```
Scenario: A customer places an order, and the backend sends a real-time alert to Driver Alex ONLY IF Driver Alex is actively on duty (app open).
```

### Test Case 1: Active User Receives Real Push Notification
1. Open `mobile-simulator/index.html` in your browser.
2. Log in as `driver_alex@company.com`.
3. Notice the status badge turns **ONLINE (ACTIVE)**, and the heartbeat countdown begins.
4. Open Swagger at [http://localhost:5000/swagger](http://localhost:5000/swagger) (or use Postman/curl).
5. Expand `POST /api/notifications/send` -> click **Try it out**.
6. Paste the payload:
   ```json
   {
     "userId": "driver_alex@company.com",
     "title": "New Ride Request! 🚗",
     "body": "Pickup at Terminal 2, Airport.",
     "onlyIfActive": true
   }
   ```
7. Click **Execute**.
8. **Result:** 
   * Response: `200 OK` (`"success": true, "deliveredCount": 1`).
   * A real Windows push notification pops up on your screen!

---

### Test Case 2: Inactive User (App Minimized) is Filtered Out
1. In `mobile-simulator/index.html`, click **⏸️ Minimize App**.
2. Notice the badge turns **BACKGROUND** and heartbeats pause.
3. Wait **90 seconds** (for the Redis TTL to expire).
4. Send the same notification request again in Swagger.
5. **Result:**
   * Response: `200 OK` (`"success": false, "reason": "UserInactive"`).
   * **No notification is delivered!** The router correctly identified that the user is not actively on screen.

---

### Test Case 3: Logged-Out User is Blocked
1. In `mobile-simulator/index.html`, bring the app back to foreground and click **🚪 Logout**.
2. The simulator calls `/api/devices/logout`.
3. Send the notification again in Swagger with `"onlyIfActive": false`.
4. **Result:**
   * Response: `"success": false, "reason": "NoActiveDevicesFound"`.
   * The token was deactivated in the database; no notification reaches a logged-out device!

---

## 7. Phase 6: Multi-App & Production FAQ

### Q1: Tomorrow I need to add a second app ("MerchantApp"). What changes?
1. In Firebase Console, click **Project settings** > **General** > **Add app** (add your Merchant App).
2. In your C# `appsettings.json`, add an entry under `FirebaseProjects`:
   ```json
   "FirebaseProjects": {
     "CustomerApp": {
       "ProjectId": "fcmpoc-connector",
       "CredentialsPath": "/app/secrets/firebase-key.json"
     },
     "MerchantApp": {
       "ProjectId": "fcmpoc-connector",
       "CredentialsPath": "/app/secrets/firebase-key.json"
     }
   }
   ```
3. When the merchant logs in, their app specifies `"appId": "MerchantApp"`. The router routes it automatically!

### Q2: What if the Merchant App has a completely DIFFERENT Firebase Project?
Download the Merchant App's own private key JSON and save it as `/app/secrets/merchant-key.json`.  
Point `CredentialsPath` for `MerchantApp` to that file. The `FirebaseAppManager` loads both keys as separate named instances!

---

You are now 100% prepared to implement, demonstrate, and explain this feature with confidence!
