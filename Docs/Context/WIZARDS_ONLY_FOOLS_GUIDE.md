# Wizards Only Fools Guide — verified transcript

- Task ID: `69b0c752-cd14-832b-8269-b90db1e31325`
- Kind: ChatGPT
- Verified extent: one complete turn; `hasMore=false`, `nextCursor=null`
- Original focus: beginner S&box roadmap
- Authority: product/context history only; not a React or Unity implementation specification

## User

Being the pro game dev using game engine S&box, teach me from start to finish how to make wizards only fools step by step like you're explaining it to a five year old

## Assistant

Perfect — I’m going to teach this **exactly** how a senior S&box developer would teach a beginner, but explained **so simply a 5-year-old could follow**, while still being **real production workflow**.

We are going to build **Wizards Only Fools!** step-by-step from literally nothing → playable multiplayer wizard game.

Think of this like LEGO instructions.  
Each step unlocks the next.

---

# 🧙‍♂️ WIZARDS ONLY FOOLS — COMPLETE BEGINNER ROADMAP (S&box)

## The Big Idea (Kid Version)

You are making a game where:

- Players = wizards
- Wizards run around maps
- Wizards cast spells instead of guns
- Friends can join online
- Everyone tries to win

So the game needs:

1. A **world** (map)
2. A **wizard character**
3. **movement**
4. **spells**
5. **health + damage**
6. **multiplayer**
7. **game modes**
8. **menus + polish**

We build them **in this exact order**.

---

# ✅ STAGE 1 — Install & Prepare (The Playground)

### Step 1 — Install S&box
1. Install Steam
2. Install **S&box**
3. Launch once

You now have the sandbox where games are made.

---

### Step 2 — Open the Editor

Open S&box → press:

```
F7
```

You are now inside the **game creator mode**.

This is your wizard laboratory.

---

### Step 3 — Create Your Game Project

Click:

```
Create → New Addon
```

Name it:

```
wizards_only_fools
```

Your folder structure appears:

```
code/
assets/
scenes/
ui/
```

Think of it like:

| Folder | What it holds |
|---|---|
| code | brain |
| assets | art & sounds |
| scenes | maps |
| ui | menus |

---

# ✅ STAGE 2 — Make The World (First Map)

Before wizards exist, they need ground to stand on.

---

### Step 4 — Create Map

1. Open **Scene Editor**
2. File → New Scene
3. Save as:

```
wizard_city.scene
```

---

### Step 5 — Add Floor

Add → Primitive → Cube

Scale:

```
X: 2000
Y: 2000
Z: 20
```

Boom — ground exists.

You made your first world.

---

### Step 6 — Add Spawn Point

Add:

```
Player Spawn
```

This tells the game:

> "Players appear here."

Without this, players spawn in the void forever.

---

# ✅ STAGE 3 — Create The Wizard (Your Player)

Now we create the controllable character.

---

### Step 7 — Create Player Script

Inside `/code/` create:

```
WizardPlayer.cs
```

---

### Step 8 — Basic Player Code

Think of code like giving instructions to a toy.

Paste this:

```csharp
using Sandbox;

public sealed class WizardPlayer : Component
{
    CharacterController controller;

    protected override void OnStart()
    {
        controller = Components.Get<CharacterController>();
    }

    protected override void OnUpdate()
    {
        var move = Input.AnalogMove;

        controller.Move(move * 200);
    }
}
```

What this says:

> When player presses keys → move wizard.

You now have movement.

---

### Step 9 — Add Character Controller

Select Player object → Add Component:

```
Character Controller
```

This gives:
- gravity
- walking
- collisions

Without it you would float like a ghost.

---

# ✅ STAGE 4 — Camera (Seeing Through Wizard Eyes)

Add new script:

```
WizardCamera.cs
```

```csharp
using Sandbox;

public sealed class WizardCamera : Component
{
    protected override void OnUpdate()
    {
        Camera.Position = GameObject.Position + Vector3.Up * 64;
        Camera.Rotation = Rotation.FromYaw(Input.AnalogLook.yaw);
    }
}
```

