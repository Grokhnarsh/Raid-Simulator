using UnityEngine;

namespace EmberDepths.Gameplay.Presentation
{
    /// <summary>
    /// Lifetime helpers for the throwaway GameObjects the simulation creates —
    /// hazard decals, telegraph warnings, actor views.
    ///
    /// <see cref="Object.Destroy"/> throws outside play mode. That matters more
    /// than it sounds: it is what stops the simulation being driven headlessly
    /// from an editor tool or a test, which is exactly where a balance pass or a
    /// thousand-seed generator sweep wants to run it.
    /// </summary>
    public static class ViewObjects
    {
        public static void Destroy(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
