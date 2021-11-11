namespace MultipleMotorsGenericIP.Device
{
    using System;
    using Crestron.SimplSharp;
    using MultipleMotorsGenericIP.Device;

	public class Motor
    {
		public event EventHandler<StateChangeEventArgs> StateChangedEvent;

        private MotorState _MotorState = MotorState.Na;

        public Motor()
		{
        }

		public MotorState MotorState
		{
			get { return _MotorState; }
			set
			{
				if (_MotorState == value)
					return;

				_MotorState = value;
				StateChangedEvent?.Invoke(this, new StateChangeEventArgs(EventType.MotorStateChanged, _MotorState));
			}
		}

        public void Close()
		{
			if (_MotorState == MotorState.Close || _MotorState == MotorState.Closing)
				return;

            // Set state to turing off and reset the auto off timer
			MotorState = MotorState.Closing;
            CreateNewMotorEvent(MotorState.Closing, true, DateTime.Now);
		}

        public void Stop()
        {
            if (_MotorState == MotorState.Stop)
                return;

            // Set state to turing off and reset the auto off timer
            MotorState = MotorState.Stop;
            CreateNewMotorEvent(MotorState.Stop, true, DateTime.Now);

		}

		public void Open()
		{
			if (_MotorState == MotorState.Open || _MotorState == MotorState.Opening)
				return;

			// Set the state
			MotorState = MotorState.Opening;
            CreateNewMotorEvent(MotorState.Opening, true, DateTime.Now);
		}


        public void SetClose()
		{
			MotorState = MotorState.Close;
			CreateNewMotorEvent(MotorState.Close, true, DateTime.Now);
		}

		public void SetOpen()
		{
			MotorState = MotorState.Open;
			CreateNewMotorEvent(MotorState.Open, true, DateTime.Now);
        }

		private void CreateNewMotorEvent(MotorState eventType, bool success, DateTime time)
		{
			var MotorEvent = new MotorEvent(eventType, success, time);

			StateChangedEvent?.Invoke(this, new StateChangeEventArgs(EventType.MotorEvent, MotorEvent));
		}
    }

	public class MotorEvent
	{
		public MotorEvent(MotorState eventType, bool success, DateTime time)
		{
			EventType = eventType;
			Success = success;
			Time = time;
			Summary = $"{eventType} at {time.ToShortTimeString()}";
		}

		public MotorState EventType { get; set; }
		public bool Success { get; set; }
		public DateTime Time { get; set; }
		public string Summary { get; set; }
	}
}
