using System.Collections.Generic;
using Verse;

namespace AutoToolSwitcher
{
    public class GameComponent_ToolTracker : GameComponent
    {
        public ToolPolicyDatabase toolPolicyDatabase;
        public Dictionary<Pawn, Pawn_ToolPolicyTracker> trackers;
        public static GameComponent_ToolTracker Instance;

        private void PreInit()
        {
            Instance = this;
            if (trackers is null)
            {
                trackers = new Dictionary<Pawn, Pawn_ToolPolicyTracker>();
            }
            if (toolPolicyDatabase is null)
            {
                toolPolicyDatabase = new ToolPolicyDatabase();
            }
        }
        public GameComponent_ToolTracker(Game game)
        {
            PreInit();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            PreInit();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            PreInit();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref trackers, "trackers", LookMode.Reference, LookMode.Deep, ref pawnKeys, ref trackerValues);
            Scribe_Deep.Look(ref toolPolicyDatabase, "toolPolicyDatabase");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PreInit();
            }
        }

        private List<Pawn> pawnKeys;
        private List<Pawn_ToolPolicyTracker> trackerValues;
    }
}