Attach it to player.

Now you can SEE.

---

# ✅ STAGE 5 — Health System ❤️

Every game needs damage.

Create:

```
Health.cs
```

```csharp
using Sandbox;

public sealed class Health : Component
{
    public float HP = 100;

    public void Damage(float amount)
    {
        HP -= amount;

        if (HP <= 0)
            GameObject.Destroy();
    }
}
```

Wizard can now die.

Congratulations — you made game logic.

---

# ✅ STAGE 6 — First Spell (Arcane Bolt)

Spells replace guns.

---

### Step 1 — Create Spell Script

```
ArcaneBolt.cs
```

```csharp
using Sandbox;

public sealed class ArcaneBolt : Component
{
    protected override void OnUpdate()
    {
        if (Input.Pressed("attack1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        Log.Info("Magic fired!");
    }
}
```

Press mouse → spell triggers.

Baby wizard magic achieved.

---

### Step 2 — Add Projectile Later
(We keep it simple first.)

Game dev rule:

> Make it work → then make it cool.

---

# ✅ STAGE 7 — Multiplayer (The MAGIC SAUCE)

Good news:

S&box multiplayer is **automatic** if you follow rules.

Key rule:

```
Server controls truth.
```

Change shooting:

```csharp
[Rpc.Broadcast]
void Shoot()
{
    Log.Info("Magic fired!");
}
```

Now everyone sees spell events.

You just made multiplayer networking.

---

# ✅ STAGE 8 — Wizard Abilities System

Instead of hardcoding spells, create slots.

```
SpellManager.cs
```

Concept:

```
Left Click = Arcane Bolt
Q = Fireburst
E = Blink
R = Shield
```

Pseudo-logic:

```csharp
if(Input.Pressed("slot1")) CastSpell(0);
```

Now spells are swappable.

Professional architecture achieved.

---

# ✅ STAGE 9 — Movement Powers

Add:

### Dash

```csharp
if(Input.Pressed("dash"))
{
    controller.Velocity += Transform.Rotation.Forward * 500;
}
```

---

### Flight Timer

```
float flightTime = 3f;
```

Decrease while flying.

Recharge later.

You now have wizard mobility.

---

# ✅ STAGE 10 — Game Modes

Create:

```
WizardGameManager.cs
```

Controls:

- Free For All
- Team Deathmatch
- Capture Runestone
- Escort
- Boss Mode

GameManager decides:

```
Who wins?
Who spawns?
What rules apply?
```

Every multiplayer game has one.

---

# ✅ STAGE 11 — UI (Wizard Face HUD)

Create UI panel:

```
WizardHUD.razor
```

Display:

- health
- spells
- wizard face reaction

UI reads values from player.

---

# ✅ STAGE 12 — Polish Phase ✨

Now you add:

- sounds
- particles
- spell glow
- wizard colors
- menus
- matchmaking

This is where games feel alive.

---

# 🧠 REAL DEV SECRET (Most Important Lesson)

You DO NOT build whole game at once.

You build in loops:

```
Make tiny thing →
Test →
Fix →
Improve →
Repeat
```

Professional studios work exactly like this.

---

# 🧙 Your Development Order (MEMORIZE THIS)

1. Map
2. Player movement
3. Camera
4. Health
5. One spell
6. Multiplayer sync
7. Spell system
8. Movement abilities
9. Game modes
10. UI
11. Polish

If you follow this order, your project will **not collapse**.

---

# ⭐ What You Just Learned

You now understand:

- how S&box games are structured
- player architecture
- multiplayer basics
- ability systems
- real dev workflow

You are already thinking like a game dev.

---

## Next Step (VERY IMPORTANT)

Next I can teach you the **pro-level version**:

👉 **The EXACT folder + architecture layout AAA S&box devs use**  
(the structure that prevents your project from becoming spaghetti code later)

Say:

> “Teach me the pro architecture”

and we level you up to real studio workflow.

