// Minimal Unity Input System surface used for headless type-checking. See README.md.
#pragma warning disable CA1050, IDE0060

namespace UnityEngine.InputSystem
{
    public class InputAction
    {
        public string name { get; }

        public bool enabled { get; }

        public void Enable() { }

        public void Disable() { }

        public TValue ReadValue<TValue>() where TValue : struct => default;

        public bool IsPressed() => false;

        public bool WasPressedThisFrame() => false;

        public bool WasReleasedThisFrame() => false;

        public bool WasPerformedThisFrame() => false;
    }

    public class InputActionMap
    {
        public string name { get; }

        public bool enabled { get; }

        public void Enable() { }

        public void Disable() { }

        public InputAction FindAction(string nameOrId, bool throwIfNotFound = false) => default;
    }

    public class InputActionAsset : ScriptableObject
    {
        public void Enable() { }

        public void Disable() { }

        public InputActionMap FindActionMap(string nameOrId, bool throwIfNotFound = false) => default;

        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => default;
    }
}
