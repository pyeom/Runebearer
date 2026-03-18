# Runebearer

A Unity game project developed collaboratively.

---

## Requirements

- **Unity 6000.3.11f1** — you must use this exact version to avoid serialization issues
- **Unity Hub** — used to manage Unity installations and open projects
- **Git** — for cloning and version control

---

## Getting Started

### 1. Install Unity Hub

Download and install Unity Hub from the [official Unity website](https://unity.com/download).

### 2. Install the correct Unity version

1. Open Unity Hub
2. Go to **Installs** → **Install Editor**
3. Select version **6000.3.11f1** (Unity 6)
   - If it doesn't appear in the list, use **Archive** → find it on the Unity download archive
4. During installation, make sure to include:
   - **Linux Build Support** (if on Linux)
   - **Windows Build Support** (if you plan to build for Windows)
   - Any other target platforms you need

### 3. Clone the repository

```bash
git clone https://github.com/pyeom/Runebearer.git
cd Runebearer
```

### 4. Open the project in Unity

1. Open Unity Hub
2. Go to **Projects** → **Add** → **Add project from disk**
3. Navigate to the cloned `Runebearer` folder and select it
4. Make sure the editor version shown is **6000.3.11f1** — if Unity Hub prompts you to install a different version, cancel and install the correct one first
5. Click the project to open it

Unity will import all assets and compile scripts on first launch — this may take a few minutes.

---

## Package Overview

This project uses the following Unity packages (all managed automatically via `Packages/manifest.json`):

| Package | Version | Purpose |
|---|---|---|
| Universal Render Pipeline (URP) | 17.3.0 | Rendering |
| Input System | 1.19.0 | Player input |
| ProBuilder | 6.0.9 | In-editor 3D modeling |
| Cinemachine | 3.1.6 | Camera control |
| AI Navigation | 2.0.11 | Pathfinding / NavMesh |
| Timeline | 1.8.11 | Cutscenes / sequencing |
| Visual Scripting | 1.9.10 | Node-based scripting |

You do **not** need to install these manually — Unity resolves them automatically when you open the project.

---

## Project Structure

```
Assets/
├── Prefabs/        # Reusable game objects
├── Scenes/         # Unity scene files
├── Scripts/        # C# game scripts
└── Settings/       # URP renderer and quality settings
Packages/           # Package dependencies
ProjectSettings/    # Unity project configuration
```

---

## Workflow Tips

- **Always pull before you start working:**
  ```bash
  git pull origin main
  ```
- **Never commit the `Library/`, `Temp/`, or `Logs/` folders** — they are gitignored and are generated locally by Unity.
- **Communicate before editing scenes** — Unity scene files (`.unity`) are hard to merge. Coordinate with the team so two people aren't editing the same scene at the same time.
- **Always save scenes in Unity** (`Ctrl+S`) before committing, otherwise your changes won't be in the file.
