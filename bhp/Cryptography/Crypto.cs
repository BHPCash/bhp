using Bhp.VM;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Bhp.Cryptography
{
    public class Crypto : ICrypto
    {
        public static readonly Crypto Default = new Crypto();

        /// <summary>
        /// 一次Sha256，一次RIPEMD160
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public byte[] Hash160(byte[] message)
        {
            return message.Sha256().RIPEMD160();
        }
        /// <summary>
        /// 两次Sha256
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public byte[] Hash256(byte[] message)
        {
            return message.Sha256().Sha256();
        }

        /// <summary>
        /// 签名(Bhp/2018-08-27)
        /// 椭圆曲线数字签名算法（ECDSA）是使用椭圆曲线密码（ECC）
        /// 对数字签名算法（DSA）的模拟
        /// 模型 SHA256
        /// </summary>
        /// <param name="message">需要签名的数据，字节流</param>
        /// <param name="prikey">私钥</param>
        /// <param name="pubkey">公钥</param>
        /// <returns></returns>
        public byte[] Sign(byte[] message, byte[] prikey, byte[] pubkey)
        {
            using (var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,//表示与公钥 (Q) 和可选私钥 (D) 相关联的曲线
                D = prikey,//表示椭圆曲线加密 (ECC) 算法的私钥 D（保存为大端格式）
                Q = new ECPoint //表示椭圆曲线加密 (ECC) 算法的公钥 Q。表示椭圆曲线加密 (ECC) 结构的 (X,Y) 坐标对
                {
                    X = pubkey.Take(32).ToArray(),//表示 X 坐标,公钥前32个字节
                    Y = pubkey.Skip(32).ToArray()//	表示 Y 坐标,公钥32个字节之后的数据
                }
            }))
            {
                //使用指定的哈希算法计算指定流的哈希值，并对生成的哈希值进行签名。
                return ecdsa.SignData(message, HashAlgorithmName.SHA256);
            }
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        /// <param name="message">已签名的数据</param>
        /// <param name="signature">要验证的签名数据</param>
        /// <param name="pubkey">公钥地址，是由02和03组成的</param>
        /// <returns></returns>
        public bool VerifySignature(byte[] message, byte[] signature, byte[] pubkey)
        {
            if (pubkey.Length == 33 && (pubkey[0] == 0x02 || pubkey[0] == 0x03))
            {
                try
                {
                    pubkey = Cryptography.ECC.ECPoint.DecodePoint(pubkey, Cryptography.ECC.ECCurve.Secp256r1).EncodePoint(false).Skip(1).ToArray();
                }
                catch
                {
                    return false;
                }
            }
            else if (pubkey.Length == 65 && pubkey[0] == 0x04)
            {
                pubkey = pubkey.Skip(1).ToArray();
            }
            else if (pubkey.Length != 64)
            {
                throw new ArgumentException();
            }
            using (var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,//表示与公钥 (Q) 和可选私钥 (D) 相关联的曲线
                Q = new ECPoint//表示椭圆曲线加密 (ECC) 算法的公钥 Q。表示椭圆曲线加密 (ECC) 结构的 (X,Y) 坐标对
                {
                    X = pubkey.Take(32).ToArray(),//表示 X 坐标,公钥前32个字节
                    Y = pubkey.Skip(32).ToArray()//	表示 Y 坐标,公钥32个字节之后的数据
                }
            }))
            {
                //通过使用指定的哈希算法计算指定数据的哈希值，并将其与提供的签名进行比较，
                //验证数字签名是否有效
                return ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);
            }
        }
    }
}
