# Asset import policy and first slice

The React `public` folder contains 403 media files (`85.151 MiB`), but only 201 are referenced. There are 71 corrupted files and 51 exact-duplicate groups. The generated React manifest is therefore not a safe Unity import list.

The first slice imports only 20 verified, referenced RGBA PNGs:

- `sprites/misc/idle_1.png` through `idle_4.png`
- `sprites/fireball/fireball_1.png` through `fireball_5.png`
- `sprites/fireball/fireballidle_1.png` through `fireballidle_10.png`
- `sprites/misc/spellbook_icon.png`

Unity import settings:

- Sprite texture type
- sRGB and alpha enabled
- Point filtering
- Mipmaps disabled
- No trimming
- Center pivot

No local models, audio, fonts, shaders, or material files exist in the React project. Much of the world art is generated procedurally in TypeScript/canvas code and must be recreated in C#/Shader Graph or baked by deterministic editor tooling.

Licensing and provenance are unresolved: the React repository contains no license, credits, attribution, or asset-provenance file. Imported assets remain internal-only until ownership is documented.

