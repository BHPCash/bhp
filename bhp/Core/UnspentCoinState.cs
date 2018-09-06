using Bhp.IO;
using System.IO;
using System.Linq;

namespace Bhp.Core
{
    /// <summary>
    /// 未花费的交易状态(可用币)
    /// <para>An unspent transaction state (available currency)</para>
    /// </summary>
    public class UnspentCoinState : StateBase, ICloneable<UnspentCoinState>
    {
        /// <summary>
        /// 每一项的状态
        /// </summary>
        public CoinState[] Items;

        public override int Size => base.Size + Items.GetVarSize();

        UnspentCoinState ICloneable<UnspentCoinState>.Clone()
        {
            return new UnspentCoinState
            {
                Items = (CoinState[])Items.Clone()
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Items = reader.ReadVarBytes().Select(p => (CoinState)p).ToArray();
        }

        void ICloneable<UnspentCoinState>.FromReplica(UnspentCoinState replica)
        {
            Items = replica.Items;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.WriteVarBytes(Items.Cast<byte>().ToArray());
        }
    }
}
