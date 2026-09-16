#!/usr/bin/env python3
"""Create the project's default ScriptableObject assets.

The demo content — four playable classes, the five named test characters, a practice target, the
camera settings and the bootstrap configuration — is plain authored data. Writing it here rather
than by hand keeps every asset's YAML consistent and lets a fresh checkout restore any asset that
was deleted by accident.

The script never overwrites an existing asset: once the editor owns a file, this stops touching it.
Run `generate_meta.py` afterwards to give any newly written asset its .meta file.

Asset references are stored as GUIDs, and those GUIDs come from the same path-derived function
`generate_meta.py` uses, so references written here resolve to the files that script will stamp.

Usage:
    python3 Tools/Unity/generate_default_data.py [--force]
"""

from __future__ import annotations

import argparse
from pathlib import Path

from generate_meta import guid_for

ROOT = Path(__file__).resolve().parents[2]
DATA = "Assets/_Project/Data"
SCRIPTS = "Assets/_Project/Scripts"

# --- Enum values, mirrored from the C# declarations ------------------------
# These must stay in step with the enums; the C# side documents that values are stable and
# append-only precisely so a table like this can exist.
ROLE = {"None": 0, "Tank": 1, "Healer": 2, "MeleeDamage": 3, "RangedDamage": 4}
RESOURCE = {"None": 0, "Mana": 1, "Rage": 2, "Energy": 3, "Focus": 4}
FACTION = {"Neutral": 0, "Raid": 1, "Enemy": 2}
STAT = {
    "MaxHealth": 1, "MaxResource": 2, "ResourceRegen": 3, "MovementSpeed": 4,
    "AttackPower": 5, "SpellPower": 6, "Armor": 7, "Resistance": 8,
    "CriticalChance": 9, "CriticalMultiplier": 10, "ThreatModifier": 11,
    "DamageTakenModifier": 12, "DamageDoneModifier": 13, "HealingDoneModifier": 14,
    "CastSpeed": 15,
}
PROJECTION = {"Orthographic": 0, "Perspective": 1}
DAMAGE_TYPE = {"Physical": 0, "Magic": 1, "True": 2}

# Script GUIDs, derived from where each class lives.
SCRIPT_GUIDS = {
    "ClassDefinition": guid_for(f"{SCRIPTS}/Characters/Data/ClassDefinition.cs"),
    "CharacterDefinition": guid_for(f"{SCRIPTS}/Characters/Data/CharacterDefinition.cs"),
    "CameraRigSettings": guid_for(f"{SCRIPTS}/CameraRig/Data/CameraRigSettings.cs"),
    "BootstrapConfig": guid_for(f"{SCRIPTS}/Game/Bootstrap/BootstrapConfig.cs"),
    "CombatTuningAsset": guid_for(f"{SCRIPTS}/Combat/Data/CombatTuningAsset.cs"),
}

HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
"""


def num(value) -> str:
    """Render a number the way Unity writes it: no trailing .0 on whole values."""
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, int):
        return str(value)
    return str(int(value)) if float(value).is_integer() else repr(round(float(value), 6))


def asset_ref(path: str | None) -> str:
    """Reference to another ScriptableObject asset, or the null reference."""
    if path is None:
        return "{fileID: 0}"
    return f"{{fileID: 11400000, guid: {guid_for(path)}, type: 2}}"


def stat_list(field: str, stats: dict[str, float]) -> str:
    if not stats:
        return f"  {field}: []\n"
    lines = [f"  {field}:"]
    for stat, value in stats.items():
        lines.append(f"  - Stat: {STAT[stat]}")
        lines.append(f"    Value: {num(value)}")
    return "\n".join(lines) + "\n"


def vec3(x: float, y: float, z: float) -> str:
    return f"{{x: {num(x)}, y: {num(y)}, z: {num(z)}}}"


def vec2(x: float, y: float) -> str:
    return f"{{x: {num(x)}, y: {num(y)}}}"


def color(r: float, g: float, b: float, a: float = 1.0) -> str:
    return f"{{r: {num(r)}, g: {num(g)}, b: {num(b)}, a: {num(a)}}}"


def write(relative_path: str, body: str, force: bool) -> bool:
    path = ROOT / relative_path
    if path.exists() and not force:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8")
    return True


# --- Class definitions -----------------------------------------------------
# Numbers here are first-pass balance placeholders. They live in data precisely so that tuning them
# never touches code; see GDD.md for the intent behind each role.
CLASSES = {
    "Class_Tank": dict(
        display="Guardian", role="Tank", resource="Rage",
        description="Holds enemies, soaks the damage meant for the group, and keeps threat above "
                    "everyone else's.",
        base={
            "MaxHealth": 5200, "MaxResource": 100, "ResourceRegen": 0, "MovementSpeed": 5.5,
            "AttackPower": 120, "Armor": 900, "Resistance": 300,
            "CriticalChance": 0.05, "CriticalMultiplier": 2, "ThreatModifier": 3,
        },
        growth={"MaxHealth": 60, "AttackPower": 1.5, "Armor": 9},
        preferred_range=3, turn_rate=720, radius=0.55,
        auto_attack=dict(interval=2.4, damage=45, coefficient=0.6, type="Physical",
                         range=3, label="Weapon Swing"),
    ),
    "Class_Healer": dict(
        display="Lightweaver", role="Healer", resource="Mana",
        description="Keeps the group alive, prioritises who is closest to dying, and manages a pool "
                    "that does not refill mid-fight.",
        base={
            "MaxHealth": 3000, "MaxResource": 2400, "ResourceRegen": 32, "MovementSpeed": 5.5,
            "SpellPower": 240, "Armor": 250, "Resistance": 400,
            "CriticalChance": 0.08, "CriticalMultiplier": 2, "ThreatModifier": 0.7,
        },
        growth={"MaxHealth": 32, "MaxResource": 26, "SpellPower": 3.4},
        preferred_range=24, turn_rate=540, radius=0.45,
        auto_attack=dict(interval=2.8, damage=25, coefficient=0.35, type="Magic",
                         range=25, label="Radiant Bolt"),
    ),
    "Class_MeleeDamage": dict(
        display="Bladedancer", role="MeleeDamage", resource="Energy",
        description="Sustained damage from inside melee range, at the cost of standing where most "
                    "boss mechanics land.",
        base={
            "MaxHealth": 3400, "MaxResource": 100, "ResourceRegen": 10, "MovementSpeed": 5.8,
            "AttackPower": 260, "Armor": 400, "Resistance": 250,
            "CriticalChance": 0.12, "CriticalMultiplier": 2, "ThreatModifier": 0.8,
        },
        growth={"MaxHealth": 36, "AttackPower": 3.6, "Armor": 4},
        preferred_range=3, turn_rate=720, radius=0.45,
        auto_attack=dict(interval=1.8, damage=55, coefficient=0.65, type="Physical",
                         range=3, label="Weapon Strike"),
    ),
    "Class_RangedDamage": dict(
        display="Stormcaller", role="RangedDamage", resource="Mana",
        description="Damage from a safe distance, with the mobility to keep that distance while "
                    "mechanics move the fight around.",
        base={
            "MaxHealth": 2900, "MaxResource": 2000, "ResourceRegen": 24, "MovementSpeed": 5.5,
            "SpellPower": 270, "Armor": 200, "Resistance": 300,
            "CriticalChance": 0.11, "CriticalMultiplier": 2, "ThreatModifier": 0.8,
        },
        growth={"MaxHealth": 30, "MaxResource": 22, "SpellPower": 3.8},
        preferred_range=28, turn_rate=540, radius=0.45,
        auto_attack=dict(interval=2.4, damage=40, coefficient=0.6, type="Magic",
                         range=28, label="Arcane Bolt"),
    ),
    "Class_PracticeTarget": dict(
        display="Practice Target", role="None", resource="None",
        description="A stationary target used to verify targeting, range and, from Phase 2, the "
                    "damage pipeline. Not raid content.",
        base={
            "MaxHealth": 1500, "MovementSpeed": 0, "Armor": 500, "Resistance": 500,
        },
        growth={},
        preferred_range=3, turn_rate=0, radius=0.7,
        auto_attack=dict(interval=0, damage=0, coefficient=0, type="Physical",
                         range=0, label="None"),
    ),
}


def auto_attack_block(spec: dict) -> str:
    """Renders the serialized AutoAttackData struct for a class."""
    lines = [
        "  _autoAttack:",
        "    SwingInterval: {0}".format(num(spec["interval"])),
        "    BaseDamage: {0}".format(num(spec["damage"])),
        "    PowerCoefficient: {0}".format(num(spec["coefficient"])),
        "    DamageType: {0}".format(DAMAGE_TYPE[spec["type"]]),
        "    Range: {0}".format(num(spec["range"])),
        "    Label: {0}".format(spec["label"]),
    ]
    return "\n".join(lines) + "\n"


def write_classes(force: bool) -> list[str]:
    written = []
    for name, spec in CLASSES.items():
        relative = f"{DATA}/Classes/{name}.asset"
        body = HEADER.format(script_guid=SCRIPT_GUIDS["ClassDefinition"], name=name)
        body += f"  _displayName: {spec['display']}\n"
        body += f"  _description: {spec['description']}\n"
        body += f"  _role: {ROLE[spec['role']]}\n"
        body += f"  _resourceKind: {RESOURCE[spec['resource']]}\n"
        body += stat_list("_baseStats", spec["base"])
        body += stat_list("_statsPerLevel", spec["growth"])
        body += auto_attack_block(spec["auto_attack"])
        body += f"  _preferredCombatRange: {num(spec['preferred_range'])}\n"
        body += f"  _turnRateDegrees: {num(spec['turn_rate'])}\n"
        body += f"  _radius: {num(spec['radius'])}\n"
        if write(relative, body, force):
            written.append(relative)
    return written


# --- Characters ------------------------------------------------------------
# The five names come from the design brief's demo roster. They are labels for raid frames and the
# combat log only: no gameplay code may branch on them, which is why they appear here in data and
# nowhere in the C#.
CHARACTERS = {
    "Character_Aric":   dict(display="Aric",   cls="Class_Tank",          level=60, accent=(0.29, 0.56, 0.89)),
    "Character_Lyra":   dict(display="Lyra",   cls="Class_Healer",        level=60, accent=(0.42, 0.80, 0.52)),
    "Character_Kael":   dict(display="Kael",   cls="Class_MeleeDamage",   level=60, accent=(0.91, 0.47, 0.25)),
    "Character_Elena":  dict(display="Elena",  cls="Class_RangedDamage",  level=60, accent=(0.63, 0.47, 0.91)),
    "Character_Torin":  dict(display="Torin",  cls="Class_RangedDamage",  level=60, accent=(0.35, 0.74, 0.82)),
    "Character_PracticeTarget": dict(
        display="Practice Target", cls="Class_PracticeTarget", level=60, accent=(0.55, 0.55, 0.58)),
}


def write_characters(force: bool) -> list[str]:
    written = []
    for name, spec in CHARACTERS.items():
        relative = f"{DATA}/Characters/{name}.asset"
        class_path = "{0}/Classes/{1}.asset".format(DATA, spec["cls"])
        body = HEADER.format(script_guid=SCRIPT_GUIDS["CharacterDefinition"], name=name)
        body += "  _displayName: {0}\n".format(spec["display"])
        body += "  _class: {0}\n".format(asset_ref(class_path))
        body += "  _level: {0}\n".format(spec["level"])
        body += "  _statOverrides: []\n"
        body += "  _viewPrefab: {fileID: 0}\n"
        body += "  _accentColor: {0}\n".format(color(*spec["accent"]))
        if write(relative, body, force):
            written.append(relative)
    return written


# --- Camera ----------------------------------------------------------------
def write_camera(force: bool) -> list[str]:
    relative = f"{DATA}/Bootstrap/CameraRigSettings.asset"
    body = HEADER.format(script_guid=SCRIPT_GUIDS["CameraRigSettings"], name="CameraRigSettings")
    body += f"  _projection: {PROJECTION['Orthographic']}\n"
    body += "  _fieldOfView: 35\n"
    body += "  _pitchDegrees: 50\n"
    body += "  _yawDegrees: 45\n"
    body += "  _minDistance: 12\n"
    body += "  _maxDistance: 38\n"
    body += "  _defaultDistance: 24\n"
    body += "  _zoomSpeed: 8\n"
    body += "  _zoomSmoothTime: 0.12\n"
    body += "  _followSmoothTime: 0.15\n"
    body += f"  _focusOffset: {vec3(0, 1.2, 0)}\n"
    body += "  _useBounds: 1\n"
    body += f"  _boundsCenter: {vec3(0, 0, 0)}\n"
    body += f"  _boundsSize: {vec3(70, 0, 70)}\n"
    return [relative] if write(relative, body, force) else []


# --- Combat ----------------------------------------------------------------
def write_combat_tuning(force: bool) -> list[str]:
    """Mitigation constants and the simulation's random seed.

    At 55 per level, a level-60 attacker needs the target to hold 3300 armour to halve the hit.
    """
    relative = f"{DATA}/Bootstrap/CombatTuning.asset"
    body = HEADER.format(script_guid=SCRIPT_GUIDS["CombatTuningAsset"], name="CombatTuning")
    body += "  _armorConstantPerLevel: 55\n"
    body += "  _resistanceConstantPerLevel: 55\n"
    body += "  _maximumMitigation: 0.75\n"
    body += "  _randomSeed: 1337\n"
    body += "  _useFixedSeed: 1\n"
    return [relative] if write(relative, body, force) else []


# --- Bootstrap -------------------------------------------------------------
# The player plus three practice targets. Group members and raid content join this list in Phase 4
# and Phase 7 respectively; the spawn mechanism does not change.
PRACTICE_TARGETS = [
    (-6.0, 0.0, 10.0, 180.0),
    (0.0, 0.0, 13.0, 180.0),
    (6.0, 0.0, 10.0, 180.0),
]


def spawn_entry(character: str, faction: str, position, yaw: float, indent: str = "  ") -> str:
    """One SpawnEntry, rendered as the YAML Unity writes for a serialized struct field."""
    x, y, z = position
    reference = asset_ref("{0}/Characters/{1}.asset".format(DATA, character))
    lines = [
        "{0}  Character: {1}".format(indent, reference),
        "{0}  Faction: {1}".format(indent, FACTION[faction]),
        "{0}  Position: {1}".format(indent, vec3(x, y, z)),
        "{0}  FacingYaw: {1}".format(indent, num(yaw)),
    ]
    return "\n".join(lines) + "\n"


def write_bootstrap(force: bool) -> list[str]:
    relative = f"{DATA}/Bootstrap/BootstrapConfig.asset"
    body = HEADER.format(script_guid=SCRIPT_GUIDS["BootstrapConfig"], name="BootstrapConfig")
    body += "  _cameraSettings: {0}\n".format(asset_ref(DATA + "/Bootstrap/CameraRigSettings.asset"))
    body += "  _combatTuning: {0}\n".format(asset_ref(DATA + "/Bootstrap/CombatTuning.asset"))
    # The controls asset is imported by the Input System's scripted importer, whose sub-object id
    # is assigned by the editor. It is wired up by "Raid Simulator/Repair Data References" on
    # first open rather than guessed here; see CLAUDE.md, "First time you open the project".
    body += "  _controls: {fileID: 0}\n"
    body += "  _playerSpawn:\n"
    body += spawn_entry("Character_Aric", "Raid", (0.0, 0.0, -6.0), 0.0)
    body += "  _additionalSpawns:\n"
    for x, y, z, yaw in PRACTICE_TARGETS:
        body += "  - Character: " + asset_ref(DATA + "/Characters/Character_PracticeTarget.asset") + "\n"
        body += f"    Faction: {FACTION['Enemy']}\n"
        body += f"    Position: {vec3(x, y, z)}\n"
        body += f"    FacingYaw: {num(yaw)}\n"
    body += "  _defaultActorPrefab: {fileID: 0}\n"
    body += "  _arenaPrefab: {fileID: 0}\n"
    body += f"  _placeholderFloorSize: {vec2(80, 80)}\n"
    body += "  _placeholderActorHeight: 1.9\n"
    body += "  _autoStart: 1\n"
    return [relative] if write(relative, body, force) else []


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--force", action="store_true",
                        help="Overwrite existing assets. Discards any edits made in the editor.")
    args = parser.parse_args()

    written: list[str] = []
    written += write_classes(args.force)
    written += write_characters(args.force)
    written += write_camera(args.force)
    written += write_combat_tuning(args.force)
    written += write_bootstrap(args.force)

    for relative in written:
        print(f"wrote {relative}")
    print(f"{len(written)} asset(s) written.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
