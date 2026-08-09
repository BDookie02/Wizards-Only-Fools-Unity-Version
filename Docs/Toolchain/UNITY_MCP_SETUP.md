# Unity MCP setup (D: only)

This project is pinned to CoplayDev Unity MCP `v10.1.0` and uses Codex's local `stdio` MCP transport. The Unity package, Codex configuration, uv runtime, managed Python, package caches, tool environment, temporary files, logs, and downloaded artifacts are all directed to D:.

The setup deliberately does **not** launch Unity, start the MCP server, request elevation, edit a PowerShell profile, modify the system `PATH`, or run a remote installer script. Unity licensing and the first Unity Package Manager import remain explicit user-controlled steps.

## Reviewed identities

| Component | Exact identity |
|---|---|
| Unity package | `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#c14de1e6dc01ab42d2bb358730cff954bce0ce6b` |
| Unity package commit | `c14de1e6dc01ab42d2bb358730cff954bce0ce6b` |
| MCP Python distribution | `mcpforunityserver==10.1.0` |
| uv | `0.12.2`, x64 Windows MSVC archive |
| uv archive SHA256 | `01442d8ce5c7124151a73e697c836d252c6da853c18c73206d3cc4c2378a91d2` |
| Managed CPython | `3.14.7` |
| MCP wheel | `mcpforunityserver-10.1.0-py3-none-any.whl` |
| MCP wheel SHA256 | `3d64a8fd2542133b619bfa1edcf9ffa80796c0618a88814569429635d72459d7` |
| Codex transport | `stdio` |

Primary evidence:

- [Unity MCP v10.1.0 release](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.1.0)
- [Unity package metadata at v10.1.0](https://raw.githubusercontent.com/CoplayDev/unity-mcp/v10.1.0/MCPForUnity/package.json)
- [MCP server metadata at v10.1.0](https://raw.githubusercontent.com/CoplayDev/unity-mcp/v10.1.0/Server/pyproject.toml)
- [CoplayDev's Codex configuration helper](https://raw.githubusercontent.com/CoplayDev/unity-mcp/v10.1.0/MCPForUnity/Editor/Helpers/CodexConfigHelper.cs)
- [uv 0.12.2 release](https://github.com/astral-sh/uv/releases/tag/0.12.2)
- [PyPI metadata for mcpforunityserver 10.1.0](https://pypi.org/pypi/mcpforunityserver/10.1.0/json)
- [Codex MCP configuration](https://developers.openai.com/codex/mcp)
- [Codex configuration reference](https://developers.openai.com/codex/config-reference)

## D: layout

The installer owns only `D:\UnityMCPToolchain`:

```text
D:\UnityMCPToolchain\
  uv\0.12.2\
  downloads\
  cache\
  python-cache\
  python\
  python-bin\
  tools\
  tool-bin\
  credentials\
  user-profile\
  app-data\roaming\
  app-data\local\
  xdg\cache\
  xdg\config\
  xdg\data\
  xdg\state\
  logs\
  temp\
  staging\
  receipts\
```

The Unity package pin lives in `Packages/manifest.json` and names the audited v10.1.0 commit directly rather than trusting a mutable tag. The Codex server definition lives in the trusted-project configuration `.codex/config.toml`. Codex starts the already-installed, verified `D:\UnityMCPToolchain\tool-bin\mcp-for-unity.exe` directly with `--transport stdio`; it does not ask `uvx` to re-resolve the package at runtime. Telemetry is disabled for the MCP process. Its user profile, `PATH`, `APPDATA`, `LOCALAPPDATA`, XDG state, temporary paths, and MCP log directory are all restricted to reviewed D: locations for the lifetime of that process; no user or system environment variable is changed persistently.

## Run the setup

Start from the D: project root, then use the absolute D: script path. The script also moves its process working directory to this verified, non-reparse D: project for every native/network operation and restores the caller's location on exit.

```powershell
Set-Location 'D:\CodexProjects\Wizards-Only-Fools-Unity'
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-mcp.ps1' plan
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-mcp.ps1' install
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\setup-unity-mcp.ps1' verify
```

`plan` is read-only: it performs no downloads and creates no directories. `install` performs the reviewed D-only installation. It downloads the uv archive and publisher checksum sidecar directly to D:, compares the publisher checksum with the hardcoded reviewed checksum, rejects unsafe ZIP entries, installs exact managed CPython without Windows registry registration, verifies the exact PyPI wheel record and hash, and installs from that local verified wheel. Installed MCP package files are compared byte-for-byte with the reviewed wheel without executing candidate tool interpreters. The receipt under `D:\UnityMCPToolchain\receipts` binds the generated launcher plus the file count and composite SHA256 identity of the entire installed tool environment, including every transitive dependency file. It never starts the MCP server. `verify` is an offline integrity check and also does not start Unity or MCP.

Existing files are treated as immutable. A matching prior install is reused; a conflicting archive, executable, Python tool environment, or launcher causes a hard failure instead of an overwrite.

## Why stdio, not HTTP

CoplayDev's v10.1.0 server supports stdio, and Codex supports project-scoped MCP configuration in `.codex/config.toml`. The direct D: launcher is used instead of the helper's `uvx --from ...` form so runtime cannot independently re-resolve a package after setup.

Local Streamable HTTP is intentionally rejected for this Windows setup while [OpenAI Codex issue #26955](https://github.com/openai/codex/issues/26955) remains open. The report reproduces a Windows Codex Desktop/CLI failure before MCP initialization against a local Coplay/FastMCP HTTP endpoint. HTTP should not be added as a fallback until that issue is resolved and the exact flow is physically re-verified on this machine.

## First Unity/Codex connection

After `install` succeeds:

1. Resume the D-only recreation pipeline: `powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\CodexProjects\Wizards-Only-Fools-Unity\Tools\automate-unity-recreation.ps1' resume -Apply`. Its Unity package-migration stages resolve the pinned Git package and require `Packages\packages-lock.json` to bind v10.1.0 to commit `c14de1e6dc01ab42d2bb358730cff954bce0ce6b`. This cannot be truthfully claimed complete before Unity imports it.
2. If the pipeline reports that Unity Personal is not activated, run its `open-activation -Apply` action, complete sign-in/activation through Unity Hub in the normal browser, and rerun `resume -Apply`. The Hub may remain open. After automated verification passes, `Tools\wof-unity.ps1 open` can open the resolved project interactively. Allow Unity to finish compiling/importing and open the MCP for Unity window if the package requests normal one-time Editor-side setup.
3. Close/restart Codex, then open and trust a new Codex task rooted at `D:\CodexProjects\Wizards-Only-Fools-Unity`. The current task cannot dynamically acquire a newly added project MCP definition.
4. Confirm that Codex lists `unityMCP` and test a read-only Unity action before allowing project mutations.

Windows itself, PowerShell, Unity licensing, certificate validation, and the inherited `SystemRoot` may use OS-managed state on the system drive. This project does not try to relocate or rewrite those operating-system facilities. All state controlled by this Unity MCP setup is explicitly placed on D:.
