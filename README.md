# Naba Game Booster

Windows ke liye chhota sa gaming booster. Khaas taur pe **BlueStacks + Free Fire** ke liye.

## Install
1. [Releases](https://github.com/nabanur/naba-game-booster/releases/latest) se `NabaGameBooster-Setup.exe` download karo.
2. Chalao. Agar Windows "Windows protected your PC" dikhaye to **More info -> Run anyway**.
3. Desktop pe **Naba Game Booster** icon aa jayega.

App khulte hi GitHub pe naya version check karta hai, aur naya version ho to **"Update karo"** button dikhata hai.

## Features
| Button | Kya karta hai |
|---|---|
| Game Mode ON | Browser, Adobe background, Telegram, Codex, Drive waghera band. RAM khali, BlueStacks HIGH priority |
| Game Mode OFF | Band hue apps wapas khulte hain |
| Gaming Optimize | Ultimate Performance power plan, Windows Game Mode ON, Xbox recording OFF, GPU scheduling ON |
| BlueStacks Optimize | PC ki RAM dekh ke BlueStacks RAM/CPU set, FPS 120, VSync off (backup banta hai) |
| Defender Exclusion | BlueStacks folders Defender scan se bahar |
| NVIDIA Setting | Power management = max performance, Shader cache = unlimited (sirf NVIDIA card pe) |
| Mouse 1:1 / Default | Mouse acceleration off / wapas |
| Memory Integrity OFF / ON | FPS ke liye off, security ke liye on (restart chahiye) |

## Naya version kaise nikale
`src/NabaGameBooster.csproj` me kuch badlo, `CHANGELOG-latest.md` me likho kya naya hai, phir tag push karo:
```
git tag v1.0.1
git push origin v1.0.1
```
GitHub Actions khud build karke Release bana dega. Saare PCs pe "Update karo" aa jayega.

## Third-party
NVIDIA settings ke liye [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector) (MIT License) app ke saath aata hai.
