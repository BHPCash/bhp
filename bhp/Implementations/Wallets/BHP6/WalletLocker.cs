using System;

namespace Bhp.Implementations.Wallets.BHP6
{
    internal class WalletLocker : IDisposable
    {
        private BHP6Wallet wallet;

        public WalletLocker(BHP6Wallet wallet)
        {
            this.wallet = wallet;
        }

        public void Dispose()
        {
            wallet.Lock();
        }
    }
}
