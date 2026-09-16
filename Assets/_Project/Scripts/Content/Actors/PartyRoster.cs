using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// Which classes fill the five party slots, and which slot the human drives.
    ///
    /// Keeping this as data rather than as scene setup is what lets the same run
    /// be played solo with four companions today and by five humans later: the
    /// only thing that changes is who supplies input for a slot.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Party Roster", fileName = "PR_NewRoster", order = 2)]
    public sealed class PartyRoster : ScriptableObject
    {
        [Tooltip("One entry per party slot, in party-frame order. Five is the tuned size.")]
        public List<ClassDefinition> Slots = new List<ClassDefinition>();

        [Tooltip("Index of the slot the local player controls. The rest run on companion AI.")]
        [Min(0)] public int LocalPlayerSlot;

        [Tooltip("Names shown on the party frames. Falls back to the class name when empty.")]
        public List<string> MemberNames = new List<string>();

        public int Count => Slots.Count;

        public ClassDefinition ClassAt(int index) =>
            index >= 0 && index < Slots.Count ? Slots[index] : null;

        public string NameAt(int index)
        {
            if (index >= 0 && index < MemberNames.Count && !string.IsNullOrWhiteSpace(MemberNames[index]))
                return MemberNames[index];

            ClassDefinition c = ClassAt(index);
            return c != null ? c.DisplayName : $"Slot {index + 1}";
        }

        /// <summary>
        /// True when the roster has at least one tank and one healer. Not enforced —
        /// an all-rogue party is a legitimate challenge run — but the run setup UI
        /// warns about it.
        /// </summary>
        public bool IsBalanced()
        {
            bool tank = false, healer = false;
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == null) continue;
                if (Slots[i].Role == ActorRole.Tank) tank = true;
                if (Slots[i].Role == ActorRole.Healer) healer = true;
            }
            return tank && healer;
        }

        private void OnValidate()
        {
            if (Slots.Count > 0) LocalPlayerSlot = Mathf.Clamp(LocalPlayerSlot, 0, Slots.Count - 1);
        }
    }
}
