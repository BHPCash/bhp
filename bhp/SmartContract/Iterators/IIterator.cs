using Bhp.SmartContract.Enumerators;
using Bhp.VM;

namespace Bhp.SmartContract.Iterators
{
    internal interface IIterator : IEnumerator
    {
        StackItem Key();
    }
}
