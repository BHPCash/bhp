﻿using Bhp.Core;
using Bhp.Cryptography.ECC;
using Bhp.IO.Caching;
using Bhp.VM;
using Bhp.VM.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bhp.SmartContract
{
    public class StateMachine : StateReader
    {
        private readonly Block persisting_block;
        private readonly DataCache<UInt160, AccountState> accounts;
        private readonly DataCache<UInt256, AssetState> assets;
        private readonly DataCache<UInt160, ContractState> contracts;
        private readonly DataCache<StorageKey, StorageItem> storages;

        private Dictionary<UInt160, UInt160> contracts_created = new Dictionary<UInt160, UInt160>();

        protected override DataCache<UInt160, AccountState> Accounts => accounts;
        protected override DataCache<UInt256, AssetState> Assets => assets;
        protected override DataCache<UInt160, ContractState> Contracts => contracts;
        protected override DataCache<StorageKey, StorageItem> Storages => storages;

        public StateMachine(Block persisting_block, DataCache<UInt160, AccountState> accounts, DataCache<UInt256, AssetState> assets, DataCache<UInt160, ContractState> contracts, DataCache<StorageKey, StorageItem> storages)
        {
            this.persisting_block = persisting_block;
            this.accounts = accounts.CreateSnapshot();
            this.assets = assets.CreateSnapshot();
            this.contracts = contracts.CreateSnapshot();
            this.storages = storages.CreateSnapshot();
            Register("Bhp.Asset.Create", Asset_Create);
            Register("Bhp.Asset.Renew", Asset_Renew);
            Register("Bhp.Contract.Create", Contract_Create);
            Register("Bhp.Contract.Migrate", Contract_Migrate);
            Register("Bhp.Contract.GetStorageContext", Contract_GetStorageContext);
            Register("Bhp.Contract.Destroy", Contract_Destroy);
            Register("Bhp.Storage.Put", Storage_Put);
            Register("Bhp.Storage.Delete", Storage_Delete);
            #region Old BHPCs APIs
            Register("BHPCs.Asset.Create", Asset_Create);
            Register("BHPCs.Asset.Renew", Asset_Renew);
            Register("BHPCs.Contract.Create", Contract_Create);
            Register("BHPCs.Contract.Migrate", Contract_Migrate);
            Register("BHPCs.Contract.GetStorageContext", Contract_GetStorageContext);
            Register("BHPCs.Contract.Destroy", Contract_Destroy);
            Register("BHPCs.Storage.Put", Storage_Put);
            Register("BHPCs.Storage.Delete", Storage_Delete);
            #endregion
        }

        public void Commit()
        {
            accounts.Commit();
            assets.Commit();
            contracts.Commit();
            storages.Commit();
        }

        protected override bool Runtime_GetTime(ExecutionEngine engine)
        {
            engine.EvaluationStack.Push(persisting_block.Timestamp);
            return true;
        }

        private bool Asset_Create(ExecutionEngine engine)
        {
            InvocationTransaction tx = (InvocationTransaction)engine.ScriptContainer;
            AssetType asset_type = (AssetType)(byte)engine.EvaluationStack.Pop().GetBigInteger();
            if (!Enum.IsDefined(typeof(AssetType), asset_type) || asset_type == AssetType.CreditFlag || asset_type == AssetType.DutyFlag || asset_type == AssetType.GoverningToken || asset_type == AssetType.UtilityToken)
                return false;
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 1024)
                return false;
            string name = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            Fixed8 amount = new Fixed8((long)engine.EvaluationStack.Pop().GetBigInteger());
            if (amount == Fixed8.Zero || amount < -Fixed8.Satoshi) return false;
            if (asset_type == AssetType.Invoice && amount != -Fixed8.Satoshi)
                return false;
            byte precision = (byte)engine.EvaluationStack.Pop().GetBigInteger();
            if (precision > 8) return false;
            if (asset_type == AssetType.Share && precision != 0) return false;
            if (amount != -Fixed8.Satoshi && amount.GetData() % (long)Math.Pow(10, 8 - precision) != 0)
                return false;
            ECPoint owner = ECPoint.DecodePoint(engine.EvaluationStack.Pop().GetByteArray(), ECCurve.Secp256r1);
            if (owner.IsInfinity) return false;
            if (!CheckWitness(engine, owner))
                return false;
            UInt160 admin = new UInt160(engine.EvaluationStack.Pop().GetByteArray());
            UInt160 issuer = new UInt160(engine.EvaluationStack.Pop().GetByteArray());
            AssetState asset = assets.GetOrAdd(tx.Hash, () => new AssetState
            {
                AssetId = tx.Hash,
                AssetType = asset_type,
                Name = name,
                Amount = amount,
                Available = Fixed8.Zero,
                Precision = precision,
                Fee = Fixed8.Zero,
                FeeAddress = new UInt160(),
                Owner = owner,
                Admin = admin,
                Issuer = issuer,
                Expiration = Blockchain.Default.Height + 1 + 2000000,
                IsFrozen = false
            });
            engine.EvaluationStack.Push(StackItem.FromInterface(asset));
            return true;
        }

        private bool Asset_Renew(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                AssetState asset = _interface.GetInterface<AssetState>();
                if (asset == null) return false;
                byte years = (byte)engine.EvaluationStack.Pop().GetBigInteger();
                asset = assets.GetAndChange(asset.AssetId);
                if (asset.Expiration < Blockchain.Default.Height + 1)
                    asset.Expiration = Blockchain.Default.Height + 1;
                try
                {
                    asset.Expiration = checked(asset.Expiration + years * 2000000u);
                }
                catch (OverflowException)
                {
                    asset.Expiration = uint.MaxValue;
                }
                engine.EvaluationStack.Push(asset.Expiration);
                return true;
            }
            return false;
        }

        private bool Contract_Create(ExecutionEngine engine)
        {
            byte[] script = engine.EvaluationStack.Pop().GetByteArray();
            if (script.Length > 1024 * 1024) return false;
            ContractParameterType[] parameter_list = engine.EvaluationStack.Pop().GetByteArray().Select(p => (ContractParameterType)p).ToArray();
            if (parameter_list.Length > 252) return false;
            ContractParameterType return_type = (ContractParameterType)(byte)engine.EvaluationStack.Pop().GetBigInteger();
            ContractPropertyState contract_properties = (ContractPropertyState)(byte)engine.EvaluationStack.Pop().GetBigInteger();
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string name = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string version = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string author = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string email = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 65536) return false;
            string description = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            UInt160 hash = script.ToScriptHash();
            ContractState contract = contracts.TryGet(hash);
            if (contract == null)
            {
                contract = new ContractState
                {
                    Script = script,
                    ParameterList = parameter_list,
                    ReturnType = return_type,
                    ContractProperties = contract_properties,
                    Name = name,
                    CodeVersion = version,
                    Author = author,
                    Email = email,
                    Description = description
                };
                contracts.Add(hash, contract);
                contracts_created.Add(hash, new UInt160(engine.CurrentContext.ScriptHash));
            }
            engine.EvaluationStack.Push(StackItem.FromInterface(contract));
            return true;
        }

        private bool Contract_Migrate(ExecutionEngine engine)
        {
            byte[] script = engine.EvaluationStack.Pop().GetByteArray();
            if (script.Length > 1024 * 1024) return false;
            ContractParameterType[] parameter_list = engine.EvaluationStack.Pop().GetByteArray().Select(p => (ContractParameterType)p).ToArray();
            if (parameter_list.Length > 252) return false;
            ContractParameterType return_type = (ContractParameterType)(byte)engine.EvaluationStack.Pop().GetBigInteger();
            ContractPropertyState contract_properties = (ContractPropertyState)(byte)engine.EvaluationStack.Pop().GetBigInteger();
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string name = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string version = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string author = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 252) return false;
            string email = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            if (engine.EvaluationStack.Peek().GetByteArray().Length > 65536) return false;
            string description = Encoding.UTF8.GetString(engine.EvaluationStack.Pop().GetByteArray());
            UInt160 hash = script.ToScriptHash();
            ContractState contract = contracts.TryGet(hash);
            if (contract == null)
            {
                contract = new ContractState
                {
                    Script = script,
                    ParameterList = parameter_list,
                    ReturnType = return_type,
                    ContractProperties = contract_properties,
                    Name = name,
                    CodeVersion = version,
                    Author = author,
                    Email = email,
                    Description = description
                };
                contracts.Add(hash, contract);
                contracts_created.Add(hash, new UInt160(engine.CurrentContext.ScriptHash));
                if (contract.HasStorage)
                {
                    foreach (var pair in storages.Find(engine.CurrentContext.ScriptHash).ToArray())
                    {
                        storages.Add(new StorageKey
                        {
                            ScriptHash = hash,
                            Key = pair.Key.Key
                        }, new StorageItem
                        {
                            Value = pair.Value.Value
                        });
                    }
                }
            }
            engine.EvaluationStack.Push(StackItem.FromInterface(contract));
            return Contract_Destroy(engine);
        }

        private bool Contract_GetStorageContext(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                ContractState contract = _interface.GetInterface<ContractState>();
                if (!contracts_created.TryGetValue(contract.ScriptHash, out UInt160 created)) return false;
                if (!created.Equals(new UInt160(engine.CurrentContext.ScriptHash))) return false;
                engine.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
                {
                    ScriptHash = contract.ScriptHash,
                    IsReadOnly = false
                }));
                return true;
            }
            return false;
        }

        private bool Contract_Destroy(ExecutionEngine engine)
        {
            UInt160 hash = new UInt160(engine.CurrentContext.ScriptHash);
            ContractState contract = contracts.TryGet(hash);
            if (contract == null) return true;
            contracts.Delete(hash);
            if (contract.HasStorage)
                foreach (var pair in storages.Find(hash.ToArray()))
                    storages.Delete(pair.Key);
            return true;
        }

        private bool Storage_Put(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (context.IsReadOnly) return false;
                if (!CheckStorageContext(context)) return false;
                byte[] key = engine.EvaluationStack.Pop().GetByteArray();
                if (key.Length > 1024) return false;
                byte[] value = engine.EvaluationStack.Pop().GetByteArray();
                storages.GetAndChange(new StorageKey
                {
                    ScriptHash = context.ScriptHash,
                    Key = key
                }, () => new StorageItem()).Value = value;
                return true;
            }
            return false;
        }

        private bool Storage_Delete(ExecutionEngine engine)
        {
            if (engine.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (context.IsReadOnly) return false;
                if (!CheckStorageContext(context)) return false;
                byte[] key = engine.EvaluationStack.Pop().GetByteArray();
                storages.Delete(new StorageKey
                {
                    ScriptHash = context.ScriptHash,
                    Key = key
                });
                return true;
            }
            return false;
        }
    }
}
