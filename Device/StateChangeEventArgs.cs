using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultipleMotorsGenericIP.Device
{
    public class StateChangeEventArgs : EventArgs
    {
        public StateChangeEventArgs(EventType eventType, object eventData)
        {
            EventType = eventType;
            EventData = eventData;
        }

        public EventType EventType { get; private set; }

        public object EventData { get; private set; }
    }
}
