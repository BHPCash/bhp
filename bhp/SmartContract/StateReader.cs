﻿using Bhp.Core;
using Bhp.Cryptography.ECC;
using Bhp.IO;
using Bhp.IO.Caching;
using Bhp.SmartContract.Enumerators;
using Bhp.SmartContract.Iterators;
using Bhp.VM;
using Bhp.VM.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using VMArray = Bhp.VM.Types.Array;
using VMBoolean = Bhp.VM.Types.Boolean;

namespace Bhp.SmartContract
{
    public class StateReader : InteropService, IDisposable
    {
        public static event EventHandler<NotifyEventArgs> Notify;
        public static event EventHandler<LogEventArgs> Log;

        private readonly List<NotifyEventArgs> notifications = new List<NotifyEventArgs>();
        private readonly List<IDisposable> disposables = new List<IDisposable>();

        public IReadOnlyList<NotifyEventArgs> Notifications => notifications;

        private DataCache<UInt160, AccountState> _accounts;
        protected virtual DataCache<UInt160, AccountState> Accounts
        {
            get
            {
                if (_accounts == null)
                    _accounts = Blockchain.Default.GetStates<UInt160, AccountState>();
                return _accounts;
            }
        }

        private DataCache<UInt256, AssetState> _assets;
        protected virtual DataCache<UInt256, AssetState> Assets
        {
            get
            {
                if (_assets == null)
                    _assets = Blockchain.Default.GetStates<UInt256, AssetState>();
                return _assets;
            }
        }

        private DataCache<UInt160, ContractState> _contracts;
        protected virtual DataCache<UInt160, ContractState> Contracts
        {
            get
            {
                if (_contracts == null)
                    _contracts = Blockchain.Default.GetStates<UInt160, ContractState>();
                return _contracts;
            }
        }

        private DataCache<StorageKey, StorageItem> _storages;
        protected virtual DataCache<StorageKey, StorageItem> Storages
        {
            get
            {
                if (_storages == null)
                    _storages = Blockchain.Default.GetStates<StorageKey, StorageItem>();
                return _storages;
            }
        }

