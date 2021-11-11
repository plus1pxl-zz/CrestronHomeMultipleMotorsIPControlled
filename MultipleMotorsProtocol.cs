namespace MultipleMotorsGenericIP
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Crestron.RAD.Common;
    using Crestron.RAD.Common.BasicDriver;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Events;
    using Crestron.RAD.Common.Transports;
    using Crestron.SimplSharp;
    using Crestron.RAD.Common.Interfaces;

    public class MultipleMotorsProtocol : ABaseDriverProtocol, IDisposable
    {
        #region Fields

        private int _numberOfMotors = 8;
        private bool _isConnected;
        private bool _consoleDebuggingEnabled;
        private MotorState _motorState;

        #endregion

        #region Events

        public event EventHandler<ValueEventArgs<bool>> ConnectedChanged;
        public event EventHandler<ValueEventArgs<string[]>> UserAttributeChanged;
        public event EventHandler<MotorsStatesEventArgs> FeedbackChanged;

        #endregion

        #region Constructor

        public MultipleMotorsProtocol(ISerialTransport transport, byte id)
            : base(transport, id)
        {
        }

        public MultipleMotorsProtocol(ISerialTransport transport, byte id, int pollingInterval)
            : base(transport, id)
        {
            base.PollingInterval = pollingInterval;
        }
        public MultipleMotorsProtocol(ISerialTransport transport, byte id, int pollingInterval, bool consoleDebuggingEnabled)
            : base(transport, id)
        {
            base.PollingInterval = pollingInterval;
            this._consoleDebuggingEnabled = consoleDebuggingEnabled;
        }
        #endregion

        #region Property

        public Dictionary<StandardCommandsEnum, string> CommandsDictionary
        {
            get;
            protected internal set;
        }

        #endregion

        protected override void ConnectionChangedEvent(bool connection)
        {
            if (ConnectedChanged != null)
                ConnectedChanged(null, new ValueEventArgs<bool>(connection));
        }

        protected override void MessageTimedOut(string lastSentCommand)
        {
            LogMessage("MultipleMotorsProtocol, MessageTimedOut: " + lastSentCommand);
        }

        protected override void ConnectionChanged(bool connection)
        {
            if (connection == _isConnected) return;
            _isConnected = connection;
            base.ConnectionChanged(connection);
            if (connection) Poll();
            // debugging
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"_isConnected field is set to:{_isConnected}");
        }

        protected override void Poll()
        {
            this.SendCustomCommandByName("StatePoll");
        }

        protected override bool PrepareStringThenSend(CommandSet commandSet)
        {
            if (!commandSet.CommandPrepared)
            {
                commandSet.Command = $"{commandSet.Command}\x0D";
                commandSet.CommandPrepared = true;
            }
            return base.PrepareStringThenSend(commandSet);
        }

        protected override void ChooseDeconstructMethod(ValidatedRxData validatedData)
        {
        }

        public override void DataHandler(string rx)
        {
            char[] separator = new[] { '\x0D' };
            var motorsStatus = rx.Split(separator, StringSplitOptions.RemoveEmptyEntries).ToList();
            MotorsStatesEventArgs motorsStatesEventArgs = new MotorsStatesEventArgs();


            foreach (var motorStatus in motorsStatus)
            {
                //System.Text.RegularExpressions.Regex
                var motorNumber = int.Parse(Regex.Match(motorStatus, @"\d+").Value);
                var motorState = MotorState.Error;

                if (motorStatus.ToLower().Contains("is open")) // && _motorState != MotorState.SetOpen
                {
                    motorState = MotorState.Open;
                }
                else if (motorStatus.ToLower().Contains("is closed")) //  && _motorState != MotorState.SetClose
                {
                    motorState = MotorState.Close;
                }
                else if (motorStatus.ToLower().Contains("is stop")) //  && _motorState != MotorState.SetStop
                {
                    motorState = MotorState.Stop;
                }

                //if (_consoleDebuggingEnabled)
                //{
                //    CrestronConsole.PrintLine($"DataHandler Method motorStatus{motorStatus} | motorNumber={motorNumber} | motorState={motorState}");
                //}

                try
                {
                    motorsStatesEventArgs.MotorsStates.Add(motorNumber, motorState);
                }
                catch (Exception exception)
                {
                    CrestronConsole.Print(exception.Message);
                    throw;
                }

            }

            FeedbackChanged?.Invoke(this, motorsStatesEventArgs);

        }

        public override void SetUserAttribute(string attributeId, string attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to string:{attributeValue}");
            if (attributeId != null)
            {
                if (attributeId == "openIcon" ||
                    attributeId == "closeIcon" ||
                    attributeId == "MotorNameProperty1" ||
                    attributeId == "MotorNameProperty2" ||
                    attributeId == "MotorNameProperty3" ||
                    attributeId == "MotorNameProperty4" ||
                    attributeId == "MotorNameProperty5" ||
                    attributeId == "MotorNameProperty6" ||
                    attributeId == "MotorNameProperty7" ||
                    attributeId == "MotorNameProperty8")
                {

                    string[] attributeData = { attributeId, attributeValue };
                    if (UserAttributeChanged != null)
                        this.UserAttributeChanged.Invoke(this, new ValueEventArgs<string[]>(attributeData));
                }
            }
        }


        public override void SetUserAttribute(string attributeId, ushort attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to ushort:{attributeValue}");
        }

        public override void SetUserAttribute(string attributeId, bool attributeValue)
        {
            if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {attributeId} to bool:{attributeValue}");
        }

        #region Logging

        internal void LogMessage(string message)
        {
            if (!EnableLogging) return;

            if (CustomLogger == null)
            {
                CrestronConsole.PrintLine(message);
            }
            else
            {
                CustomLogger(message + "\n");
            }
        }

        public List<UserAttribute> RetrieveUserAttributes()
        {
            throw new NotImplementedException();
        }

        #endregion Logging
    }

    public class MotorsStatesEventArgs : EventArgs
    {
        public Dictionary<int, MotorState> MotorsStates { get; set; }

        public MotorsStatesEventArgs()
        {
            this.MotorsStates = new Dictionary<int, MotorState>();
        }
        //public MotorState motorState { get; set; }
        //public int motorNumber { get; set; }
    }
}
