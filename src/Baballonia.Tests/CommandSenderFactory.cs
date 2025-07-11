using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class CommandSenderFactory
    {
        public ICommandSender Create(string port)
        {
            return new SerialCommandSender(port);
        }
    }
}
