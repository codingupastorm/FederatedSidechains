using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class SmartContractSidechainTests : TestBase
    {
        private const string WalletName = "mywallet";
        private const string Password = "password";
        private const string AccountName = "account 0";
        private const string Passphrase = "passphrase";

        [Fact]
        public void FundMainChainWallet()
        {
            this.StartNodes(Chain.Main);
            this.ConnectMainChainNodes();

            NodeChain mainUser = this.MainAndSideChainNodeMap["mainUser"];
            NodeChain fedMain1 = this.MainAndSideChainNodeMap["fedMain1"];

            mainUser.Node.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);

            TestHelper.MineBlocks(mainUser.Node, (int) this.mainchainNetwork.Consensus.CoinbaseMaturity + 1);
            TestHelper.WaitForNodeToSync(mainUser.Node, fedMain1.Node);

            IEnumerable<Bitcoin.Features.Wallet.UnspentOutputReference> spendableOutputs = mainUser.Node.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName);
            Assert.Equal((long)this.mainchainNetwork.Consensus.ProofOfWorkReward, spendableOutputs.Sum(x => x.Transaction.Amount));
        }
    }
}
