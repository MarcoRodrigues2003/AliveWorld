using System.Collections.Generic;
using UnityEngine;

namespace AliveWorld.World
{
    [RequireComponent(typeof(WorldBlackboard))]
    public sealed class WorldBlackboardBootstrap : MonoBehaviour
    {
        public List<WorldAnnouncementDefinition> definitions = new();

        private void Awake()
        {
            var bb = GetComponent<WorldBlackboard>();

            // If already populated, do nothing.
            if (bb.Announcements.Count > 0)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                if (def == null) continue;

                bb.Register(def, def.activeByDefault, 1f);
            }
        }
    }
}
