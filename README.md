# Naba Game Booster

Windows ke liye chhota sa gaming booster. Khaas taur pe **BlueStacks + Free Fire** ke liye.

## Install
1. [Releases](https://github.com/nabanur/naba-game-booster/releases/latest) se `NabaGameBooster-Setup.exe` download karo.
2. Chalao. Agar Windows "Windows protected your PC" dikhaye to **More info -> Run anyway**.
3. Desktop pe **Naba Game Booster** icon aa jayega.

App khulte hi (aur har 2 ghante) GitHub pe naya version check karta hai. **Auto update** ON ho to naya version khud download aur install ho jata hai. Header me **Update check** button bhi hai.

## Features
| Button | Kya karta hai |
|---|---|
| Game Mode ON | Browser, Premiere, Animate, Telegram, Codex waghera PAUSE (kaam jaisa ka taisa), RAM khali; Google Drive/OneDrive/Epic band; BlueStacks HIGH priority |
| Game Mode OFF | Paused apps wahin se chalu, band hue apps wapas |
| Gaming Optimize | Ultimate Performance power plan, Windows Game Mode ON, Xbox recording OFF, GPU scheduling ON |
| BlueStacks Optimize | PC ki RAM dekh ke BlueStacks RAM/CPU set, FPS 120, VSync off (backup banta hai) |
| Defender Exclusion | BlueStacks folders Defender scan se bahar |
| NVIDIA Setting | Power management = max performance, Shader cache = unlimited (sirf NVIDIA card pe) |
| Mouse 1:1 / Default | Mouse acceleration off / wapas |
| Memory Integrity OFF / ON | FPS ke liye off, security ke liye on (restart chahiye) |
| BlueStacks Turbo | Hamesha HIGH priority, high performance GPU, fullscreen optimization off |
| RAM Cleaner | Game Mode me cache (standby) RAM har 5 min saaf |
| Timer 0.5ms | Game Mode me Windows timer 0.5ms - smooth frames |
| Startup Apps tab | PC chalu hote hi khulne wale apps band/chalu |
| Aim Trainer tab | 30 second headshot practice |

## Naya version kaise nikale
`src/NabaGameBooster.csproj` me kuch badlo, `CHANGELOG-latest.md` me likho kya naya hai, phir tag push karo:
```
git tag v1.0.1
git push origin v1.0.1
```
GitHub Actions khud build karke Release bana dega. Saare PCs pe "Update karo" aa jayega.

## Third-party
NVIDIA settings ke liye [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector) (MIT License) app ke saath aata hai.
