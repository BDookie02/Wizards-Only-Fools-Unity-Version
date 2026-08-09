# Unity 6.3 LTS toolchain setup

The project toolchain is pinned to Unity `6000.3.21f1`, changeset `c02631ffc030`. Unity released this build on July 29, 2026. The automation never resolves a moving `latest` or `lts` alias.

The exact base Editor is direct-installed at `D:\UnityEditors\6000.3.21f1`. `Tools\install-unity-editor.ps1` downloads the immutable official installer, verifies its pinned byte length, SHA-256, valid Unity Authenticode signer and signer thumbprint, then verifies the installed product version and changeset before the component workflow is allowed to run.

Build-support components use `Tools\install-unity-components.ps1`, not Unity CLI module installation. Unity CLI `1.0.0-beta.3` can discover this direct Editor, but it refuses to install modules for an Editor that was not installed through Unity Hub. The CLI path and observed version remain visible in the plan as diagnostic information only.

The required module set is:

- Android Build Support: `android`
- Web Build Support: `webgl`
- Windows Build Support (IL2CPP): `windows-il2cpp`
- Windows Dedicated Server Build Support: `windows-server`
- Android's pinned SDK, NDK r27c, OpenJDK 17, CMake 3.22.1, platform tools, build tools 36, command-line tools 16.0, and Android platforms 34 through 36

## D-only environment

The automation rejects every controlled path that is not rooted on `D:`. Before it starts an Editor child process, it preserves the normal Windows identity variables (`APPDATA`, `LOCALAPPDATA`, `USERPROFILE`, `HOMEDRIVE`, and `HOMEPATH`) so Unity can reuse the Hub-authenticated licensing client. It explicitly redirects the large mutable work areas instead:

- `TEMP=D:\tmp\wof-unity`
- `TMP=D:\tmp\wof-unity`
- `UPM_CACHE_ROOT=D:\UnityPackageCache`
- `UPM_NPM_CACHE_PATH=D:\UnityPackageCache\npm`
- `UPM_CACHE_PATH=D:\UnityPackageCache\packages`
- `UPM_GIT_LFS_CACHE_PATH=D:\UnityPackageCache\git-lfs`
- `GRADLE_USER_HOME=D:\UnityAndroidState\Gradle` for build/test runs
- `ANDROID_USER_HOME=D:\UnityAndroidState\AndroidUser` for build/test runs

Unity Personal activation specifically requires Unity Hub; launching the Editor directly cannot activate a Personal license. `Tools\setup-unity-hub.ps1` downloads the current official signed Hub installer, installs the application at `D:\UnityHub`, keeps Hub's Electron user data under `D:\UnityHubProfile` and temporary files under `D:\tmp\unity-hub`, and receipt-verifies both installer and installed executable. It deliberately preserves the normal Windows identity environment so browser-based Unity authentication reuses the existing signed-in Chrome profile, and registers `unityhub://` callbacks with the same D-drive Hub data argument. Before batch Unity work, `refresh-license -Apply` restarts only the D-hosted Hub processes, reuses the stored token, and requires fresh entitlement and status-200 seat evidence in the D Hub log. Browser account data, the tiny Windows protocol registry entry, installer registration, and machine license storage remain Windows-managed security/state boundaries.

Additional D-drive locations are:

- Editor: `D:\UnityEditors\6000.3.21f1`
- Signed installers and Android archives: `D:\UnityInstallers\6000.3.21f1`
- Component extraction staging: `D:\UCS\3`
- Component temporary work: `D:\UCT\3`
- Unity Package Manager cache: `D:\UnityPackageCache`
- Toolchain logs: `D:\UnityCli\WofToolchain\logs`
- Pre-upgrade source backups: `D:\UnityProjectBackups\Wizards-Only-Fools-Unity`
- Resumable automation state: `D:\UnityAutomationState\Wizards-Only-Fools-Unity`

All paths controlled by this project are pinned to `D:`. Windows services, Unity licensing services, and the Codex/PowerShell host can still use pre-existing operating-system-managed state on the system drive; the scripts cannot redirect or honestly guarantee otherwise. Existing-token license refresh is automated. Initial account authentication, any required reauthentication, and UAC approval remain manual security boundaries.

## Runbook

