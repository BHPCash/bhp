using Bhp.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bhp.UnitTests
{
    public class TestTransaction : Transaction
    {
        public TestTransaction(UInt256 assetId, TransactionType type, UInt160 scriptHash) : base(type)
        {
            TransactionOutput transVal = new TransactionOutput();
            transVal.Value = Fixed8.FromDecimal(50);
            transVal.AssetId = assetId;
            transVal.ScriptHash = scriptHash;
            base.Outputs = new TransactionOutput[1] { transVal };
        }
    }
}
