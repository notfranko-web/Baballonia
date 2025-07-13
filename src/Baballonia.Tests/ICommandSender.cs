namespace Baballonia.Tests
{
    public interface ICommandSender : IDisposable
    {
        public void WriteLine(string message);
        public string ReadLine();

    }
}