The recommended entry point plans and resumes the complete workflow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' plan
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' resume -Apply
```

The automation records each stage atomically on D: and stops with a precise resume instruction at UAC, Unity activation, an open-project lock, or a real verification failure. The lower-level runbook remains available for diagnosis.

Inspect local toolchain state, package-migration stage, and D-drive free space without launching an installer, Unity CLI, Unity Hub, or the Editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-toolchain.ps1' plan
```

To download/install or verify the exact signed base Editor independently:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\install-unity-editor.ps1' install -Apply
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\install-unity-editor.ps1' verify
```

To install or repair its required components:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-toolchain.ps1' install -Apply
```

The `-Apply` switch is the explicit mutation and license-term gate. The setup wrapper calls the direct installer with `-Action install -Apply`. That installer uses URLs pinned to Unity `6000.3.21f1` and fixed Android dependency versions. It validates exact byte sizes plus publisher-provided MD5 or SHA-384 values where available, records SHA-256 values for every artifact, rejects unsafe ZIP paths before extraction, and verifies complete payload/evidence tree digests against atomic install receipts.

Review Unity's and Google's applicable license terms before using `-Apply`. The workflow is idempotent: verified downloads and installed components are retained, complete partial downloads are promoted only after verification, and an incomplete or conflicting destination is not silently overwritten. Signed Unity component executables can request elevation even though their destination is on `D:`. Windows may show one UAC prompt; the user must approve it. Once all executable payload receipts still match, later resumes skip elevation entirely.

After installation, close any Editor using this project and run the isolated baseline upgrade:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-toolchain.ps1' upgrade-project -Apply
```

The baseline action deliberately stops at NGO `2.13.0`. In order, it:

1. Verifies the exact Editor and all direct-installed build components.
2. Creates a verified timestamped D-drive backup of `Assets`, `Packages`, and `ProjectSettings`, stages it under a unique partial name, writes completion metadata, and promotes it only after source/destination inventories match.
3. Calls `Tools\migrate-unity-packages.ps1 -Action apply` before the first Unity 6000.3 project open. This pins the Unity 6000.3 baseline packages while preserving uGUI `2.0.0`.
4. Opens Unity 6000.3 in batch mode so Package Manager resolves the baseline, updates `packages-lock.json`, and updates the project serialization identity.
5. Calls the package tool's `verify` action and requires both manifest and lock file to resolve NGO `2.13.0`.

It never deletes an existing Unity lock file or overwrites a completed backup. The package migration tool also creates a verified, timestamped D-drive source backup before changing the manifest.

Only after the baseline passes, apply the isolated NGO patch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-toolchain.ps1' upgrade-ngo-patch -Apply
```

This second action first verifies the resolved `2.13.0` baseline, changes only NGO to `2.13.1`, runs a separate Unity Package Manager resolution, and verifies the final manifest and lock state. A resumable manifest already at `2.13.1` is resolved without being downgraded.

Baseline and NGO-patch Unity runs have separate timestamped logs in `D:\UnityCli\WofToolchain\logs`. Errors name the exact package-apply, Unity-resolution, or verification phase. Do not continue to the NGO patch when the baseline action fails.

The source runner, `Tools\wof-unity.ps1`, resolves the exact Editor version from `ProjectSettings\ProjectVersion.txt`, checks only approved D-drive Editor roots, and rejects a missing or non-D Editor.

If batch migration stops because Unity Personal is not activated, open the verified D-hosted Unity Hub while preserving the normal signed-in browser identity:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' open-activation -Apply
```

Account sign-in and license activation are manual security steps. The Hub may remain open while batch automation resumes.

Verify the direct component installation, exact Editor, required modules, project revision, source runner, and final NGO `2.13.1` package state:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-toolchain.ps1' verify
```

## Authoritative references

- [Unity Download Archive](https://unity.com/releases/editor/archive) - official Editor and build-support downloads.
- [Android Studio downloads](https://developer.android.com/studio) - official Android SDK command-line tool and dependency distribution.
- [Unity 6000.3.21f1 release page](https://unity.com/releases/editor/whats-new/6000.3.21f1) — exact Editor version, release date, changeset, and Windows component availability.
- [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference) — standalone invocation, positional Editor versions, module flags, `--cm`, environment controls, output formats, and exit codes.
