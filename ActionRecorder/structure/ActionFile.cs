using System;
using System.Collections.Generic;
using System.Linq;
using Loamen.KeyMouseHook;

namespace ActionRecorder.structure
{
    [Serializable]
    public class ActionFile
    {
        public string Name = "RecordedAction";
        public DateTime RecordedDate = DateTime.Now;
        public List<MacroEvent> Actions = new List<MacroEvent>();
        public bool Loop = true;
        
        public int TotalTime => Actions.Sum(x => x.TimeSinceLastEvent);
    }
}