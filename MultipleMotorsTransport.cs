namespace MultipleMotorsGenericIP
{
    using Crestron.RAD.Common.Transports;

    public class MultipleMotorsTransport : ATransportDriver
    {
        public MultipleMotorsTransport()
        {
            //IsConnected = true;
        }

        public override void SendMethod(string message, object[] parameters)
        {
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }
    }
}

