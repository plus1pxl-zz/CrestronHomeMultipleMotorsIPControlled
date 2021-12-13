using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MultipleMotorsGenericIP
{
    using System;
    using System.Linq;
    using Crestron.RAD.Common;
    using Crestron.RAD.Common.Attributes.Programming;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Events;
    using Crestron.RAD.Common.Interfaces;
    using Crestron.RAD.Common.Interfaces.ExtensionDevice;
    using Crestron.RAD.Common.Transports;
    using Crestron.RAD.DeviceTypes.ExtensionDevice;
    using Crestron.SimplSharp;
    using Crestron.SimplSharpPro.CrestronThread;
    using MultipleMotorsGenericIP.Device;

	public class MultipleMotorsDriverIp : AExtensionDevice,
			//ICloudConnected,
			ITcp
	{
		private const int PollingInterval = 60000;
		private const int NumberOfMotors = 8;

		#region Commands

		// Define all of the keys to be used as commands, these match the keys in the ui definition for command actions
		private const string OpenCommand = "Open";
		private const string CloseCommand = "Close";
        private const string StopCommand = "Stop";
		private const string ToggleCommand = "Toggle";
        private const string DefaultMotorName = "Zone";

        #endregion

		#region Property Keys

		// Define all of the keys for properties, these match the properties used in the ui definition
		private const string MotorStateIconKey = "MotorStateIcon";
		private const string ErrorIcon = "icAlertRegular";
		//private const string OnIcon = "icRemoteButtonGreen";
		//private const string OffIcon = "icRemoteButtonRed";
		private const string SpinnerIcon = "icSpinner";
		private const string MotorStateKey = "MotorState";
		private const string OpenLabel = "^OpenLabel";
		private const string OpeningLabel = "^OpeningLabel";
		private const string CloseLabel = "^CloseLabel";
		private const string ClosingLabel = "^ClosingLabel";
        private const string StopLabel = "^StopLabel";
		private const string ErrorLabel = "^ErrorLabel";

        private static readonly string[] MotorNameKeys = {
            "MotorName1",
            "MotorName2",
            "MotorName3",
            "MotorName4",
            "MotorName5",
            "MotorName6",
            "MotorName7",
            "MotorName8" };

        private static readonly string[] MotorStatusKeys = {
            "MotorStatus1",
            "MotorStatus2",
            "MotorStatus3",
            "MotorStatus4",
            "MotorStatus5",
            "MotorStatus6",
            "MotorStatus7",
            "MotorStatus8" };

		private static readonly string[] MotorStateKeys = {
            "MotorState1",
            "MotorState2",
            "MotorState3",
            "MotorState4",
            "MotorState5",
            "MotorState6",
            "MotorState7",
            "MotorState8" };

        private static readonly string[] MotorVisibilityKeys = {
            "MotorVisible1",
            "MotorVisible2",
            "MotorVisible3",
            "MotorVisible4",
            "MotorVisible5",
            "MotorVisible6",
            "MotorVisible7",
            "MotorVisible8" };

        #endregion

        #region Translation Keys

        // Define all of the keys to be used for translations, these match the keys in the translation files
        private const string MotorStateTranslationKey = "OnMotorStateLabel";

        #endregion

		#region Programming

		private const string OnLabel = "^OnLabel";
        private const string OffLabel = "^OffLabel";

        #endregion

		#region Fields

		private bool _consoleDebuggingEnabled;
		private string _openIcon;
		private string _closeIcon;

        private Motor[] _motorEmulators = new Motor[NumberOfMotors];
        //private List<MotorEmulator> _motorEmulators = new List<MotorEmulator>()
        //{
        //    new MotorEmulator()
        //};

        private MultipleMotorsProtocol _motorsProtocol;
		private MultipleMotorsTransport _motorsTransport;
		TcpTransport _tcpTransport;

		private PropertyValue<string> _motorStateProperty;
		private PropertyValue<string> _motorStateIconProperty;

        private PropertyValue<string>[] _motorNamesPropertyValues = new PropertyValue<string>[NumberOfMotors];
        private PropertyValue<string>[] _motorStatusPropertyValues = new PropertyValue<string>[NumberOfMotors];
        private PropertyValue<bool>[] _motorStatePropertyValues = new PropertyValue<bool>[NumberOfMotors];
        private PropertyValue<bool>[] _motorVisibilityPropertyValues = new PropertyValue<bool>[NumberOfMotors];

		private PropertyValue<bool> _autoOffProperty;
		private PropertyValue<int> _autoOffTimeProperty;

		#endregion

		#region Constructor

		public MultipleMotorsDriverIp()
			: base()
		{
			this._consoleDebuggingEnabled = true;
			AddUserAttributes();
			CreateDeviceDefinition();
		}

		#endregion

		#region Property
		protected MultipleMotorsProtocol Protocol
		{
			get { return _motorsProtocol; }
			set
			{
				if (_motorsProtocol != null)
				{
					_motorsProtocol.ConnectedChanged -= ProtocolConnectedChanged;
				}

				_motorsProtocol = value;
				DeviceProtocol = _motorsProtocol;

				if (value != null)
				{
					_motorsProtocol.ConnectedChanged -= ProtocolConnectedChanged;
					_motorsProtocol.ConnectedChanged += ProtocolConnectedChanged;
				}
			}
		}

		#endregion

		#region AExtensionDevice Members

		protected override IOperationResult SetDriverPropertyValue<T>(string propertyKey, T value)
		{
            for (int i = 0; i < NumberOfMotors; i++)
            {
                if (propertyKey == MotorStateKeys[i])
                {
                    var state = value as bool?;
                    if (state == null)
                        return new OperationResult(OperationResultCode.Error,
                            "The value provided could not be converted to a bool.");
                    else if (state == true)
                        _motorEmulators[i].Open();
                    else if (state == false)
                        _motorEmulators[i].Close();
                    return new OperationResult(OperationResultCode.Success);
                }

            }

            return new OperationResult(OperationResultCode.Error, "The property does not exist.");
		}

		protected override IOperationResult SetDriverPropertyValue<T>(string objectId, string propertyKey, T value)
		{
			throw new System.NotImplementedException();
		}


		protected override IOperationResult DoCommand(string command, string[] parameters)
		{
			// ReSharper disable once ObjectCreationAsStatement
			new Thread(DoCommandThreadCallback, new DoCommandObject(command, parameters));
			return new OperationResult(OperationResultCode.Success);
		}

		#endregion

		#region ITcp Members

		void ITcp.Initialize(IPAddress ipAddress, int port)
		{
			Initialize(ipAddress, port);
		}

		public void Initialize(IPAddress ipAddress, int port)
		{

			try
			{
				_tcpTransport = new TcpTransport
				{
					EnableAutoReconnect = this.EnableAutoReconnect,
					EnableLogging = this.InternalEnableLogging,
					CustomLogger = this.InternalCustomLogger,
					EnableRxDebug = this.InternalEnableRxDebug,
					EnableTxDebug = this.InternalEnableTxDebug
				};

				TcpTransport tcpTransport = _tcpTransport;
				tcpTransport.Initialize(ipAddress, port);
				this.ConnectionTransport = tcpTransport;
				tcpTransport.DriverID = DriverID;
				base.UserAttributesChanged += MotorDriverIpUserAttributesChanged;

				_motorsProtocol = new MultipleMotorsProtocol(this.ConnectionTransport, base.Id, PollingInterval, _consoleDebuggingEnabled)
				{
					EnableLogging = this.InternalEnableLogging,
					CustomLogger = this.InternalCustomLogger
				};

				base.DeviceProtocol = _motorsProtocol;
				base.DeviceProtocol.Initialize(DriverData);
				_motorsProtocol.RxOut += SendRxOut;
				_motorsProtocol.ConnectedChanged += ProtocolConnectedChanged;
				_motorsProtocol.UserAttributeChanged += ProtocolAttributeChanged;
				_motorsProtocol.FeedbackChanged += ProtocolFeedbackChanged;

                for (int i = 0; i < NumberOfMotors; i++)
                {
					_motorEmulators[i] = new Motor();
                    _motorEmulators[i].StateChangedEvent += MotorEmulatorMotorStateChangedEvent;
				}
            }
			catch (Exception ex)
			{
				CrestronConsole.PrintLine("MotorProtocol Error: {0}", ex.Message);

				if (EnableLogging)
				{
					Log(string.Format("MotorProtocol Error: {0}", ex.Message));
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MotorDriverIpUserAttributesChanged(object sender, UserAttributeListEventArgs e)
		{
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetUserAttribute {sender.ToString()} to bool:{e.ToString()}");
        }

		#endregion

		#region IConnection Members

		public override void Connect()
		{
			base.Connect();
			UpdateUserAttributes();
			//CrestronConsole.PrintLine("In Connect()");
			Refresh();
		}

		public override void Reconnect()
		{
			base.Reconnect();
			//CrestronConsole.PrintLine("In Reconnect()");
			UpdateUserAttributes();
		}


		#endregion

		#region Programmable Operations
		

		#endregion

		#region Programmable Events


        #endregion

		#region Private Methods


		private void CreateDeviceDefinition()
		{

            for (int i = 0; i < NumberOfMotors; i++)
            {
                _motorNamesPropertyValues[i] =
                    CreateProperty<string>(new PropertyDefinition(MotorNameKeys[i], null, DevicePropertyType.String));

                _motorStatusPropertyValues[i] =
                    CreateProperty<string>(new PropertyDefinition(MotorStatusKeys[i], null, DevicePropertyType.String));

				_motorStatePropertyValues[i] =
                    CreateProperty<bool>(new PropertyDefinition(MotorStateKeys[i], null, DevicePropertyType.Boolean));

                _motorVisibilityPropertyValues[i] =
                    CreateProperty<bool>(new PropertyDefinition(MotorVisibilityKeys[i], null, DevicePropertyType.Boolean));
			}

			// Define the state property
			_motorStateProperty = CreateProperty<string>(
				new PropertyDefinition(MotorStateKey, null, DevicePropertyType.String));

			// Define the state icon property
			_motorStateIconProperty = CreateProperty<string>(new PropertyDefinition(MotorStateIconKey, null, DevicePropertyType.String));
        }

		/// <summary>
		/// Update the state of all properties.
		/// </summary>
		private void Refresh()
		{
            for (int i = 0; i < NumberOfMotors; i++)
            {
				if (_motorEmulators[i] == null)
                    return;
			}

            Commit();
		}


		private object DoCommandThreadCallback(object userSpecific)
		{
			var doCommandObject = (DoCommandObject)userSpecific;
			// command without the motor number
			var command = Regex.Match(doCommandObject.Command, @"^\D+").Value;
			//var parameters = doCommandObject.Parameters;
			var motorNumberString = Regex.Match(doCommandObject.Command, @"\d+$").Value;
            int.TryParse(motorNumberString, out int motorNumber);
            int motorEmulatorIndex = motorNumber - 1;

			CrestronConsole.PrintLine($"DoCommandThreadCallback command: {command} motorEmulatorIndex - {motorEmulatorIndex}");

            switch (command)
            {
                case OpenCommand:
                    _motorEmulators[motorEmulatorIndex].Open();
                    break;

                case CloseCommand:
                    _motorEmulators[motorEmulatorIndex].Close();
                    break;

                case StopCommand:
                    _motorEmulators[motorEmulatorIndex].Stop();
                    break;
            }

            _motorsProtocol.SendCustomCommandByName(command);

			return null;
		}

		private void ProtocolFeedbackChanged(object sender, MotorsStatesEventArgs e)
		{
			foreach (var motorState in e.MotorsStates)
			{
                if (motorState.Value == MotorState.Open)
                {
                    _motorEmulators[motorState.Key - 1].SetOpen();
                }
                else if (motorState.Value == MotorState.Close)
                {
                    _motorEmulators[motorState.Key - 1].SetClose();
                }
                else if (motorState.Value == MotorState.Error)
                {
                    _motorEmulators[motorState.Key - 1].MotorState = MotorState.Error;
                }

            }
		}

		private void MotorEmulatorMotorStateChangedEvent(object sender, StateChangeEventArgs stateChangeEventArgs)
		{
			switch (stateChangeEventArgs.EventType)
			{
				case EventType.MotorStateChanged:
                    var state = (MotorState)stateChangeEventArgs.EventData;
                    int motorEmulatorIndex = DetermineMotorEmulatorIndex(sender, state); ;
                    SetMotorState(motorEmulatorIndex, state);
					break;
            }

			Commit();
		}

		private int DetermineMotorEmulatorIndex(object sender, MotorState state)
		{
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"DetermineMotorEmulatorIndex {state}. Sender: {sender}");
			var eventSender = (Motor)sender;
			int motorEmulatorIndex = 0;

            for (int i = 0; i < NumberOfMotors; i++)
            {
				if (eventSender == _motorEmulators[i])
                {
                    motorEmulatorIndex = i;
                }
			}

            return motorEmulatorIndex;
        }

		//process feedback
		private void SetMotorState(int motorEmulatorIndex, MotorState state)
        {
            var motorNumber = motorEmulatorIndex + 1;
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"SetMotorState on motor {motorNumber}. State: {state}");
			switch (state)
			{
				case MotorState.Open:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = OpenLabel;
                    _motorStatePropertyValues[motorEmulatorIndex].Value = true;
					break;
				case MotorState.Opening:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = OpeningLabel;
					_motorsProtocol.SendCustomCommandByName($"Open{motorNumber}");
					break;
				case MotorState.Close:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = CloseLabel;
                    _motorStatePropertyValues[motorEmulatorIndex].Value = false;
					break;
				case MotorState.Closing:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = ClosingLabel;
					_motorsProtocol.SendCustomCommandByName($"Close{motorNumber}");
					break;
                case MotorState.Stop:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = StopLabel;
                    _motorsProtocol.SendCustomCommandByName($"Stop{motorNumber}");
                    break;
				case MotorState.Error:
                    _motorStatusPropertyValues[motorEmulatorIndex].Value = ErrorLabel;
					break;
			}

            _motorStateIconProperty.Value = _closeIcon;
            _motorStateProperty.Value = CloseLabel;

			for (int i = 0; i < NumberOfMotors; i++)
            {
                if (_motorStatePropertyValues[i].Value)
                {
					_motorStateIconProperty.Value = _openIcon;
                    _motorStateProperty.Value = OpenLabel;
				}
            }
			Commit();
        }

		private void AddUserAttributes()
		{
			AddUserAttribute(
				UserAttributeType.Custom,
				"openIcon",
				"Status Open Icon",
				"Enter icon name from available list of extension device icons. Leave empty for default icon",
				true,
				UserAttributeRequiredForConnectionType.None,
				UserAttributeDataType.String,
				"icShadesOpen");

			AddUserAttribute(
				UserAttributeType.Custom,
				"closeIcon",
				"Status Close Icon",
				"Enter icon name from available list of extension device icons. Leave empty for default icon",
				true,
				UserAttributeRequiredForConnectionType.None,
				UserAttributeDataType.String,
				"icShadesClosedDisabled");

            for (int i = 1; i < NumberOfMotors + 1; i++)
			{
				AddUserAttribute(
					UserAttributeType.Custom,
					$"MotorNameProperty{i}",
					$"Name for motor 0{i}",
					$"Enter the desired name of motor 0{i}",
					true,
					UserAttributeRequiredForConnectionType.None,
					UserAttributeDataType.String,
					$"{DefaultMotorName} {i}");
			}

			UpdateUserAttributes();
            
        }

        private void UpdateUserAttributes()
		{
			var userAttributes = RetrieveUserAttributes();

			var openIconKeyValue = userAttributes
				.FirstOrDefault(x => x.ParameterId == "openIcon");
			if (openIconKeyValue != null)
			{
				_openIcon = openIconKeyValue.Data.DefaultValue;
				if (_consoleDebuggingEnabled) CrestronConsole.PrintLine(_openIcon);
			}

			var closeIconKeyValue = userAttributes
				.FirstOrDefault(x => x.ParameterId == "closeIcon");
			if (closeIconKeyValue != null)
			{
				_closeIcon = closeIconKeyValue.Data.DefaultValue;
				if (_consoleDebuggingEnabled) CrestronConsole.PrintLine(_closeIcon);
			}
		}

        private void SetMotorVisibility(int i, string attributeValue)
        {
            var userAttributes = RetrieveUserAttributes();

            var defaultMotorName = $"{DefaultMotorName} {i + 1}";
            string motorName = attributeValue;

			_motorVisibilityPropertyValues[i].Value = (motorName == defaultMotorName) ? false : true;

            //CrestronConsole.PrintLine($"motorName == defaultMotorName | _motorVisibilityPropertyValues[{i}].Value = {_motorVisibilityPropertyValues[i].Value}");

		}

		public object GetPropertyValue(object userAttribute, string propertyName)
		{
			return userAttribute.GetType().GetProperties()
				.Single(pi => pi.Name == propertyName)
				.GetValue(userAttribute, null);
		}
		#endregion

		#region IConnection3 Members

		protected virtual void ProtocolConnectedChanged(object driver, ValueEventArgs<bool> e)
		{
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine("ConnectedChanged Event");
			if (e != null)
				Connected = e.Value;
        }

		protected virtual void ProtocolAttributeChanged(object driver, ValueEventArgs<string[]> e)
		{
			if (_consoleDebuggingEnabled) CrestronConsole.PrintLine($"ProtocolAttributeChanged Event");

			if (e != null)
			{
				var attributeId = e.Value[0];
				var attributeValue = e.Value[1];

				if (attributeId == "openIcon")
				{
					this._openIcon = attributeValue;
					CrestronConsole.PrintLine($"openIcon changed to: {attributeValue}");
				}
				else if (attributeId == "closeIcon")
				{
					this._closeIcon = attributeValue;
					CrestronConsole.PrintLine($"closeIcon changed to: {attributeValue}");
				}
				else if (attributeId.Contains("MotorNameProperty"))
				{
                    for (int i = 0; i < NumberOfMotors; i++)
                    {
                        if (attributeId == $"MotorNameProperty{i + 1}")
                        {
                            _motorNamesPropertyValues[i].Value = attributeValue;

                            SetMotorVisibility(i, attributeValue);
						}
                    }
                }
			}

			Commit();
		}
		public static string ToLowerFirstChar(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;

			return char.ToLower(input[0]) + input.Substring(1);
		}
		#endregion
	}

	internal class DoCommandObject
	{
		public DoCommandObject(string command, string[] parameters)
		{
			Command = command;
			Parameters = parameters;
		}

		public string Command;
		public string[] Parameters;
	}
}
