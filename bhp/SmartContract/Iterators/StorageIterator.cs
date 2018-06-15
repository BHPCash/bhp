﻿using Bhp.Core;
using Bhp.VM;
using System.Collections.Generic;

namespace Bhp.SmartContract.Iterators
{
    internal class StorageIterator : IIterator
    {
        private readonly IEnumerator<KeyValuePair<StorageKey, StorageItem>> enumerator;

        public StorageIterator(IEnumerator<KeyValuePair<StorageKey, StorageItem>> enumerator)
        {
            this.enumerator = enumerator;
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public StackItem Key()
        {
            return enumerator.Current.Key.Key;
        }

        public bool Next()
        {
            return enumerator.MoveNext();
        }

        public StackItem Value()
        {
            return enumerator.Current.Value.Value;
        }
    }
}