        public StateReader()
        {
            Register("Bhp.Runtime.GetTrigger", Runtime_GetTrigger);
            Register("Bhp.Runtime.CheckWitness", Runtime_CheckWitness);
            Register("Bhp.Runtime.Notify", Runtime_Notify);
            Register("Bhp.Runtime.Log", Runtime_Log);
            Register("Bhp.Runtime.GetTime", Runtime_GetTime);
            Register("Bhp.Runtime.Serialize", Runtime_Serialize);
            Register("Bhp.Runtime.Deserialize", Runtime_Deserialize);
            Register("Bhp.Blockchain.GetHeight", Blockchain_GetHeight);
            Register("Bhp.Blockchain.GetHeader", Blockchain_GetHeader);
            Register("Bhp.Blockchain.GetBlock", Blockchain_GetBlock);
            Register("Bhp.Blockchain.GetTransaction", Blockchain_GetTransaction);
            Register("Bhp.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight);
            Register("Bhp.Blockchain.GetAccount", Blockchain_GetAccount);
            Register("Bhp.Blockchain.GetValidators", Blockchain_GetValidators);
            Register("Bhp.Blockchain.GetAsset", Blockchain_GetAsset);
            Register("Bhp.Blockchain.GetContract", Blockchain_GetContract);
            Register("Bhp.Header.GetIndex", Header_GetIndex);
            Register("Bhp.Header.GetHash", Header_GetHash);
            Register("Bhp.Header.GetVersion", Header_GetVersion);
            Register("Bhp.Header.GetPrevHash", Header_GetPrevHash);
            Register("Bhp.Header.GetMerkleRoot", Header_GetMerkleRoot);
            Register("Bhp.Header.GetTimestamp", Header_GetTimestamp);
            Register("Bhp.Header.GetConsensusData", Header_GetConsensusData);
            Register("Bhp.Header.GetNextConsensus", Header_GetNextConsensus);
            Register("Bhp.Block.GetTransactionCount", Block_GetTransactionCount);
            Register("Bhp.Block.GetTransactions", Block_GetTransactions);
            Register("Bhp.Block.GetTransaction", Block_GetTransaction);
            Register("Bhp.Transaction.GetHash", Transaction_GetHash);
            Register("Bhp.Transaction.GetType", Transaction_GetType);
            Register("Bhp.Transaction.GetAttributes", Transaction_GetAttributes);
            Register("Bhp.Transaction.GetInputs", Transaction_GetInputs);
            Register("Bhp.Transaction.GetOutputs", Transaction_GetOutputs);
            Register("Bhp.Transaction.GetReferences", Transaction_GetReferences);
            Register("Bhp.Transaction.GetUnspentCoins", Transaction_GetUnspentCoins);
            Register("Bhp.InvocationTransaction.GetScript", InvocationTransaction_GetScript);
            Register("Bhp.Attribute.GetUsage", Attribute_GetUsage);
            Register("Bhp.Attribute.GetData", Attribute_GetData);
            Register("Bhp.Input.GetHash", Input_GetHash);
            Register("Bhp.Input.GetIndex", Input_GetIndex);
            Register("Bhp.Output.GetAssetId", Output_GetAssetId);
            Register("Bhp.Output.GetValue", Output_GetValue);
            Register("Bhp.Output.GetScriptHash", Output_GetScriptHash);
            Register("Bhp.Account.GetScriptHash", Account_GetScriptHash);
            Register("Bhp.Account.GetVotes", Account_GetVotes);
            Register("Bhp.Account.GetBalance", Account_GetBalance);
            Register("Bhp.Asset.GetAssetId", Asset_GetAssetId);
            Register("Bhp.Asset.GetAssetType", Asset_GetAssetType);
            Register("Bhp.Asset.GetAmount", Asset_GetAmount);
            Register("Bhp.Asset.GetAvailable", Asset_GetAvailable);
            Register("Bhp.Asset.GetPrecision", Asset_GetPrecision);
            Register("Bhp.Asset.GetOwner", Asset_GetOwner);
            Register("Bhp.Asset.GetAdmin", Asset_GetAdmin);
            Register("Bhp.Asset.GetIssuer", Asset_GetIssuer);
            Register("Bhp.Contract.GetScript", Contract_GetScript);
            Register("Bhp.Contract.IsPayable", Contract_IsPayable);
            Register("Bhp.Storage.GetContext", Storage_GetContext);
            Register("Bhp.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext);
            Register("Bhp.Storage.Get", Storage_Get);
            Register("Bhp.Storage.Find", Storage_Find);
            Register("Bhp.StorageContext.AsReadOnly", StorageContext_AsReadOnly);
            Register("Bhp.Enumerator.Create", Enumerator_Create);
            Register("Bhp.Enumerator.Next", Enumerator_Next);
            Register("Bhp.Enumerator.Value", Enumerator_Value);
            Register("Bhp.Enumerator.Concat", Enumerator_Concat);
            Register("Bhp.Iterator.Create", Iterator_Create);
            Register("Bhp.Iterator.Key", Iterator_Key);
            Register("Bhp.Iterator.Keys", Iterator_Keys);
            Register("Bhp.Iterator.Values", Iterator_Values);
            #region Aliases
            Register("Bhp.Iterator.Next", Enumerator_Next);
            Register("Bhp.Iterator.Value", Enumerator_Value);
            #endregion
            #region Old BHPCs APIs
            Register("BHPCs.Runtime.CheckWitness", Runtime_CheckWitness);
            Register("BHPCs.Runtime.Notify", Runtime_Notify);
            Register("BHPCs.Runtime.Log", Runtime_Log);
            Register("BHPCs.Blockchain.GetHeight", Blockchain_GetHeight);
            Register("BHPCs.Blockchain.GetHeader", Blockchain_GetHeader);
            Register("BHPCs.Blockchain.GetBlock", Blockchain_GetBlock);
            Register("BHPCs.Blockchain.GetTransaction", Blockchain_GetTransaction);
            Register("BHPCs.Blockchain.GetAccount", Blockchain_GetAccount);
            Register("BHPCs.Blockchain.GetValidators", Blockchain_GetValidators);
            Register("BHPCs.Blockchain.GetAsset", Blockchain_GetAsset);
            Register("BHPCs.Blockchain.GetContract", Blockchain_GetContract);
            Register("BHPCs.Header.GetHash", Header_GetHash);
            Register("BHPCs.Header.GetVersion", Header_GetVersion);
            Register("BHPCs.Header.GetPrevHash", Header_GetPrevHash);
            Register("BHPCs.Header.GetMerkleRoot", Header_GetMerkleRoot);
            Register("BHPCs.Header.GetTimestamp", Header_GetTimestamp);
            Register("BHPCs.Header.GetConsensusData", Header_GetConsensusData);
            Register("BHPCs.Header.GetNextConsensus", Header_GetNextConsensus);
            Register("BHPCs.Block.GetTransactionCount", Block_GetTransactionCount);
            Register("BHPCs.Block.GetTransactions", Block_GetTransactions);
            Register("BHPCs.Block.GetTransaction", Block_GetTransaction);
            Register("BHPCs.Transaction.GetHash", Transaction_GetHash);
            Register("BHPCs.Transaction.GetType", Transaction_GetType);
            Register("BHPCs.Transaction.GetAttributes", Transaction_GetAttributes);
            Register("BHPCs.Transaction.GetInputs", Transaction_GetInputs);
            Register("BHPCs.Transaction.GetOutputs", Transaction_GetOutputs);
            Register("BHPCs.Transaction.GetReferences", Transaction_GetReferences);
            Register("BHPCs.Attribute.GetUsage", Attribute_GetUsage);
            Register("BHPCs.Attribute.GetData", Attribute_GetData);
            Register("BHPCs.Input.GetHash", Input_GetHash);
            Register("BHPCs.Input.GetIndex", Input_GetIndex);
            Register("BHPCs.Output.GetAssetId", Output_GetAssetId);
            Register("BHPCs.Output.GetValue", Output_GetValue);
            Register("BHPCs.Output.GetScriptHash", Output_GetScriptHash);
            Register("BHPCs.Account.GetScriptHash", Account_GetScriptHash);
            Register("BHPCs.Account.GetVotes", Account_GetVotes);
            Register("BHPCs.Account.GetBalance", Account_GetBalance);
            Register("BHPCs.Asset.GetAssetId", Asset_GetAssetId);
            Register("BHPCs.Asset.GetAssetType", Asset_GetAssetType);
            Register("BHPCs.Asset.GetAmount", Asset_GetAmount);
            Register("BHPCs.Asset.GetAvailable", Asset_GetAvailable);
            Register("BHPCs.Asset.GetPrecision", Asset_GetPrecision);
            Register("BHPCs.Asset.GetOwner", Asset_GetOwner);
            Register("BHPCs.Asset.GetAdmin", Asset_GetAdmin);
            Register("BHPCs.Asset.GetIssuer", Asset_GetIssuer);
            Register("BHPCs.Contract.GetScript", Contract_GetScript);
            Register("BHPCs.Storage.GetContext", Storage_GetContext);
            Register("BHPCs.Storage.Get", Storage_Get);
            #endregion
        }

        internal bool CheckStorageContext(StorageContext context)
        {
            ContractState contract = Contracts.TryGet(context.ScriptHash);
            if (contract == null) return false;
            if (!contract.HasStorage) return false;
            return true;
        }

        public void Dispose()
        {
            foreach (IDisposable disposable in disposables)
                disposable.Dispose();
            disposables.Clear();
        }

        protected virtual bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            ApplicationEngine app_engine = (ApplicationEngine)engine;
            engine.EvaluationStack.Push((int)app_engine.Trigger);
            return true;
        }

        protected bool CheckWitness(ExecutionEngine engine, UInt160 hash)
        {
            IVerifiable container = (IVerifiable)engine.ScriptContainer;
            UInt160[] _hashes_for_verifying = container.GetScriptHashesForVerifying();
            return _hashes_for_verifying.Contains(hash);
        }

        protected bool CheckWitness(ExecutionEngine engine, ECPoint pubkey)
        {
            return CheckWitness(engine, Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash());
        }

        protected virtual bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            byte[] hashOrPubkey = engine.EvaluationStack.Pop().GetByteArray();
            bool result;
            if (hashOrPubkey.Length == 20)
                result = CheckWitness(engine, new UInt160(hashOrPubkey));
            else if (hashOrPubkey.Length == 33)
                result = CheckWitness(engine, ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1));
            else
                return false;
            engine.EvaluationStack.Push(result);
            return true;
        }

