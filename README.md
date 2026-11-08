# Naba Game Booster

Windows 10/11 (64-bit) ke liye gaming booster. Khaas taur pe **BlueStacks 5 / MSI App Player + Free Fire MAX** ke liye.

## Install
1. [Releases](https://github.com/nabanur/naba-game-booster/releases/latest) se `NabaGameBooster-Setup.exe` download karo.
2. Chalao. Agar Windows "Windows protected your PC" dikhaye to **More info -> Run anyway**.
3. Desktop pe **Naba Game Booster** icon aa jayega.

App khulte hi (aur har 2 ghante) GitHub pe naya version check karta hai. **Auto update** ON ho to naya version khud download aur install ho jata hai. Header me **Update check** button bhi hai.

## Features
| Button | Kya karta hai |
|---|---|
| **ONE-CLICK BOOST** | PC pehchan ke (Low/Mid/High) saari settings ek saath + PC Score |
| Game Mode ON | Browser, Premiere, Animate, Telegram, Codex waghera PAUSE (kaam jaisa ka taisa), RAM khali; Google Drive/OneDrive/Epic band; BlueStacks HIGH priority |
| Game Mode OFF | Paused apps wahin se chalu, band hue apps wapas |
| Gaming Optimize | Ultimate Performance power plan, Windows Game Mode ON, Xbox recording OFF, GPU scheduling ON |
| BlueStacks / MSI Optimize | PC ki RAM dekh ke RAM/CPU, chuna hua FPS limit, High FPS, VSync off (backup banta hai) |
| Defender Exclusion | BlueStacks folders Defender scan se bahar |
| NVIDIA Setting | Power management = max performance, Shader cache = unlimited (sirf NVIDIA card pe) |
| Mouse 1:1 / Default | Mouse acceleration off / wapas |
| Memory Integrity OFF / ON | FPS ke liye off, security ke liye on (restart chahiye) |
| BlueStacks Turbo | Hamesha HIGH priority, high performance GPU, fullscreen optimization off |
| RAM Cleaner | Game Mode me cache (standby) RAM har 5 min saaf |
| Game Watchdog | Game Mode me CPU khaane wale background programs ki priority kam |
| ASTC | Free Fire textures graphics card pe decode (CPU halka) |
| Timer 0.5ms | Game Mode me Windows timer 0.5ms - smooth frames |
| Startup Apps | PC chalu hote hi khulne wale apps band/chalu |
| BlueStacks Fast Mode | Windows Hyper-V band - emulator apne tez engine pe (restart chahiye) |
| FPS Limit | Smooth (monitor ke barabar) / 120 / 240 - FPS counter ON |

## Naya version kaise nikale
`src/NabaGameBooster.csproj` me kuch badlo, `CHANGELOG-latest.md` me likho kya naya hai, phir tag push karo:
```
git tag v1.0.1
git push origin v1.0.1
```
GitHub Actions khud build karke Release bana dega. Saare PCs pe "Update karo" aa jayega.

## Third-party
- [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector) (MIT) - NVIDIA settings

Details: `THIRD-PARTY-NOTICES.txt` (app folder me).
