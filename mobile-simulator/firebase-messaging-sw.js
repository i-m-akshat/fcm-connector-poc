// Firebase Messaging Service Worker (handles background push notifications)
importScripts("https://www.gstatic.com/firebasejs/10.12.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/10.12.0/firebase-messaging-compat.js");

firebase.initializeApp({
  apiKey: "YOUR_FIREBASE_WEB_API_KEY",
  authDomain: "your-project.firebaseapp.com",
  projectId: "your-project-id",
  storageBucket: "your-project.firebasestorage.app",
  messagingSenderId: "000000000000",
  appId: "1:000000000000:web:0000000000000000000000"
});

const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
  console.log("[firebase-messaging-sw.js] Background message received:", payload);

  // If there is no notification object, this is a Silent Push (background data sync only)
  if (!payload.notification) {
    console.log("[firebase-messaging-sw.js] Silent background push received. App synced without disturbing user.");
    return;
  }

  const title = payload.notification.title || "Real-Time Notification";
  const options = {
    body: payload.notification.body || "You have a new alert.",
    icon: "https://www.gstatic.com/mobilesdk/160503_mobilesdk/logo/2x/firebase_96dp.png",
    data: payload.data
  };

  self.registration.showNotification(title, options);
});
