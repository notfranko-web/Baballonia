namespace Baballonia.Contracts
{
    // maybe this shouldn't be here, but i dont like to have a separate file for 5 LOC
    public enum CommandSenderType
    {
        Serial,
        Wifi
    }

    // this interface exists so we can easier mock factory it in tests
    public interface ICommandSenderFactory
    {
        public ICommandSender Create(CommandSenderType type, string port);
    }
}
