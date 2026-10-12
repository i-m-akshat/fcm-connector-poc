# 📱 Mobile Apps Integration Manual & Multi-Project Guide
**FCM Connector (.NET 8) — iOS, Android, Flutter, React Native & Multi-Tenant Firebase Integration**

---

## Table of Contents
1. [How iOS Works with FCM (Under the Hood)](#1-how-ios-works-with-fcm-under-the-hood)
2. [Step-by-Step iOS Setup (Apple Developer & Firebase)](#2-step-by-step-ios-setup-apple-developer--firebase)
3. [Multi-Project Setup: Connecting Existing Apps with Separate Firebase Projects](#3-multi-project-setup-connecting-existing-apps-with-separate-firebase-projects)
4. [Mobile Client Implementation Snippets](#4-mobile-client-implementation-snippets)
   - [Swift (Native iOS)](#a-swift-native-ios)
   - [Kotlin (Native Android)](#b-kotlin-native-android)
   - [Flutter (Cross-Platform)](#c-flutter-ios--android)
   - [React Native (Cross-Platform)](#d-react-native)
5. [Testing & Verification Playbook](#5-testing--verification-playbook)

---

## 1. How iOS Works with FCM (Under the Hood)

Many engineers wonder: *"Does Firebase Cloud Messaging work natively on iOS, or do I need Apple APNs?"*

### The Architecture:
```
+--------------------------+
|  Firebase Connector      |
|  (.NET 8 Backend)        |
+------------+-------------+
             |
             | 1. HTTP v1 Push Request (Tokens / Topic)
             v
+--------------------------+
|  Google FCM Gateway      |
+------------+-------------+
             |
             +-------------------------------+
             |                               |
  (If token is Android)             (If token is iOS)
             |                               |
             v                               v
+------------------------+      +------------------------+
| Google Play Services   |      | Apple APNs Servers     |
+------------+-----------+      +------------+-----------+
             |                               |
             v                               v
     [ Android Phone ]                 [ Apple iPhone ]
```

1. FCM **does not replace Apple Push Notification service (APNs)**.
2. FCM acts as an enterprise gateway:
   - When an iPhone opens the app, iOS issues an **Apple APNs Token**.
   - The Firebase iOS SDK exchanges that APNs token with Google to get an **FCM Device Token**.
   - When our backend sends a push to FCM, FCM automatically translates the message into Apple’s native HTTP/2 APNs format (complete with `aps`, `badge`, `sound`, `content-available`, and `interruption-level`) and relays it directly to Apple APNs servers.
   - Apple then delivers the notification directly to the iPhone.

---

## 2. Step-by-Step iOS Setup (Apple Developer & Firebase)

To enable iOS push notifications, you only need to configure Apple APNs credentials in your Firebase Console **once**.

### Step 2.1: Generate an APNs Authentication Key in Apple Developer Portal
1. Log in to [developer.apple.com/account](https://developer.apple.com/account).
2. Go to **Certificates, Identifiers & Profiles** > **Keys**.
3. Click the **`+`** button to create a new key.
4. Name the key (e.g., `FCM APNs Key`).
5. Check the box for **Apple Push Notifications service (APNs)**.
6. Click **Continue** > **Register**.
7. Download the key file (it will be named `AuthKey_<KEY_ID>.p8`).
   > ⚠️ **IMPORTANT:** Save this `.p8` file in a secure location. Apple only allows you to download it **once**.
8. Note down your:
   - **Key ID:** (10-character string shown next to the key).
   - **Team ID:** (10-character string located in the top-right corner of your Apple Developer account).

---

### Step 2.2: Upload the `.p8` Key to Firebase Console
1. Open the [Firebase Console](https://console.firebase.google.com/).
2. Select your project (e.g., `fcmpoc-connector`).
3. Click the **Gear icon (⚙️)** > **Project settings**.
4. Switch to the **Cloud Messaging** tab.
5. Scroll down to **Apple app configuration**.
6. Under **APNs Authentication Key**, click **Upload**:
   - Upload the `.p8` file.
   - Enter your 10-character **Key ID**.
   - Enter your 10-character **Team ID**.
7. Click **Upload**.

### 💡 Key Concept: What is the APNs Key? Is it Different for Each Device?

> **NO, the APNs Key is NOT different for each device!**

The APNs Key (`.p8` file) is **ONE single master credential** for your entire Apple Developer Account:
* Think of it as your company's **Master Passkey**. It tells Apple that Google Firebase has your official permission to deliver notifications to any of your iOS apps.
* You generate it **ONCE** in the Apple Developer Portal and upload it **ONCE** to Firebase Console.
* You **never** install or put the `.p8` key on user devices or inside mobile app code.

#### ⚖️ APNs Key vs. Device Token (Clear Comparison)

| Concept | What is it? | How many exist? | Where does it go? |
| :--- | :--- | :--- | :--- |
| **Apple APNs Key (`.p8`)** | Company master authorization signature. | **Exactly 1** for your whole company / team. | Uploaded **once** into **Firebase Console**. |
| **Device Token (FCM Token)** | The unique digital address of one specific phone. | **Unique for every device** (Alex's phone has one, Bob's has another). | Sent by the mobile app to our backend (`POST /api/devices/register`). |

> 🚀 **Important Backend Note:**
> You do **NOT** need a separate backend for iOS! The exact same .NET Firebase Connector endpoint (`POST /api/notifications/send`) delivers to both Android and iOS devices automatically. Firebase handles the Apple APNs translation behind the scenes using this uploaded `.p8` key.

---

> 💡 **Why `.p8` is superior to `.p12` certificates:**
> - A `.p8` auth key **never expires** (unlike `.p12` certificates which expire every 12 months).
> - One `.p8` key works for **all** your iOS apps (Customer, Driver, Merchant).
> - One key works across both **Development/Sandbox** and **Production**.

---

### Step 2.3: Configure Xcode Capabilities
In your iOS project in Xcode:
1. Select your target > **Signing & Capabilities**.
2. Click **`+ Capability`** and add **Push Notifications**.
3. Click **`+ Capability`** again and add **Background Modes**:
   - Check **Remote notifications** (This is **required** for Silent Push notifications to wake up your app!).
   - Check **Background fetch** (recommended).

---

## 3. Multi-Project Setup: Connecting Existing Apps with Separate Firebase Projects

If your company has multiple existing mobile apps, they might each have their own separate Firebase project (e.g., `CustomerApp` in Project A, `DriverApp` in Project B, `MerchantApp` in Project C).

Our backend is built specifically to support **both**:
- **Option 1:** Multiple apps sharing **one** Firebase project.
- **Option 2:** Multiple apps with **separate, independent** Firebase projects.

```
                    +------------------------------------+
                    |    Firebase Connector Backend      |
                    |    (Routes dynamically via AppId)  |
                    +----+-------------+------------+----+
                         |             |            |
         FIREBASE_APP_CUSTOMER_JSON    |   FIREBASE_APP_MERCHANT_JSON
                         |             |            |
                         v             |            v
                 +---------------+     |    +---------------+
                 |  Firebase     |     |    |  Firebase     |
                 |  Project A    |     |    |  Project C    |
                 | (CustomerApp) |     |    | (MerchantApp) |
                 +---------------+     |    +---------------+
                                       v
                             FIREBASE_APP_DRIVER_JSON
                                       v
                               +---------------+
                               |  Firebase     |
                               |  Project B    |
                               |  (DriverApp)  |
                               +---------------+
```

### Step 3.1: Download Service Account Keys for Each Existing Project
For each existing mobile app project in Firebase Console:
1. Open [Firebase Console](https://console.firebase.google.com/).
2. Select that app's project.
3. Go to **Project settings (⚙️)** > **Service accounts**.
4. Click **Generate new private key** and download the JSON file.

### Step 3.2: Register the Keys in `.env`
In the backend's `.env` file, use the standardized convention:
`FIREBASE_APP_<APP_NAME>_JSON='<JSON_CONTENT>'`

```bash
# App 1: Existing Customer App Project
FIREBASE_APP_CUSTOMERAPP_JSON='{"type":"service_account","project_id":"my-customer-firebase",...}'

# App 2: Existing Driver App Project
FIREBASE_APP_DRIVERAPP_JSON='{"type":"service_account","project_id":"my-driver-firebase",...}'

# App 3: Existing Merchant App Project
FIREBASE_APP_MERCHANTAPP_JSON='{"type":"service_account","project_id":"my-merchant-firebase",...}'

# App 4: Your FCMPOC App
FIREBASE_APP_FCMPOC_JSON='{"type":"service_account","project_id":"fcmpoc-connector",...}'
```

> 🔒 **Security Note:** You can also pass the JSON as a **Base64 string** if your deployment CI/CD platform (e.g. AWS EKS, Azure Container Apps, Kubernetes Secrets) does not support multiline strings.

### Step 3.3: How Dynamic Multi-Tenant Delivery Works
1. When the mobile client logs in, it registers its `appId`:
   ```json
   POST /api/devices/register
   {
     "userId": "driver_alex@company.com",
     "appId": "DriverApp",
     "deviceToken": "fcm_token_...",
     "deviceType": "iOS"
   }
   ```
2. When you trigger a push:
   ```json
   POST /api/notifications/send
   {
     "userId": "driver_alex@company.com",
     "title": "New Ride!",
     "body": "Customer waiting."
   }
   ```
3. The connector backend:
   - Queries the user's active devices.
   - Automatically sees that driver Alex's device belongs to `DriverApp`.
   - Routes the dispatch to the `FIREBASE_APP_DRIVERAPP_JSON` Firebase project instance.
   - If user Alex also has a device registered with `CustomerApp`, the backend groups devices and dispatches to **both** respective Firebase projects in parallel!

---

## 4. Mobile Client Implementation Snippets

Copy-paste these snippets into your existing client apps to connect them to the Firebase Connector.

---

### A. Swift (Native iOS)

#### 1. In `AppDelegate.swift`:
```swift
import UIKit
import FirebaseCore
import FirebaseMessaging
import UserNotifications

@main
class AppDelegate: UIResponder, UIApplicationDelegate, UNUserNotificationCenterDelegate, MessagingDelegate {

    var window: UIWindow?
    var heartbeatTimer: Timer?
    var currentFCMToken: String?
    let currentUserId = "driver_alex@company.com"
    let currentAppId = "DriverApp" // Or "FCMPOC", "CustomerApp"
    let apiBaseUrl = "https://your-api-domain.com/api"

    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        FirebaseApp.configure()
        Messaging.messaging().delegate = self

        // 1. Request Notification Permissions
        UNUserNotificationCenter.current().delegate = self
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .badge, .sound]) { granted, _ in
            if granted {
                DispatchQueue.main.async {
                    application.registerForRemoteNotifications()
                }
            }
        }
        return true
    }

    // 2. Receive FCM Token from Google
    func messaging(_ messaging: Messaging, didReceiveRegistrationToken fcmToken: String?) {
        guard let token = fcmToken else { return }
        self.currentFCMToken = token
        print("✅ FCM Registration Token: \(token)")
        
        // Register device with your .NET backend
        registerDeviceWithBackend(token: token)
    }

    // 3. Register Device Token with Backend
    func registerDeviceWithBackend(token: String) {
        guard let url = URL(string: "\(apiBaseUrl)/devices/register") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")

        let payload: [String: Any] = [
            "userId": currentUserId,
            "appId": currentAppId,
            "deviceToken": token,
            "deviceType": "iOS"
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)

        URLSession.shared.dataTask(with: request) { _, response, _ in
            if let httpResp = response as? HTTPURLResponse, httpResp.statusCode == 200 {
                print("✅ Registered device with backend API.")
                DispatchQueue.main.async {
                    self.startHeartbeatLoop()
                }
            }
        }.resume()
    }

    // 4. Sliding Window 45s Heartbeat Loop
    func startHeartbeatLoop() {
        heartbeatTimer?.invalidate()
        heartbeatTimer = Timer.scheduledTimer(withTimeInterval: 45.0, repeats: true) { [weak self] _ in
            self?.sendHeartbeat()
        }
    }

    func sendHeartbeat() {
        guard let token = currentFCMToken,
              let url = URL(string: "\(apiBaseUrl)/presence/heartbeat") else { return }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let payload = ["userId": currentUserId, "deviceId": token]
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        URLSession.shared.dataTask(with: request).resume()
    }

    // 5. Handle Background Silent Push Notifications
    func application(_ application: UIApplication, didReceiveRemoteNotification userInfo: [AnyHashable : Any],
                     fetchCompletionHandler completionHandler: @escaping (UIBackgroundFetchResult) -> Void) {
        print("🔕 Silent Background Notification Received: \(userInfo)")
        
        // Handle your background data sync here (e.g., fetch orders, sync SQLite)
        completionHandler(.newData)
    }
}
```

---

### B. Kotlin (Native Android)

#### 1. In `MyFirebaseMessagingService.kt`:
```kotlin
package com.company.app

import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.io.IOException

class MyFirebaseMessagingService : FirebaseMessagingService() {

    private val apiBaseUrl = "https://your-api-domain.com/api"
    private val httpClient = OkHttpClient()

    override fun onNewToken(token: String) {
        super.onNewToken(token)
        registerDeviceWithBackend(token)
    }

    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        super.onMessageReceived(remoteMessage)

        // Case 1: Silent Push (Data-Only)
        if (remoteMessage.notification == null && remoteMessage.data.isNotEmpty()) {
            val action = remoteMessage.data["action"]
            // Trigger local database sync or work manager
            return
        }

        // Case 2: Visible Alert Push
        remoteMessage.notification?.let {
            showNotification(it.title ?: "Alert", it.body ?: "")
        }
    }

    private fun registerDeviceWithBackend(token: String) {
        val json = JSONObject().apply {
            put("userId", "driver_alex@company.com")
            put("appId", "DriverApp")
            put("deviceToken", token)
            put("deviceType", "Android")
        }
        val body = json.toString().toRequestBody("application/json".toMediaType())
        val request = Request.Builder().url("$apiBaseUrl/devices/register").post(body).build()
        httpClient.newCall(request).enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {}
            override fun onResponse(call: Call, response: Response) {}
        })
    }

    private fun showNotification(title: String, body: String) {
        // Standard Android NotificationManagerCompat channel notification builder
    }
}
```

---

### C. Flutter (iOS & Android)

#### In `main.dart`:
```dart
import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:http/http.dart' as http;

const String apiBaseUrl = "https://your-api-domain.com/api";
const String userId = "driver_alex@company.com";
const String appId = "DriverApp"; // Matches your .env key

// Background Handler (for silent & terminated pushes)
@pragma('vm:entry-point')
Future<void> _firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
  if (message.notification == null && message.data.isNotEmpty) {
    // 🔕 Silent Data-Only Push received! Sync local SQLite / Hive here.
    print("Silent background push: ${message.data}");
  }
}

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();
  FirebaseMessaging.onBackgroundMessage(_firebaseMessagingBackgroundHandler);
  runApp(const MyApp());
}

class MyApp extends StatefulWidget {
  const MyApp({super.key});
  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  String? deviceToken;
  Timer? heartbeatTimer;

  @override
  void initState() {
    super.initState();
    initFCM();
  }

  Future<void> initFCM() async {
    // 1. Request Permission (Crucial for iOS)
    NotificationSettings settings = await FirebaseMessaging.instance.requestPermission(
      alert: true, badge: true, sound: true,
    );

    if (settings.authorizationStatus == AuthorizationStatus.authorized) {
      // 2. Acquire FCM Token
      deviceToken = await FirebaseMessaging.instance.getToken();
      if (deviceToken != null) {
        await registerDevice(deviceToken!);
        startHeartbeatLoop(deviceToken!);
      }

      // 3. Listen for Token Refreshes
      FirebaseMessaging.instance.onTokenRefresh.listen((newToken) {
        registerDevice(newToken);
      });

      // 4. Foreground Notifications
      FirebaseMessaging.onMessage.listen((RemoteMessage message) {
        if (message.notification != null) {
          // Show in-app banner / snackbar
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text("${message.notification!.title}: ${message.notification!.body}")),
          );
        }
      });
    }
  }

  Future<void> registerDevice(String token) async {
    await http.post(
      Uri.parse("$apiBaseUrl/devices/register"),
      headers: {"Content-Type": "application/json"},
      body: jsonEncode({
        "userId": userId,
        "appId": appId,
        "deviceToken": token,
        "deviceType": "FlutterMobile",
      }),
    );
  }

  void startHeartbeatLoop(String token) {
    heartbeatTimer?.cancel();
    // 45-second sliding window ping
    heartbeatTimer = Timer.periodic(const Duration(seconds: 45), (timer) async {
      await http.post(
        Uri.parse("$apiBaseUrl/presence/heartbeat"),
        headers: {"Content-Type": "application/json"},
        body: jsonEncode({"userId": userId, "deviceId": token}),
      );
    });
  }

  @override
  void dispose() {
    heartbeatTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(
      home: Scaffold(body: Center(child: Text("FCM Client Connected"))),
    );
  }
}
```

---

### D. React Native

```javascript
import React, { useEffect } from 'react';
import messaging from '@react-native-firebase/messaging';

const API_BASE = 'https://your-api-domain.com/api';
const USER_ID = 'driver_alex@company.com';
const APP_ID = 'DriverApp';

export default function App() {
  useEffect(() => {
    async function setupFCM() {
      // 1. Request permission for iOS
      const authStatus = await messaging().requestPermission();
      const enabled =
        authStatus === messaging.AuthorizationStatus.AUTHORIZED ||
        authStatus === messaging.AuthorizationStatus.PROVISIONAL;

      if (enabled) {
        // 2. Get FCM token
        const token = await messaging().getToken();
        await registerDevice(token);

        // 3. Start 45s Heartbeat Loop
        const interval = setInterval(() => {
          sendHeartbeat(token);
        }, 45000);

        return () => clearInterval(interval);
      }
    }

    setupFCM();
  }, []);

  async function registerDevice(token) {
    await fetch(`${API_BASE}/devices/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        userId: USER_ID,
        appId: APP_ID,
        deviceToken: token,
        deviceType: 'ReactNative',
      }),
    });
  }

  async function sendHeartbeat(token) {
    await fetch(`${API_BASE}/presence/heartbeat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId: USER_ID, deviceId: token }),
    });
  }

  return null;
}
```

---

## 5. Testing & Verification Playbook

### Verifying iOS Push End-to-End:
1. Run your iOS app on a **Physical iPhone** (Apple does not support push notifications on Xcode iOS Simulators unless running on macOS 13+ with Apple Silicon and iOS 16+).
2. Look at Xcode Console to find your logged FCM Token.
3. Call `GET http://localhost:5000/api/devices` to verify your iOS device is registered with `deviceType: "iOS"`.
4. Call `POST http://localhost:5000/api/notifications/send`:
   ```json
   {
     "userId": "driver_alex@company.com",
     "title": "iOS Test Alert 🍏",
     "body": "Delivered successfully through Apple APNs!",
     "isSilent": false
   }
   ```
5. Lock the iPhone screen. Within 1-2 seconds, the native iOS lock-screen banner will appear.