        protected virtual bool Runtime_Notify(ExecutionEngine engine)
        {
            StackItem state = engine.EvaluationStack.Pop();
            NotifyEventArgs notification = new NotifyEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), state);
            Notify?.Invoke(this, notification);
            notifications.Add(notification);
            return true;
        }

        protected virtual bool Runtime_Log(ExecutionEngine engine)
        {
            string message = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            Log?.Invoke(this, new LogEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), message));
            return true;
        }

        protected virtual bool Runtime_GetTime(ExecutionEngine engine)
        {
            BlockBase header = Blockchain.Default?.GetHeader(Blockchain.Default.Height);
            if (header == null) header = Blockchain.GenesisBlock;
            engine.EvaluationStack.Push(header.Timestamp + Blockchain.SecondsPerBlock);
            return true;
        }

        private void SerializeStackItem(StackItem item, BinaryWriter writer)
        {
            switch (item)
            {
                case ByteArray _:
                    writer.Write((byte)StackItemType.ByteArray);
                    writer.WriteVarBytes(item.GetByteArray());
                    break;
                case VMBoolean _:
                    writer.Write((byte)StackItemType.Boolean);
                    writer.Write(item.GetBoolean());
                    break;
                case Integer _:
                    writer.Write((byte)StackItemType.Integer);
                    writer.WriteVarBytes(item.GetByteArray());
                    break;
                case InteropInterface _:
                    throw new NotSupportedException();
                case VMArray array:
                    if (array is Struct)
                        writer.Write((byte)StackItemType.Struct);
                    else
                        writer.Write((byte)StackItemType.Array);
                    writer.WriteVarInt(array.Count);
                    foreach (StackItem subitem in array)
                        SerializeStackItem(subitem, writer);
                    break;
                case Map map:
                    writer.Write((byte)StackItemType.Map);
                    writer.WriteVarInt(map.Count);
                    foreach (var pair in map)
                    {
                        SerializeStackItem(pair.Key, writer);
                        SerializeStackItem(pair.Value, writer);
                    }
                    break;
            }
        }

        protected virtual bool Runtime_Serialize(ExecutionEngine engine)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                try
                {
                    SerializeStackItem(engine.EvaluationStack.Pop(), writer);
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                writer.Flush();
                engine.EvaluationStack.Push(ms.ToArray());
            }
            return true;
        }

        private StackItem DeserializeStackItem(BinaryReader reader)
        {
            StackItemType type = (StackItemType)reader.ReadByte();
            switch (type)
            {
                case StackItemType.ByteArray:
                    return new ByteArray(reader.ReadVarBytes());
                case StackItemType.Boolean:
                    return new VMBoolean(reader.ReadBoolean());
                case StackItemType.Integer:
                    return new Integer(new BigInteger(reader.ReadVarBytes()));
                case StackItemType.Array:
                case StackItemType.Struct:
                    {
                        VMArray array = type == StackItemType.Struct ? new Struct() : new VMArray();
                        ulong count = reader.ReadVarInt();
                        while (count-- > 0)
                            array.Add(DeserializeStackItem(reader));
                        return array;
                    }
                case StackItemType.Map:
                    {
                        Map map = new Map();
                        ulong count = reader.ReadVarInt();
                        while (count-- > 0)
                        {
                            StackItem key = DeserializeStackItem(reader);
                            StackItem value = DeserializeStackItem(reader);
                            map[key] = value;
                        }
                        return map;
                    }
                default:
                    throw new FormatException();
            }
        }

        protected virtual bool Runtime_Deserialize(ExecutionEngine engine)
        {
            byte[] data = engine.EvaluationStack.Pop().GetByteArray();
            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                StackItem item;
                try
                {
                    item = DeserializeStackItem(reader);
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
                engine.EvaluationStack.Push(item);
            }
            return true;
        }

        protected virtual bool Blockchain_GetHeight(ExecutionEngine engine)
        {
            if (Blockchain.Default == null)
                engine.EvaluationStack.Push(0);
            else
                engine.EvaluationStack.Push(Blockchain.Default.Height);
            return true;
        }

        protected virtual bool Blockchain_GetHeader(ExecutionEngine engine)
        {
            byte[] data = engine.EvaluationStack.Pop().GetByteArray();
            Header header;
            if (data.Length <= 5)
            {
                uint height = (uint)new BigInteger(data);
                if (Blockchain.Default != null)
                    header = Blockchain.Default.GetHeader(height);
                else if (height == 0)
                    header = Blockchain.GenesisBlock.Header;
                else
                    header = null;
            }
            else if (data.Length == 32)
            {
                UInt256 hash = new UInt256(data);
                if (Blockchain.Default != null)
                    header = Blockchain.Default.GetHeader(hash);
                else if (hash == Blockchain.GenesisBlock.Hash)
                    header = Blockchain.GenesisBlock.Header;
                else
                    header = null;
            }
            else
            {
                return false;
            }
            engine.EvaluationStack.Push(StackItem.FromInterface(header));
            return true;
        }

        protected virtual bool Blockchain_GetBlock(ExecutionEngine engine)
        {
            byte[] data = engine.EvaluationStack.Pop().GetByteArray();
            Block block;
            if (data.Length <= 5)
            {
                uint height = (uint)new BigInteger(data);
                if (Blockchain.Default != null)
                    block = Blockchain.Default.GetBlock(height);
                else if (height == 0)
                    block = Blockchain.GenesisBlock;
                else
                    block = null;
            }
            else if (data.Length == 32)
            {
                UInt256 hash = new UInt256(data);
                if (Blockchain.Default != null)
                    block = Blockchain.Default.GetBlock(hash);
                else if (hash == Blockchain.GenesisBlock.Hash)
                    block = Blockchain.GenesisBlock;
                else
                    block = null;
            }
            else
            {
                return false;
            }
            engine.EvaluationStack.Push(StackItem.FromInterface(block));
            return true;
        }

        protected virtual bool Blockchain_GetTransaction(ExecutionEngine engine)
        {
            byte[] hash = engine.EvaluationStack.Pop().GetByteArray();
            Transaction tx = Blockchain.Default?.GetTransaction(new UInt256(hash));
            engine.EvaluationStack.Push(StackItem.FromInterface(tx));
            return true;
        }

        protected virtual bool Blockchain_GetTransactionHeight(ExecutionEngine engine)
        {
            byte[] hash = engine.EvaluationStack.Pop().GetByteArray();
            int height;
            if (Blockchain.Default == null)
                height = -1;
            else
                Blockchain.Default.GetTransaction(new UInt256(hash), out height);
            engine.EvaluationStack.Push(height);
            return true;
        }

        protected virtual bool Blockchain_GetAccount(ExecutionEngine engine)
        {
            UInt160 hash = new UInt160(engine.EvaluationStack.Pop().GetByteArray());
            AccountState account = Accounts.GetOrAdd(hash, () => new AccountState(hash));
            engine.EvaluationStack.Push(StackItem.FromInterface(account));
            return true;
        }

        protected virtual bool Blockchain_GetValidators(ExecutionEngine engine)
        {
            ECPoint[] validators = Blockchain.Default.GetValidators();
            engine.EvaluationStack.Push(validators.Select(p => (StackItem)p.EncodePoint(true)).ToArray());
            return true;
        }

        protected virtual bool Blockchain_GetAsset(ExecutionEngine engine)
        {
            UInt256 hash = new UInt256(engine.EvaluationStack.Pop().GetByteArray());
            AssetState asset = Assets.TryGet(hash);
            if (asset == null) return false;
            engine.EvaluationStack.Push(StackItem.FromInterface(asset));
            return true;
        }

        protected virtual bool Blockchain_GetContract(ExecutionEngine engine)
        {
            UInt160 hash = new UInt160(engine.EvaluationStack.Pop().GetByteArray());
            ContractState contract = Contracts.TryGet(hash);
            if (contract == null)
                engine.EvaluationStack.Push(new byte[0]);
            else
                engine.EvaluationStack.Push(StackItem.FromInterface(contract));
            return true;
        }

        protected virtual bool Header_GetIndex(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.Index);
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.Hash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetVersion(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.Version);
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetPrevHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.PrevHash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetMerkleRoot(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.MerkleRoot.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetTimestamp(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.Timestamp);
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetConsensusData(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.ConsensusData);
                return true;
            }
            return false;
        }

        protected virtual bool Header_GetNextConsensus(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.EvaluationStack.Push(header.NextConsensus.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Block_GetTransactionCount(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                if (block == null) return false;
                engine.EvaluationStack.Push(block.Transactions.Length);
                return true;
            }
            return false;
        }

        protected virtual bool Block_GetTransactions(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                if (block == null) return false;
                engine.EvaluationStack.Push(block.Transactions.Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Block_GetTransaction(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                int index = (int)engine.EvaluationStack.Pop().GetBigInteger();
                if (block == null) return false;
                if (index < 0 || index >= block.Transactions.Length) return false;
                Transaction tx = block.Transactions[index];
                engine.EvaluationStack.Push(StackItem.FromInterface(tx));
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Hash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetType(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push((int)tx.Type);
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetAttributes(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Attributes.Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetInputs(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Inputs.Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetOutputs(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Outputs.Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetReferences(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Inputs.Select(p => StackItem.FromInterface(tx.References[p])).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Transaction_GetUnspentCoins(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(Blockchain.Default.GetUnspent(tx.Hash).Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool InvocationTransaction_GetScript(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                InvocationTransaction tx = _interface.GetInterface<InvocationTransaction>();
                if (tx == null) return false;
                engine.EvaluationStack.Push(tx.Script);
                return true;
            }
            return false;
        }

        protected virtual bool Attribute_GetUsage(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransactionAttribute attr = _interface.GetInterface<TransactionAttribute>();
                if (attr == null) return false;
                engine.EvaluationStack.Push((int)attr.Usage);
                return true;
            }
            return false;
        }

        protected virtual bool Attribute_GetData(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransactionAttribute attr = _interface.GetInterface<TransactionAttribute>();
                if (attr == null) return false;
                engine.EvaluationStack.Push(attr.Data);
                return true;
            }
            return false;
        }

        protected virtual bool Input_GetHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                CoinReference input = _interface.GetInterface<CoinReference>();
                if (input == null) return false;
                engine.EvaluationStack.Push(input.PrevHash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Input_GetIndex(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                CoinReference input = _interface.GetInterface<CoinReference>();
                if (input == null) return false;
                engine.EvaluationStack.Push((int)input.PrevIndex);
                return true;
            }
            return false;
        }

        protected virtual bool Output_GetAssetId(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransactionOutput output = _interface.GetInterface<TransactionOutput>();
                if (output == null) return false;
                engine.EvaluationStack.Push(output.AssetId.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Output_GetValue(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransactionOutput output = _interface.GetInterface<TransactionOutput>();
                if (output == null) return false;
                engine.EvaluationStack.Push(output.Value.GetData());
                return true;
            }
            return false;
        }

        protected virtual bool Output_GetScriptHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                TransactionOutput output = _interface.GetInterface<TransactionOutput>();
                if (output == null) return false;
                engine.EvaluationStack.Push(output.ScriptHash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Account_GetScriptHash(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AccountState account = _interface.GetInterface<AccountState>();
                if (account == null) return false;
                engine.EvaluationStack.Push(account.ScriptHash.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Account_GetVotes(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AccountState account = _interface.GetInterface<AccountState>();
                if (account == null) return false;
                engine.EvaluationStack.Push(account.Votes.Select(p => (StackItem)p.EncodePoint(true)).ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Account_GetBalance(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AccountState account = _interface.GetInterface<AccountState>();
                UInt256 asset_id = new UInt256(engine.EvaluationStack.Pop().GetByteArray());
                if (account == null) return false;
                Fixed8 balance = account.Balances.TryGetValue(asset_id, out Fixed8 value) ? value : Fixed8.Zero;
                engine.EvaluationStack.Push(balance.GetData());
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetAssetId(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.AssetId.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetAssetType(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push((int)asset.AssetType);
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetAmount(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.Amount.GetData());
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetAvailable(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.Available.GetData());
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetPrecision(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push((int)asset.Precision);
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetOwner(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.Owner.EncodePoint(true));
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetAdmin(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.Admin.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Asset_GetIssuer(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                engine.EvaluationStack.Push(asset.Issuer.ToArray());
                return true;
            }
            return false;
        }

        protected virtual bool Contract_GetScript(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                ContractState contract = _interface.GetInterface<ContractState>();
                if (contract == null) return false;
                engine.EvaluationStack.Push(contract.Script);
                return true;
            }
            return false;
        }

        protected virtual bool Contract_IsPayable(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                ContractState contract = _interface.GetInterface<ContractState>();
                if (contract == null) return false;
                engine.EvaluationStack.Push(contract.Payable);
                return true;
            }
            return false;
        }

        protected virtual bool Storage_GetContext(ExecutionEngine engine)
        {
            engine.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
            {
                ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
                IsReadOnly = false
            }));
            return true;
        }

        protected virtual bool Storage_GetReadOnlyContext(ExecutionEngine engine)
        {
            engine.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
            {
                ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
                IsReadOnly = true
            }));
            return true;
        }

        protected virtual bool Storage_Get(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (!CheckStorageContext(context)) return false;
                byte[] key = engine.EvaluationStack.Pop().GetByteArray();
                StorageItem item = Storages.TryGet(new StorageKey
                {
                    ScriptHash = context.ScriptHash,
                    Key = key
                });
                engine.EvaluationStack.Push(item?.Value ?? new byte[0]);
                return true;
            }
            return false;
        }

        protected virtual bool Storage_Find(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (!CheckStorageContext(context)) return false;
                byte[] prefix = engine.EvaluationStack.Pop().GetByteArray();
                byte[] prefix_key;
                using (MemoryStream ms = new MemoryStream())
                {
                    int index = 0;
                    int remain = prefix.Length;
                    while (remain >= 16)
                    {
                        ms.Write(prefix, index, 16);
                        ms.WriteByte(0);
                        index += 16;
                        remain -= 16;
                    }
                    if (remain > 0)
                        ms.Write(prefix, index, remain);
                    prefix_key = context.ScriptHash.ToArray().Concat(ms.ToArray()).ToArray();
                }
                StorageIterator iterator = new StorageIterator(Storages.Find(prefix_key).Where(p => p.Key.Key.Take(prefix.Length).SequenceEqual(prefix)).GetEnumerator());
                engine.EvaluationStack.Push(StackItem.FromInterface(iterator));
                disposables.Add(iterator);
                return true;
            }
            return false;
        }

        protected virtual bool StorageContext_AsReadOnly(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (!context.IsReadOnly)
                    context = new StorageContext
                    {
                        ScriptHash = context.ScriptHash,
                        IsReadOnly = true
                    };
                engine.EvaluationStack.Push(StackItem.FromInterface(context));
                return true;
            }
            return false;
        }

        protected virtual bool Enumerator_Create(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is VMArray array)
            {
                IEnumerator enumerator = new ArrayWrapper(array);
                engine.EvaluationStack.Push(StackItem.FromInterface(enumerator));
                return true;
            }
            return false;
        }

        protected virtual bool Enumerator_Next(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                IEnumerator enumerator = _interface.GetInterface<IEnumerator>();
                engine.EvaluationStack.Push(enumerator.Next());
                return true;
            }
            return false;
        }

        protected virtual bool Enumerator_Value(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                IEnumerator enumerator = _interface.GetInterface<IEnumerator>();
                engine.EvaluationStack.Push(enumerator.Value());
                return true;
            }
            return false;
        }

        protected virtual bool Enumerator_Concat(ExecutionEngine engine)
        {
            if (!(engine.EvaluationStack.Pop() is InteropInterface _interface1)) return false;
            if (!(engine.EvaluationStack.Pop() is InteropInterface _interface2)) return false;
            IEnumerator first = _interface1.GetInterface<IEnumerator>();
            IEnumerator second = _interface2.GetInterface<IEnumerator>();
            IEnumerator result = new ConcatenatedEnumerator(first, second);
            engine.EvaluationStack.Push(StackItem.FromInterface(result));
            return true;
        }

        protected virtual bool Iterator_Create(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is Map map)
            {
                IIterator iterator = new MapWrapper(map);
                engine.EvaluationStack.Push(StackItem.FromInterface(iterator));
                return true;
            }
            return false;
        }

        protected virtual bool Iterator_Key(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                IIterator iterator = _interface.GetInterface<IIterator>();
                engine.EvaluationStack.Push(iterator.Key());
                return true;
            }
            return false;
        }

        protected virtual bool Iterator_Keys(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                IIterator iterator = _interface.GetInterface<IIterator>();
                engine.EvaluationStack.Push(StackItem.FromInterface(new IteratorKeysWrapper(iterator)));
                return true;
            }
            return false;
        }

        protected virtual bool Iterator_Values(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                IIterator iterator = _interface.GetInterface<IIterator>();
                engine.EvaluationStack.Push(StackItem.FromInterface(new IteratorValuesWrapper(iterator)));
                return true;
            }
            return false;
        }
    }
}
