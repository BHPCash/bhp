using Bhp.VM;
using System;

namespace Bhp.SmartContract.Enumerators
{
    internal interface IEnumerator : IDisposable, IInteropInterface
    {
        bool Next();
        StackItem Value();
    }
}
