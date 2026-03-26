![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

# GeminiOrbFX

Interactive Orb Effects for Beat Saber (1.40.8) triggered by live events (TikTok, Twitch, Streamer.bot, HTTP).

<img width="1638" height="822" alt="Screenshot 2026-03-26 165130" src="https://github.com/user-attachments/assets/484d41cd-97d7-424a-aeab-a2e3981f75ea" />


---

## ✨ Features

- Gameplay orbs triggered by chat, follows, gifts, etc
- Built-in TikTok Relay mode
- Streamer.bot / HTTP support (recommended)
- Twitch support via Streamer.bot
- Chat command triggers
- Gift filtering (specific gifts / coin thresholds)
- Follow triggers
- Live preview for orb settings 
- Debug tools for testing

---

## 📦 Installation

1. Download the latest release  
2. Drop `GeminiOrbFX.UI.dll` into:  
   Beat Saber/Plugins/  
3. Launch the game  

---

## 🔌 Service Modes

### 📡 TikTok Relay

- Built into the mod  
- Requires your own Euler API key (see below)  
- Connect from the in-game UI
<img width="756" height="637" alt="Tiktok Connection" src="https://github.com/user-attachments/assets/94ee67b0-c7ab-4e2f-baf4-dac45d836ed4" />
<img width="771" height="647" alt="Tiktok Triggers" src="https://github.com/user-attachments/assets/2660a9dd-fc86-4600-801f-c16481e67f96" />
<img width="760" height="656" alt="Tiktok Gift Settings" src="https://github.com/user-attachments/assets/f1327966-1505-4753-8b62-7c4e1ef55f01" />


---

### 🤖 Streamer.bot (Recommended)

- Works with:
  - Twitch
  - TikTok (via Streamer.bot / TikFinity)
  - YouTube
- No API key required  
- Uses simple HTTP endpoints  
- Most stable option
<img width="726" height="633" alt="Tiktok Streamer Bot Setting" src="https://github.com/user-attachments/assets/49235365-29f7-428b-bdf2-51cf217a6e12" />
  

---

## 🔑 TikTok Relay Setup

TikTok Relay requires your own Euler API key.

1. Get your API key from Euler https://www.eulerstream.com/register
2. Create a new key and keep this safe 
3. Run the game once  
4. Go to: Beat Saber/UserData/
5. Open your config file (GeminiOrbFX)  
6. Find: "EulerApiKey": ""
7. Add your key:
8. Save and restart the game  

---

## 🌐 HTTP Endpoints

Base URL: http://127.0.0.1:6556/

Examples:

```
http://127.0.0.1:6556/orb?name=USERNAME
http://127.0.0.1:6556/orb?name=USERNAME&lane=2
http://127.0.0.1:6556/orb?name=USERNAME&speed=6.5
http://127.0.0.1:6556/orb?name=USERNAME&color=#FF00FF
http://127.0.0.1:6556/orb?name=USERNAME&color=cyan
http://127.0.0.1:6556/orb?name=USERNAME&h=0.83&s=0.85&v=0.85
http://127.0.0.1:6556/orb?name=USERNAME&hsv=0.83,0.85,0.85
```

Parameters:
- name → username shown on orb  
- lane → 0–3  
- speed → orb speed  
- color → hex or named color  
- h,s,v → HSV values  
- hsv → comma format

## ⚡ Streamer.bot Preset

A ready-to-use preset is included in Releases.

Just import it into Streamer.bot — no manual setup required. 

---

## 🎮 In-Game Controls

- Main Menu  
  - Shows service mode + status  
  - Queue info

<img width="794" height="674" alt="GeminiOrbFX Main" src="https://github.com/user-attachments/assets/cd35083b-e1d2-4d30-9e8f-1588673f3f78" />

- Gameplay Setup  
  - Live status view  
  - Queue + connection state

<img width="958" height="611" alt="Gameplay Menu Status" src="https://github.com/user-attachments/assets/5ece5008-7955-49cf-8ab7-9fea6f2b1f3d" />
 
- Orb Controls  
  - Real-time preview  
  - No save required for testing  
  - Save to keep changes

<img width="886" height="686" alt="Orb Controls Preview" src="https://github.com/user-attachments/assets/eb21b4f9-309c-4b08-89a1-de9a66b99cb3" />

---

## ⚠️ Notes

- TikTok Relay requires your own API key  
- No API keys are included with this mod  
- Streamer.bot mode is the easiest setup  
- Preview changes only save when you press Save  

---

## 📌 Version

v2.0.0 (Beat Saber 1.40.8)

---

## ❤️ Credits

Created by - Josh / GeminiJoshVR  
Cheif tester - Swifty / sw1ftyc1  

---

## 🌐 Community

https://discord.gg/swiftyandjoshiesparadise 

---

## 📬 Feedback

Open an issue on GitHub if needed.

---

## 📜 License

MIT License
