using System;
using System.Collections.Generic;
using System.Text;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class SmartContractTests : IClassFixture<SidechainTestContextFixture>
    {
        private readonly SidechainTestContext context;

        public SmartContractTests(SidechainTestContextFixture contextFixture)
        {
            this.context = contextFixture.Context;
            contextFixture.Initialize().RunSynchronously(); // Dirty, fix later
        }

        [Fact]
        public void CanCreateContract()
        {

        }
    }
}
