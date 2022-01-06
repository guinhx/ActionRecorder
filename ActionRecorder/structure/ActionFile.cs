using System;
using System.Linq;
using Loamen.KeyMouseHook;

namespace ActionRecorder.structure
{
    public class ActionFile
    {

        public string Name = "RecordedAction";
        public DateTime RecordedDate = DateTime.Now;
        public int ActionsSize = 0;
        public MacroEvent[] Actions = new MacroEvent[Application.ACTIONS_SIZE];
        public bool Loop = true;

        public int TotalTime => Actions.Sum(x => x.TimeSinceLastEvent);
    }
}