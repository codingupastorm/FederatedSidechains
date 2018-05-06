﻿using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests
{
    public class GeneralPurposeWalletTransactionHandlerTest : LogsTestBase
    {
        public GeneralPurposeWalletTransactionHandlerTest()
        {
            // These tests use Network.Main.
            // Ensure that these static flags have the expected values.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        [Fact]
        public void BuildTransactionThrowsWalletExceptionWhenMoneyIsZero()
        {
            Assert.Throws<GeneralPurposeWalletException>(() =>
            {
                var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, new Mock<IGeneralPurposeWalletManager>().Object, new Mock<IGeneralPurposeWalletFeePolicy>().Object, Network.Main);

                var result = walletTransactionHandler.BuildTransaction(CreateContext(new GeneralPurposeWalletAccountReference(), "password", new Script(), Money.Zero, FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionNoSpendableTransactionsThrowsWalletException()
        {
            Assert.Throws<GeneralPurposeWalletException>(() =>
            {
                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
                wallet.AccountsRoot.ElementAt(0).Accounts.Add(
                    new GeneralPurposeAccount
					{
                        Name = "account1",
                        ExternalAddresses = new List<GeneralPurposeAddress>(),
                        InternalAddresses = new List<GeneralPurposeAddress>()
                    });

                var chain = new Mock<ConcurrentChain>();
                var block = new BlockHeader();
                chain.Setup(c => c.Tip).Returns(new ChainedBlock(block, block.GetHash(), 1));

                var dataDir = "TestData/GeneralPurposeWalletTransactionHandlerTest/BuildTransactionNoSpendableTransactionsThrowsWalletException";
                var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain.Object, NodeSettings.Default(),
                    new DataFolder(new NodeSettings(args:new string[] { $"-datadir={dataDir}" }).DataDir), new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, new Mock<IGeneralPurposeWalletFeePolicy>().Object, Network.Main);

                walletManager.Wallets.Add(wallet);

                var walletReference = new GeneralPurposeWalletAccountReference
				{
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", new Script(), new Money(500), FeeType.Medium, 2));
            });
        }

        [Fact]
        public void BuildTransactionFeeTooLowThrowsWalletException()
        {
            Assert.Throws<GeneralPurposeWalletException>(() =>
            {
                var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
                walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                    .Returns(new FeeRate(0));
				
                var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
	            wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
                var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
                var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
                var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

                var chain = new ConcurrentChain(wallet.Network);
                WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, wallet.GetAddressFromBase58(spendingKeys.Address.ToString()));

                var dataDir = "TestData/GeneralPurposeWalletTransactionHandlerTest/BuildTransactionFeeTooLowThrowsWalletException";
                var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(),
                    new DataFolder(new NodeSettings(args:new string[] { $"-datadir={dataDir}" }).DataDir), walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
                var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, Network.Main);

                walletManager.Wallets.Add(wallet);

                var walletReference = new GeneralPurposeWalletAccountReference
				{
                    AccountName = "account1",
                    WalletName = "myWallet1"
                };

                walletTransactionHandler.BuildTransaction(CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0));
            });
        }

        [Fact]
        public void BuildTransactionNoChangeAdressesLeftCreatesNewChangeAddress()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
	        wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);

            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, wallet.GetAddressFromBase58(spendingKeys.Address.ToString()));
            var addressTransaction = wallet.GetAddressFromBase58(spendingKeys.Address.ToString()).Transactions.First();

            var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                .Returns(new FeeRate(20000));

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), dataFolder,
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

            var walletReference = new GeneralPurposeWalletAccountReference
			{
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            var context = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            var transactionResult = walletTransactionHandler.BuildTransaction(context);

            var result = new Transaction(transactionResult.ToHex());
	        var expectedChangeAddressKeys = wallet.AccountsRoot.First().Accounts.First().InternalAddresses.First();

            Assert.Single(result.Inputs);
            Assert.Equal(addressTransaction.Id, result.Inputs[0].PrevOut.Hash);

            Assert.Equal(2, result.Outputs.Count);
            var output = result.Outputs[0];
            Assert.Equal((addressTransaction.Amount - context.TransactionFee - 7500), output.Value);
            Assert.Equal(expectedChangeAddressKeys.ScriptPubKey, output.ScriptPubKey);

            output = result.Outputs[1];
            Assert.Equal(7500, output.Value);
            Assert.Equal(destinationKeys.PubKey.ScriptPubKey, output.ScriptPubKey);

            Assert.Equal(addressTransaction.Amount - context.TransactionFee, result.TotalOut);
            Assert.NotNull(transactionResult.GetHash());
            Assert.Equal(result.GetHash(), transactionResult.GetHash());
        }

        [Fact]
        public void FundTransaction_Given__a_wallet_has_enough_inputs__When__adding_inputs_to_an_existing_transaction__Then__the_transaction_is_funded_successfully()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
	        wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
            var destinationKeys1 = WalletTestsHelpers.GenerateAddressKeys(wallet);
            var destinationKeys2 = WalletTestsHelpers.GenerateAddressKeys(wallet);
            var destinationKeys3 = WalletTestsHelpers.GenerateAddressKeys(wallet);

            // wallet with 4 coinbase outputs of 50 = 200 Bitcoin
            var chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, wallet.GetAddressFromBase58(spendingKeys.Address.ToString()), 4);

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));
            var overrideFeeRate = new FeeRate(20000);

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

            var walletReference = new GeneralPurposeWalletAccountReference
			{
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            // create a trx with 3 outputs 50 + 50 + 49 = 149 BTC
            var context = new TransactionBuildContext(walletReference,
                new[]
                {
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys1.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(50, MoneyUnit.BTC), ScriptPubKey = destinationKeys2.PubKey.ScriptPubKey },
                    new Recipient { Amount = new Money(49, MoneyUnit.BTC), ScriptPubKey = destinationKeys3.PubKey.ScriptPubKey }
                }
                .ToList(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };

            var fundTransaction = walletTransactionHandler.BuildTransaction(context);
            Assert.Equal(3, fundTransaction.Inputs.Count); // 3 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // 3 outputs with change

            // remove the change output
            fundTransaction.Outputs.Remove(fundTransaction.Outputs.First(f => f.ScriptPubKey == context.ChangeAddress.ScriptPubKey));
            // remove 2 inputs they will be added back by fund transaction
            fundTransaction.Inputs.RemoveAt(2);
            fundTransaction.Inputs.RemoveAt(1);
            Assert.Single(fundTransaction.Inputs); // 3 inputs

            var fundTransactionClone = fundTransaction.Clone();
            var fundContext = new TransactionBuildContext(walletReference, new List<Recipient>(), "password")
            {
                MinConfirmations = 0,
                FeeType = FeeType.Low
            };

            fundContext.OverrideFeeRate = overrideFeeRate;
            walletTransactionHandler.FundTransaction(fundContext, fundTransaction);

            foreach (var input in fundTransactionClone.Inputs) // all original inputs are still in the trx
                Assert.Contains(fundTransaction.Inputs, a => a.PrevOut == input.PrevOut);

            Assert.Equal(3, fundTransaction.Inputs.Count); // we expect 3 inputs
            Assert.Equal(4, fundTransaction.Outputs.Count); // we expect 4 outputs
            Assert.Equal(new Money(150, MoneyUnit.BTC) - fundContext.TransactionFee, fundTransaction.TotalOut);

            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys1.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys2.PubKey.ScriptPubKey);
            Assert.Contains(fundTransaction.Outputs, a => a.ScriptPubKey == destinationKeys3.PubKey.ScriptPubKey);
        }

        [Fact]
        public void Given_AnInvalidAccountIsUsed_When_GetMaximumSpendableAmountIsCalled_Then_AnExceptionIsThrown()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
                dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<GeneralPurposeWalletFeePolicy>(), Network.Main);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> { WalletTestsHelpers.CreateAccount("account 1") }
            });
            walletManager.Wallets.Add(wallet);

            Exception ex = Assert.Throws<GeneralPurposeWalletException>(() => walletTransactionHandler.GetMaximumSpendableAmount(new GeneralPurposeWalletAccountReference("wallet1", "noaccount"), FeeType.Low, true));
            Assert.NotNull(ex);
            Assert.NotNull(ex.Message);
            Assert.NotEqual(string.Empty, ex.Message);
            Assert.IsType<GeneralPurposeWalletException>(ex);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, new ConcurrentChain(Network.Main), NodeSettings.Default(),
                dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<GeneralPurposeWalletFeePolicy>(), Network.Main);

	        GeneralPurposeAccount account = WalletTestsHelpers.CreateAccount("account 1");

	        GeneralPurposeAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), 1, new SpendingDetails()));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), 1, new SpendingDetails()));

	        GeneralPurposeAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), 3, new SpendingDetails()));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), 4, new SpendingDetails()));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new GeneralPurposeWalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalledForConfirmedTransactions_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, new ConcurrentChain(Network.Main), NodeSettings.Default(),
                dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<GeneralPurposeWalletFeePolicy>(), Network.Main);

            GeneralPurposeAccount account = WalletTestsHelpers.CreateAccount("account 1");

            GeneralPurposeAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null));

            GeneralPurposeAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new GeneralPurposeWalletAccountReference("wallet1", "account 1"), FeeType.Low, false);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoConfirmedSpendableFound_Then_MaxAmountReturnsAsTheSumOfUnconfirmedTxs()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations())).Returns(new FeeRate(20000));

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, new ConcurrentChain(Network.Main), NodeSettings.Default(),
                dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, Network.Main);

            GeneralPurposeAccount account = WalletTestsHelpers.CreateAccount("account 1");

            GeneralPurposeAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(1), new Money(15000), null, null, null, new Key().ScriptPubKey));
            accountAddress1.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(2), new Money(10000), null, null, null, new Key().ScriptPubKey));

            GeneralPurposeAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(3), new Money(20000), null, null, null, new Key().ScriptPubKey));
            accountAddress2.Transactions.Add(WalletTestsHelpers.CreateTransaction(new uint256(4), new Money(120000), null, null, null, new Key().ScriptPubKey));

            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new GeneralPurposeWalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(new Money(165000), result.max + result.fee);
        }

        [Fact]
        public void Given_GetMaximumSpendableAmountIsCalled_When_ThereAreNoTransactions_Then_MaxAmountReturnsAsZero()
        {
            DataFolder dataFolder = CreateDataFolder(this);

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, new ConcurrentChain(Network.Main), NodeSettings.Default(),
                dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);

            var walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, It.IsAny<GeneralPurposeWalletFeePolicy>(), Network.Main);
            GeneralPurposeAccount account = WalletTestsHelpers.CreateAccount("account 1");
            GeneralPurposeAddress accountAddress1 = WalletTestsHelpers.CreateAddress();
            GeneralPurposeAddress accountAddress2 = WalletTestsHelpers.CreateAddress();
            account.ExternalAddresses.Add(accountAddress1);
            account.InternalAddresses.Add(accountAddress2);

            var wallet = WalletTestsHelpers.CreateWallet("wallet1");
            wallet.AccountsRoot.Add(new AccountRoot()
            {
                Accounts = new List<GeneralPurposeAccount> { account }
            });

            walletManager.Wallets.Add(wallet);

            (Money max, Money fee) result = walletTransactionHandler.GetMaximumSpendableAmount(new GeneralPurposeWalletAccountReference("wallet1", "account 1"), FeeType.Low, true);
            Assert.Equal(Money.Zero, result.max);
            Assert.Equal(Money.Zero, result.fee);
        }

        /// <summary>
        /// Tests the <see cref="WalletTransactionHandler.EstimateFee(TransactionBuildContext)"/> method by
        /// comparing it's fee calculation with the transaction fee computed for the same tx in the
        /// <see cref="WalletTransactionHandler.BuildTransaction(TransactionBuildContext)"/> method.
        /// </summary>
        [Fact]
        public void EstimateFeeWithLowFeeMatchesBuildTxLowFee()
        {
            var dataFolder = CreateDataFolder(this);

	        GeneralPurposeWallet wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet1", "password");
	        wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
            var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
            var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);

            ConcurrentChain chain = new ConcurrentChain(wallet.Network);
            WalletTestsHelpers.AddBlocksWithCoinbaseToChain(wallet.Network, chain, wallet.GetAddressFromBase58(spendingKeys.Address.ToString()));
            TransactionData addressTransaction = wallet.GetAddressFromBase58(spendingKeys.Address.ToString()).Transactions.First();

            var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetFeeRate(FeeType.Low.ToConfirmations()))
                .Returns(new FeeRate(20000));

            var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain, NodeSettings.Default(), dataFolder,
                walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default);
	        GeneralPurposeWalletTransactionHandler walletTransactionHandler = new GeneralPurposeWalletTransactionHandler(this.LoggerFactory.Object, walletManager, walletFeePolicy.Object, Network.Main);

            walletManager.Wallets.Add(wallet);

	        GeneralPurposeWalletAccountReference walletReference = new GeneralPurposeWalletAccountReference
			{
                AccountName = "account1",
                WalletName = "myWallet1"
            };

            // Context to build requires password in order to sign transaction.
            TransactionBuildContext buildContext = CreateContext(walletReference, "password", destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            walletTransactionHandler.BuildTransaction(buildContext);

            // Context for estimate does not need password.
            TransactionBuildContext estimateContext = CreateContext(walletReference, null, destinationKeys.PubKey.ScriptPubKey, new Money(7500), FeeType.Low, 0);
            Money fee = walletTransactionHandler.EstimateFee(estimateContext);

            Assert.Equal(fee, buildContext.TransactionFee);
        }

        public static TransactionBuildContext CreateContext(GeneralPurposeWalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}