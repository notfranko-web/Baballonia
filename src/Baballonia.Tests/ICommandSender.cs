using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public interface ICommandSender : IDisposable
    {
        public void WriteLine(string message);
        public string ReadLine();

    }
}
