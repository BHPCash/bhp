using Bhp.Core;
using System;

namespace Bhp.Implementations.Blockchains.LevelDB
{
    public class ApplicationExecutedEventArgs : EventArgs
    {
        public Transaction Transaction { get; internal set; }
        public ApplicationExecutionResult[] ExecutionResults { get; internal set; }
    }
}
