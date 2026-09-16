using RaidSim.Characters.Data;
using RaidSim.Characters.Runtime;
using RaidSim.Core.Common;
using RaidSim.Core.Diagnostics;
using RaidSim.Core.Simulation;
using UnityEngine;

namespace RaidSim.Game.Bootstrap
{
    /// <summary>
    /// Creates <see cref="CombatActor"/> GameObjects from authored spawn entries.
    /// </summary>
    /// <remarks>
    /// <para>One spawner serves every kind of participant. A group member, a trash mob and the boss
    /// differ only in the data handed to it, which is what keeps "add a new enemy" an authoring task
    /// rather than a programming one.</para>
    /// <para>When a character has no view prefab the spawner builds a labelled placeholder body so
    /// the project is playable before any art exists. Replacing it is a matter of assigning a prefab
    /// — see <c>BLENDER_PIPELINE.md</c>.</para>
    /// </remarks>
    public sealed class ActorSpawner
    {
        private readonly SimulationContext _context;
        private readonly GameObject _defaultPrefab;
        private readonly float _placeholderHeight;
        private readonly Transform _parent;

        public ActorSpawner(SimulationContext context, GameObject defaultPrefab, float placeholderHeight, Transform parent)
        {
            _context = context;
            _defaultPrefab = defaultPrefab;
            _placeholderHeight = Mathf.Max(0.2f, placeholderHeight);
            _parent = parent;
        }

        /// <summary>
        /// Spawns one entry. Returns null when the entry is empty or its data is incomplete.
        /// </summary>
        public CombatActor Spawn(in SpawnEntry entry)
        {
            if (!entry.IsValid)
            {
                return null;
            }

            CharacterDefinition definition = entry.Character;
            string problem = definition.Validate();
            if (problem != null)
            {
                _context.Log.Error(LogChannel.Bootstrap, problem);
                return null;
            }

            GameObject prefab = definition.ViewPrefab != null ? definition.ViewPrefab : _defaultPrefab;
            GameObject instance = prefab != null
                ? Object.Instantiate(prefab, _parent)
                : BuildPlaceholderBody(definition);

            instance.name = $"Actor_{definition.name}";
            instance.transform.SetParent(_parent, worldPositionStays: true);

            var actor = instance.GetComponent<CombatActor>();
            if (actor == null)
            {
                actor = instance.AddComponent<CombatActor>();
            }

            EnsureController(instance, definition);
            actor.Initialise(_context, definition, entry.Faction);

            if (!actor.IsInitialised)
            {
                Object.Destroy(instance);
                return null;
            }

            actor.PlaceAt(entry.Position, entry.FacingDirection);
            return actor;
        }

        /// <summary>Spawns every valid entry in <paramref name="entries"/>.</summary>
        public int SpawnAll(SpawnEntry[] entries)
        {
            if (entries == null)
            {
                return 0;
            }

            int spawned = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (Spawn(entries[i]) != null)
                {
                    spawned++;
                }
            }

            return spawned;
        }

        /// <summary>
        /// Makes sure the instance has a correctly sized <see cref="CharacterController"/>.
        /// </summary>
        /// <remarks>
        /// The controller doubles as the click-targeting collider, so its dimensions must match the
        /// footprint the simulation uses for range checks or the two would disagree.
        /// </remarks>
        private void EnsureController(GameObject instance, CharacterDefinition definition)
        {
            var controller = instance.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = instance.AddComponent<CharacterController>();
            }

            float radius = definition.Class.Radius;
            controller.radius = radius;
            controller.height = Mathf.Max(_placeholderHeight, radius * 2f);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        private GameObject BuildPlaceholderBody(CharacterDefinition definition)
        {
            var root = new GameObject("PlaceholderBody");
            root.transform.SetParent(_parent, worldPositionStays: false);

            // The visual is a child so that swapping in a real model later is a parenting change and
            // nothing else; the root keeps the controller, the collider and the actor component.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, worldPositionStays: false);

            float radius = definition.Class != null ? definition.Class.Radius : 0.5f;
            float height = Mathf.Max(_placeholderHeight, radius * 2f);
            visual.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            visual.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            // The capsule's own collider would fight the CharacterController for the targeting ray.
            Object.Destroy(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = definition.AccentColor;
            }

            return root;
        }
    }
}
