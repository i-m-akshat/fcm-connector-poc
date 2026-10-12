# Firebase Secrets Folder

Place your downloaded Google Firebase Service Account Private Key file here:

```
secrets/firebase-key.json
```

### How to get this file:
1. Open [Firebase Console](https://console.firebase.google.com/).
2. Click **Project settings (⚙️)** > **Service accounts** tab.
3. Click the blue button: **Generate new private key**.
4. Rename the downloaded file to `firebase-key.json` and place it in this folder.

> [!WARNING]
> This folder is mounted into the container at `/app/secrets:ro`. Never commit `firebase-key.json` to public version control!
